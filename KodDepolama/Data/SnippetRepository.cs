using KodDepolama;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace DevSnippetManager.Data
{
    public class SnippetRepository : ISnippetRepository
    {
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
                ["Folder"] = "TEXT NOT NULL DEFAULT ''"
            };

            foreach (var missing in missingColumns)
            {
                if (columns.Contains(missing.Key))
                    continue;

                await using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE Snippets ADD COLUMN {missing.Key} {missing.Value};";
                await alter.ExecuteNonQueryAsync();
            }
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

        public async Task AddOrUpdateAsync(Snippet snippet)
        {
            try
            {
                var existing = await _context.Snippets.FindAsync(snippet.Id);
                if (existing != null)
                {
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
