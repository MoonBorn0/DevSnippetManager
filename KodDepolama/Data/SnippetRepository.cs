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
