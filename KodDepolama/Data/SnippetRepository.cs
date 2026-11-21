using DevSnippetManager.Services.ProjectScan;
using KodDepolama;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevSnippetManager.Data
{
    public class SnippetRepository : ISnippetRepository
    {
        public const int MaxVersionsPerSnippet = 20;

        private readonly AppDbContext _context;
        private readonly string _databasePath;

        public SnippetRepository(AppSettings settings)
        {
            _databasePath = settings.DatabasePath;
            _context = new AppDbContext(_databasePath);
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();
                await EnsureOrganizationColumnsAsync();
                await EnsureSnippetVersionsTableAsync();

                if (!await _context.Snippets.AnyAsync())
                    await SeedDatabaseAsync();
            }
            catch (Exception ex)
            {
                try
                {
                    _context.ChangeTracker.Clear();
                }
                catch
                {
                    // Preserve the intended database initialization error.
                }
                throw new InvalidOperationException($"Veritabanı açılamadı: {_databasePath}. {ex.Message}", ex);
            }
        }

        private async Task EnsureOrganizationColumnsAsync()
        {
            var connection = _context.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(Snippets);";
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));
            }

            var missingColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["IsFavorite"] = "INTEGER NOT NULL DEFAULT 0",
                ["UsageCount"] = "INTEGER NOT NULL DEFAULT 0",
                ["LastUsedAt"] = "TEXT NULL",
                ["Folder"] = "TEXT NOT NULL DEFAULT ''",
                ["SourceRoot"] = "TEXT NOT NULL DEFAULT ''",
                ["SourceFile"] = "TEXT NOT NULL DEFAULT ''",
                ["SourceSymbol"] = "TEXT NOT NULL DEFAULT ''",
                ["SourceComment"] = "TEXT NOT NULL DEFAULT ''",
                ["IsSourceMissing"] = "INTEGER NOT NULL DEFAULT 0",
                ["LastScannedAt"] = "TEXT NULL"
            };

            foreach (var missing in missingColumns)
            {
                if (columns.Contains(missing.Key))
                    continue;

                await using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE Snippets ADD COLUMN {missing.Key} {missing.Value};";
                await alter.ExecuteNonQueryAsync();
            }

            await using var createSourceIndex = connection.CreateCommand();
            createSourceIndex.CommandText = @"CREATE INDEX IF NOT EXISTS ""IX_Snippets_SourceRoot_SourceFile_SourceSymbol"" ON ""Snippets"" (""SourceRoot"", ""SourceFile"", ""SourceSymbol"");";
            await createSourceIndex.ExecuteNonQueryAsync();
        }

        private async Task EnsureSnippetVersionsTableAsync()
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            await using var createTable = connection.CreateCommand();
            createTable.CommandText = @"
