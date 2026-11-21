using DevSnippetManager.Data;
using KodDepolama;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DevVault.Tests;

public class SnippetRepositoryTests : IDisposable
{
    private readonly string _tempDir;

    public SnippetRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DevVaultRepoTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    private SnippetRepository CreateRepository(string fileName)
    {
        var settings = new AppSettings { DatabasePath = Path.Combine(_tempDir, fileName) };
        return new SnippetRepository(settings);
    }

    [Fact]
    public async Task InitializeAsync_SeedsSampleSnippetsOnNewDatabase()
    {
        var repo = CreateRepository("seed.db");
        await repo.InitializeAsync();
        var all = (await repo.GetAllAsync()).ToList();
        Assert.True(all.Count >= 100, $"Expected >= 100 seeded snippets, got {all.Count}");
    }

    [Fact]
    public async Task InitializeAsync_ThrowsInvalidOperationExceptionWithPathOnCorruptOrInvalidDb()
    {
        var corruptPath = Path.Combine(_tempDir, "corrupt.db");
        await File.WriteAllTextAsync(corruptPath, "not a sqlite file at all");
        var corruptRepo = CreateRepository("corrupt.db");
        var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() => corruptRepo.InitializeAsync());
        Assert.Contains(corruptPath, ex1.Message);

