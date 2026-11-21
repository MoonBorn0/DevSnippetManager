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

    [Fact]
    public async Task SchemaUpgrade_AddsSourceColumns()
    {
        var dbPath = Path.Combine(_tempDir, "legacy_source_upgrade.db");
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
                    IsFavorite INTEGER NOT NULL DEFAULT 0,
                    UsageCount INTEGER NOT NULL DEFAULT 0,
                    LastUsedAt TEXT NULL,
                    Folder TEXT NOT NULL DEFAULT ''
                );
                INSERT INTO Snippets (Title, Language, Code, Tags, Description, CreatedAt, UpdatedAt)
                VALUES ('OldRow', 'C#', 'return 42;', '', '', '2025-01-01', '2025-01-01');
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        var repo = CreateRepository("legacy_source_upgrade.db");
        await repo.InitializeAsync();

        await using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            await conn.OpenAsync();
            await using var colCmd = conn.CreateCommand();
            colCmd.CommandText = "PRAGMA table_info(Snippets);";
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await colCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) cols.Add(reader.GetString(1));
            }

            Assert.Contains("SourceRoot", cols);
            Assert.Contains("SourceFile", cols);
            Assert.Contains("SourceSymbol", cols);
            Assert.Contains("SourceComment", cols);
            Assert.Contains("IsSourceMissing", cols);
            Assert.Contains("LastScannedAt", cols);
            Assert.DoesNotContain("IsFromSource", cols);

            await using var idxCmd = conn.CreateCommand();
            idxCmd.CommandText = "PRAGMA index_list(Snippets);";
            var indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await idxCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync()) indexes.Add(reader.GetString(1));
            }
            Assert.Contains("IX_Snippets_SourceRoot_SourceFile_SourceSymbol", indexes);
        }

        var row = Assert.Single(await repo.GetAllAsync());
        Assert.Equal(string.Empty, row.SourceRoot);
        Assert.Equal(string.Empty, row.SourceFile);
        Assert.Equal(string.Empty, row.SourceSymbol);
        Assert.Equal(string.Empty, row.SourceComment);
        Assert.False(row.IsSourceMissing);
        Assert.Null(row.LastScannedAt);
        Assert.False(row.IsFromSource);
    }

    [Fact]
    public async Task SourceFields_RoundTripThroughSaveAndJson()
    {
        var repo = CreateRepository("source_roundtrip.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var scannedAt = new DateTime(2026, 11, 21, 23, 30, 0);
        var snippet = new Snippet
        {
            Title = "OrderService.Total",
            Language = "C#",
            Code = "public decimal Total() => 0m;",
            Folder = "Shop",
            SourceRoot = @"C:\Projects\Shop",
            SourceFile = @"Services\OrderService.cs",
            SourceSymbol = "Shop.Orders.OrderService.Total()",
            SourceComment = "Toplam tutarı hesaplar.",
            IsSourceMissing = true,
            LastScannedAt = scannedAt
        };
        Assert.True(snippet.IsFromSource);

        await repo.AddOrUpdateAsync(snippet);

        var freshRepo = CreateRepository("source_roundtrip.db");
        var loaded = Assert.Single(await freshRepo.GetAllAsync());
        Assert.Equal(snippet.SourceRoot, loaded.SourceRoot);
        Assert.Equal(snippet.SourceFile, loaded.SourceFile);
        Assert.Equal(snippet.SourceSymbol, loaded.SourceSymbol);
        Assert.Equal(snippet.SourceComment, loaded.SourceComment);
        Assert.True(loaded.IsSourceMissing);
        Assert.Equal(scannedAt, loaded.LastScannedAt);
        Assert.True(loaded.IsFromSource);

        var json = System.Text.Json.JsonSerializer.Serialize(loaded);
        Assert.DoesNotContain("IsFromSource", json);
        var fromJson = System.Text.Json.JsonSerializer.Deserialize<Snippet>(json)!;
        Assert.Equal(snippet.SourceRoot, fromJson.SourceRoot);
        Assert.Equal(snippet.SourceFile, fromJson.SourceFile);
        Assert.Equal(snippet.SourceSymbol, fromJson.SourceSymbol);
        Assert.Equal(snippet.SourceComment, fromJson.SourceComment);
        Assert.True(fromJson.IsSourceMissing);
        Assert.Equal(scannedAt, fromJson.LastScannedAt);
        Assert.True(fromJson.IsFromSource);
    }

    [Fact]
    public async Task ApplyScan_InsertsUpdatesAndMarks()
    {
        var repo = CreateRepository("apply_scan.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var existing1 = new Snippet { Title = "M1", Language = "C#", Code = "v1", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M1()" };
        var existing2 = new Snippet { Title = "M2", Language = "C#", Code = "v1", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M2()" };
        await repo.AddOrUpdateAsync(existing1);
        await repo.AddOrUpdateAsync(existing2);

        var now = new DateTime(2026, 11, 21, 23, 55, 0);
        var plan = new DevSnippetManager.Services.ProjectScan.ScanPlan { ScannedAt = now };
        plan.ToInsert.Add(new Snippet { Title = "M3", Language = "C#", Code = "new", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M3()" });
        plan.ToUpdate.Add(new Snippet { Id = existing1.Id, Title = "M1", Language = "C#", Code = "v2", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M1()", LastScannedAt = now, UpdatedAt = now });
        plan.MarkMissing.Add(existing2.Id);

        var res = await repo.ApplyScanAsync(plan);
        Assert.Equal(1, res.Added);
        Assert.Equal(1, res.Updated);
        Assert.Equal(1, res.MarkedMissing);

        var all = (await repo.GetAllAsync()).ToList();
        Assert.Equal(3, all.Count);
        Assert.Equal("v2", all.Single(s => s.Id == existing1.Id).Code);
        Assert.True(all.Single(s => s.Id == existing2.Id).IsSourceMissing);
    }

    [Fact]
    public async Task ApplyScan_UpdateCreatesVersionWithOldCode()
    {
        var repo = CreateRepository("apply_scan_version.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var existing = new Snippet { Title = "M1", Language = "C#", Code = "old-code", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M1()" };
        await repo.AddOrUpdateAsync(existing);

        var plan = new DevSnippetManager.Services.ProjectScan.ScanPlan { ScannedAt = DateTime.Now };
        plan.ToUpdate.Add(new Snippet { Id = existing.Id, Title = "M1", Language = "C#", Code = "new-code", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M1()" });
        await repo.ApplyScanAsync(plan);

        var versions = await repo.GetVersionsAsync(existing.Id);
        var v = Assert.Single(versions);
        Assert.Equal("old-code", v.Code);
    }

    [Fact]
    public async Task ApplyScan_RescanCreatesNoVersions()
    {
        var repo = CreateRepository("apply_scan_rescan.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var existing = new Snippet { Title = "M1", Language = "C#", Code = "same", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M1()" };
        await repo.AddOrUpdateAsync(existing);

        var plan = new DevSnippetManager.Services.ProjectScan.ScanPlan { ScannedAt = DateTime.Now, Unchanged = 1 };
        plan.Touch.Add(existing.Id);
        await repo.ApplyScanAsync(plan);

        Assert.Empty(await repo.GetVersionsAsync(existing.Id));
    }

    [Fact]
    public async Task ApplyScan_MissingFlagCreatesNoVersion()
    {
        var repo = CreateRepository("apply_scan_missing.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var existing = new Snippet { Title = "M1", Language = "C#", Code = "c1", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M1()" };
        await repo.AddOrUpdateAsync(existing);

        var plan = new DevSnippetManager.Services.ProjectScan.ScanPlan { ScannedAt = DateTime.Now };
        plan.MarkMissing.Add(existing.Id);
        await repo.ApplyScanAsync(plan);

        Assert.Empty(await repo.GetVersionsAsync(existing.Id));
        var loaded = Assert.Single(await repo.GetAllAsync());
        Assert.True(loaded.IsSourceMissing);
    }

    [Fact]
    public async Task ApplyScan_FailureRollsBackEverything()
    {
        var repo = CreateRepository("apply_scan_rollback.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var existing = new Snippet { Title = "M1", Language = "C#", Code = "orig", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M1()" };
        await repo.AddOrUpdateAsync(existing);

        var plan = new DevSnippetManager.Services.ProjectScan.ScanPlan { ScannedAt = DateTime.Now };
        plan.ToUpdate.Add(new Snippet { Id = existing.Id, Title = "M1", Language = "C#", Code = "modified", Folder = "P", SourceRoot = @"C:\P", SourceFile = "A.cs", SourceSymbol = "A.M1()" });
        plan.ToInsert.Add(new Snippet { Title = null!, Language = "C#", Code = "invalid" });

        await Assert.ThrowsAnyAsync<Exception>(() => repo.ApplyScanAsync(plan));

        var loaded = Assert.Single(await repo.GetAllAsync());
        Assert.Equal("orig", loaded.Code);
        Assert.Empty(await repo.GetVersionsAsync(existing.Id));
    }

    [Fact]
    public async Task RelocateSourceRoot_ChangesOnlyMatchingRoot()
    {
        var repo = CreateRepository("relocate.db");
        await repo.InitializeAsync();
        await repo.ClearAsync();

        var s1 = new Snippet { Title = "A", Language = "C#", Code = "a", SourceRoot = @"C:\Old\Proj", SourceFile = "A.cs", SourceSymbol = "A()" };
        var s2 = new Snippet { Title = "B", Language = "C#", Code = "b", SourceRoot = @"C:\Other\Proj", SourceFile = "B.cs", SourceSymbol = "B()" };
        await repo.AddOrUpdateAsync(s1);
        await repo.AddOrUpdateAsync(s2);

        var count = await repo.RelocateSourceRootAsync(@"c:\old\proj\", @"D:\Moved\Proj");
        Assert.Equal(1, count);

        var all = (await repo.GetAllAsync()).ToList();
        Assert.Equal(@"D:\Moved\Proj", all.Single(s => s.Id == s1.Id).SourceRoot);
        Assert.Equal(@"C:\Other\Proj", all.Single(s => s.Id == s2.Id).SourceRoot);
        Assert.Empty(await repo.GetVersionsAsync(s1.Id));
    }
}