CREATE TABLE IF NOT EXISTS ""SnippetVersions"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_SnippetVersions"" PRIMARY KEY AUTOINCREMENT,
    ""SnippetId"" INTEGER NOT NULL,
    ""Title"" TEXT NOT NULL,
    ""Language"" TEXT NOT NULL,
    ""Tags"" TEXT NOT NULL,
    ""Description"" TEXT NOT NULL,
    ""Folder"" TEXT NOT NULL,
    ""Code"" TEXT NOT NULL,
    ""SavedAt"" TEXT NOT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_SnippetVersions_Snippets_SnippetId"" FOREIGN KEY (""SnippetId"") REFERENCES ""Snippets"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_SnippetVersions_SnippetId_SavedAt"" ON ""SnippetVersions"" (""SnippetId"", ""SavedAt"");";
            await createTable.ExecuteNonQueryAsync();
        }

        private async Task SeedDatabaseAsync()
        {
            try
            {
                var samples = SnippetSeedData.GetProfessionalSnippets();
                await _context.Snippets.AddRangeAsync(samples);
                await _context.SaveChangesAsync();
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task<IEnumerable<Snippet>> GetAllAsync()
        {
            return await _context.Snippets.OrderBy(s => s.Language).ThenBy(s => s.Title).ToListAsync();
        }

        public async Task<IReadOnlyList<SnippetVersion>> GetVersionsAsync(int snippetId)
        {
            return await _context.SnippetVersions
                .AsNoTracking()
                .Where(v => v.SnippetId == snippetId)
                .OrderByDescending(v => v.SavedAt)
                .ThenByDescending(v => v.Id)
                .ToListAsync();
        }

        public async Task AddOrUpdateAsync(Snippet snippet)
        {
            try
            {
                Snippet? dbBefore = snippet.Id > 0
                    ? await _context.Snippets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == snippet.Id)
                    : null;

                var existing = snippet.Id > 0 ? await _context.Snippets.FindAsync(snippet.Id) : null;
                if (existing != null)
                {
                    if (dbBefore != null)
                        await SnapshotVersionIfContentChangedAsync(dbBefore, snippet);

                    _context.Entry(existing).CurrentValues.SetValues(snippet);
                }
                else
                {
                    snippet.Id = 0;
                    await _context.Snippets.AddAsync(snippet);
                }

                await _context.SaveChangesAsync();
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        private async Task SnapshotVersionIfContentChangedAsync(Snippet dbBefore, Snippet incoming)
        {
            if (!HasContentChanged(dbBefore, incoming))
                return;

            var version = new SnippetVersion
            {
                SnippetId = dbBefore.Id,
                Title = dbBefore.Title ?? string.Empty,
                Language = dbBefore.Language ?? string.Empty,
                Tags = dbBefore.Tags ?? string.Empty,
                Description = dbBefore.Description ?? string.Empty,
                Folder = dbBefore.Folder ?? string.Empty,
                Code = dbBefore.Code ?? string.Empty,
                SavedAt = dbBefore.UpdatedAt,
                CreatedAt = DateTime.Now
            };
            await _context.SnippetVersions.AddAsync(version);

            var overflow = await _context.SnippetVersions
                .Where(v => v.SnippetId == dbBefore.Id)
                .OrderByDescending(v => v.SavedAt)
                .ThenByDescending(v => v.Id)
                .Skip(MaxVersionsPerSnippet - 1)
                .ToListAsync();
            if (overflow.Count > 0)
                _context.SnippetVersions.RemoveRange(overflow);
        }

        public async Task<ScanApplyResult> ApplyScanAsync(ScanPlan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var updatedIds = new HashSet<int>();
                var now = plan.ScannedAt == default ? DateTime.Now : plan.ScannedAt;

                if (plan.ToInsert.Count > 0)
                {
                    foreach (var item in plan.ToInsert)
                        item.Id = 0;
                    await _context.Snippets.AddRangeAsync(plan.ToInsert);
                }

                foreach (var updated in plan.ToUpdate)
                {
                    var dbBefore = await _context.Snippets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == updated.Id);
                    var existing = await _context.Snippets.FindAsync(updated.Id);
                    if (existing == null || dbBefore == null)
                        continue;

                    await SnapshotVersionIfContentChangedAsync(dbBefore, updated);
                    if (HasContentChanged(dbBefore, updated))
                        updated.UpdatedAt = now;

                    _context.Entry(existing).CurrentValues.SetValues(updated);
                    updatedIds.Add(updated.Id);
                }

                foreach (var id in plan.MarkMissing)
                {
                    if (updatedIds.Contains(id)) continue;
                    var existing = await _context.Snippets.FindAsync(id);
                    if (existing != null)
                        existing.IsSourceMissing = true;
                }

                foreach (var id in plan.ClearMissing)
                {
                    if (updatedIds.Contains(id)) continue;
                    var existing = await _context.Snippets.FindAsync(id);
                    if (existing != null)
                        existing.IsSourceMissing = false;
                }

                foreach (var id in plan.Touch)
                {
                    if (updatedIds.Contains(id)) continue;
                    var existing = await _context.Snippets.FindAsync(id);
                    if (existing != null)
                        existing.LastScannedAt = now;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new ScanApplyResult(
                    plan.ToInsert.Count,
                    plan.ToUpdate.Count,
                    plan.MarkMissing.Count,
                    plan.ClearMissing.Count,
                    plan.Unchanged,
                    plan.Relinked);
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Preserve original error
                }
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task<int> RelocateSourceRootAsync(string oldRoot, string newRoot)
        {
            var normalizedOld = ProjectLocator.NormalizePath(oldRoot);
            var normalizedNew = ProjectLocator.NormalizePath(newRoot);

            try
            {
                var candidates = await _context.Snippets
                    .Where(s => s.SourceRoot != "")
                    .ToListAsync();

                var count = 0;
                foreach (var snippet in candidates)
                {
                    if (ProjectLocator.PathEquals(snippet.SourceRoot, normalizedOld))
                    {
                        snippet.SourceRoot = normalizedNew;
                        count++;
                    }
                }

                if (count > 0)
                    await _context.SaveChangesAsync();

                return count;
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        private static bool HasContentChanged(Snippet before, Snippet after)
        {
            return !string.Equals(before.Title ?? string.Empty, after.Title ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(before.Language ?? string.Empty, after.Language ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(before.Tags ?? string.Empty, after.Tags ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(before.Description ?? string.Empty, after.Description ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(before.Folder ?? string.Empty, after.Folder ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(before.Code ?? string.Empty, after.Code ?? string.Empty, StringComparison.Ordinal);
        }

        public async Task UpdateMetadataAsync(Snippet snippet)
        {
            try
            {
                var entry = _context.Entry(snippet);
                if (entry.State == EntityState.Detached)
                    _context.Snippets.Attach(snippet);

                entry = _context.Entry(snippet);
                var rows = await _context.Snippets
                    .Where(s => s.Id == snippet.Id)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(s => s.IsFavorite, snippet.IsFavorite)
                        .SetProperty(s => s.UsageCount, snippet.UsageCount)
                        .SetProperty(s => s.LastUsedAt, snippet.LastUsedAt));
                if (rows == 0)
                    throw new DbUpdateConcurrencyException("Snippet bulunamadı.");

                var favoriteProperty = entry.Property(s => s.IsFavorite);
                favoriteProperty.OriginalValue = favoriteProperty.CurrentValue;
                favoriteProperty.IsModified = false;

                var usageCountProperty = entry.Property(s => s.UsageCount);
                usageCountProperty.OriginalValue = usageCountProperty.CurrentValue;
                usageCountProperty.IsModified = false;

                var lastUsedAtProperty = entry.Property(s => s.LastUsedAt);
                lastUsedAtProperty.OriginalValue = lastUsedAtProperty.CurrentValue;
                lastUsedAtProperty.IsModified = false;
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task AddRangeAsync(IEnumerable<Snippet> snippets)
        {
            try
            {
                await _context.Snippets.AddRangeAsync(snippets);
                await _context.SaveChangesAsync();
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task ReplaceAllAsync(IEnumerable<Snippet> snippets)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var allVersions = await _context.SnippetVersions.ToListAsync();
                if (allVersions.Count > 0)
                    _context.SnippetVersions.RemoveRange(allVersions);

                var all = await _context.Snippets.ToListAsync();
                _context.Snippets.RemoveRange(all);
                await _context.Snippets.AddRangeAsync(snippets);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch
                {
                    // Preserve the original write failure.
                }

                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task DeleteAsync(Snippet snippet)
        {
            try
            {
                var existing = await _context.Snippets.FindAsync(snippet.Id);
                if (existing != null)
                {
                    var versions = await _context.SnippetVersions
                        .Where(v => v.SnippetId == existing.Id)
                        .ToListAsync();
                    if (versions.Count > 0)
                        _context.SnippetVersions.RemoveRange(versions);

                    _context.Snippets.Remove(existing);
                    await _context.SaveChangesAsync();
                }
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task ClearAsync()
        {
            try
            {
                var allVersions = await _context.SnippetVersions.ToListAsync();
                if (allVersions.Count > 0)
                    _context.SnippetVersions.RemoveRange(allVersions);

                var all = await _context.Snippets.ToListAsync();
                _context.Snippets.RemoveRange(all);
                await _context.SaveChangesAsync();
            }
            catch
            {
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public Task<List<Snippet>> ImportAsync(string filePath)
        {
            throw new NotImplementedException("JSON import is handled at ViewModel level now.");
        }

        public Task ExportAsync(string filePath, IEnumerable<Snippet> snippets)
        {
            throw new NotImplementedException("JSON export is handled at ViewModel level now.");
        }
    }
}