        var invalidPath = @"Z:\nonexistent_drive_devvault_xyz\test.db";
        var invalidRepo = new SnippetRepository(new AppSettings { DatabasePath = invalidPath });
        var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() => invalidRepo.InitializeAsync());
        Assert.Contains(invalidPath, ex2.Message);
    }

    [Fact]
    public async Task InitializeAsync_PartialLegacySchemaUpgradeIsIdempotentAndHasNoCodePreviewColumn()
    {
        var dbPath = Path.Combine(_tempDir, "partial_legacy.db");
        await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE Snippets (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Language TEXT NOT NULL,
                    Code TEXT NOT NULL,
                    Tags TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    Folder TEXT NOT NULL DEFAULT ''
                );
                INSERT INTO Snippets (Title, Language, Code, Tags, Description, CreatedAt, UpdatedAt, Folder)
                VALUES ('Partial', 'C#', 'return 1;', '', '', '2025-01-01', '2025-01-01', 'KeepMe');
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var repo = CreateRepository("partial_legacy.db");
        await repo.InitializeAsync();
        await repo.InitializeAsync(); // Idempotent second call

        var all = (await repo.GetAllAsync()).ToList();
        Assert.Single(all);
        Assert.Equal("KeepMe", all[0].Folder);

        await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(Snippets);";
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) cols.Add(reader.GetString(1));

            Assert.Contains("IsFavorite", cols);
            Assert.Contains("UsageCount", cols);
            Assert.Contains("LastUsedAt", cols);
            Assert.Contains("Folder", cols);
            Assert.DoesNotContain("CodePreview", cols);
        }
    }

    [Fact]
    public async Task UpdateMetadata_DetachedAndNonExistentAndNonExistentIdInsert_AndReplaceAllRollback()
    {
        var dbPath = Path.Combine(_tempDir, "edge.db");
        var repo = CreateRepository("edge.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        // Non-existent Id is inserted as new
        var inserted = new Snippet { Id = 99999, Title = "NewWithBadId", Language = "C#", Code = "a" };
        await repo.AddOrUpdateAsync(inserted);
        Assert.True(inserted.Id > 0 && inserted.Id != 99999);

        // UpdateMetadataAsync on detached instance (fresh repository/context)
        var freshRepo = CreateRepository("edge.db");
        var detached = new Snippet { Id = inserted.Id, IsFavorite = true, UsageCount = 4, LastUsedAt = DateTime.Now };
        await freshRepo.UpdateMetadataAsync(detached);
        var fetched = (await freshRepo.GetAllAsync()).Single(s => s.Id == inserted.Id);
        Assert.True(fetched.IsFavorite);
        Assert.Equal(4, fetched.UsageCount);

        // UpdateMetadataAsync on non-existent Id throws, repo remains usable
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>(
            () => repo.UpdateMetadataAsync(new Snippet { Id = 88888, IsFavorite = true }));
        Assert.Single(await repo.GetAllAsync());

        // ReplaceAllAsync failure rolls back and keeps old data
        await Assert.ThrowsAnyAsync<Exception>(() => repo.ReplaceAllAsync(new List<Snippet>
        {
            new() { Title = null!, Language = "C#", Code = "bad" }
        }));
        var afterRollback = (await repo.GetAllAsync()).ToList();
        Assert.Single(afterRollback);
        Assert.Equal("NewWithBadId", afterRollback[0].Title);

        // Create a version, delete snippet, verify directly via SQLite that SnippetVersions rows are 0
        afterRollback[0].Code = "updated-code";
        await repo.AddOrUpdateAsync(afterRollback[0]);
        var deletedId = afterRollback[0].Id;
        await repo.DeleteAsync(afterRollback[0]);

        await using var conn = new SqliteConnection($"Data Source={dbPath}");
        await conn.OpenAsync();
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM SnippetVersions WHERE SnippetId = $id;";
        countCmd.Parameters.AddWithValue("$id", deletedId);
        var versionCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        Assert.Equal(0, versionCount);
    }

    [Fact]
    public async Task InitializeAsync_UpgradesLegacySchemaWithoutOrganizationColumnsOrVersionsTable()
    {
        var dbPath = Path.Combine(_tempDir, "legacy.db");
        await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE Snippets (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Language TEXT NOT NULL,
                    Code TEXT NOT NULL,
                    Tags TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                INSERT INTO Snippets (Title, Language, Code, Tags, Description, CreatedAt, UpdatedAt)
                VALUES ('Legacy', 'C#', 'return 1;', '', '', '2025-01-01', '2025-01-01');
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var repo = CreateRepository("legacy.db");
        await repo.InitializeAsync();

        var all = (await repo.GetAllAsync()).ToList();
        Assert.Single(all);
        Assert.Equal("Legacy", all[0].Title);
        Assert.Equal(string.Empty, all[0].Folder);
        Assert.False(all[0].IsFavorite);

        var versions = await repo.GetVersionsAsync(all[0].Id);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task CrudAndUpdateMetadata_WorksWithoutCreatingVersionOnMetadataOnlyChange()
    {
        var repo = CreateRepository("crud.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var snippet = new Snippet { Title = "S1", Language = "C#", Code = "v1", Folder = "Work" };
        await repo.AddOrUpdateAsync(snippet);
        Assert.True(snippet.Id > 0);
        Assert.Empty(await repo.GetVersionsAsync(snippet.Id));

        // Metadata-only update must NOT create a version
        snippet.IsFavorite = true;
        snippet.UsageCount = 3;
        snippet.LastUsedAt = DateTime.Now;
        await repo.UpdateMetadataAsync(snippet);
        Assert.Empty(await repo.GetVersionsAsync(snippet.Id));

        // Real content change MUST create a version with OLD values
        snippet.Code = "v2";
        snippet.UpdatedAt = DateTime.Now.AddMinutes(1);
        await repo.AddOrUpdateAsync(snippet);

        var versions = await repo.GetVersionsAsync(snippet.Id);
        Assert.Single(versions);
        Assert.Equal("v1", versions[0].Code);

        // Delete removes snippet and cascades/removes versions
        var id = snippet.Id;
        await repo.DeleteAsync(snippet);
        Assert.Empty(await repo.GetAllAsync());
        Assert.Empty(await repo.GetVersionsAsync(id));
    }

    [Fact]
    public async Task VersionHistory_CapsAtMax20NewestVersions()
    {
        var repo = CreateRepository("versions_cap.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var snippet = new Snippet { Title = "V", Language = "C#", Code = "v0", UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0) };
        await repo.AddOrUpdateAsync(snippet);

        for (var i = 1; i <= 25; i++)
        {
            snippet.Code = $"v{i}";
            snippet.UpdatedAt = new DateTime(2026, 1, 1, 0, i, 0);
            await repo.AddOrUpdateAsync(snippet);
        }

        var versions = await repo.GetVersionsAsync(snippet.Id);
        Assert.Equal(SnippetRepository.MaxVersionsPerSnippet, versions.Count);
        Assert.Equal("v24", versions[0].Code);
        Assert.Equal("v5", versions[^1].Code);
    }

    [Fact]
    public async Task ReplaceAllAsync_ReplacesAtomicallyAndClearsVersions()
    {
        var repo = CreateRepository("replace.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var s1 = new Snippet { Title = "Old", Language = "C#", Code = "a" };
        await repo.AddOrUpdateAsync(s1);
        s1.Code = "b";
        await repo.AddOrUpdateAsync(s1);
        Assert.Single(await repo.GetVersionsAsync(s1.Id));

        await repo.ReplaceAllAsync(new List<Snippet>
        {
            new() { Title = "New1", Language = "Python", Code = "print(1)" },
            new() { Title = "New2", Language = "SQL", Code = "SELECT 1;" }
        });

        var all = (await repo.GetAllAsync()).ToList();
        Assert.Equal(2, all.Count);
        Assert.Empty(await repo.GetVersionsAsync(s1.Id));
    }
}
