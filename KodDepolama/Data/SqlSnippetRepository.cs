using KodDepolama;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevSnippetManager.Data
{
    public class SqlSnippetRepository : ISnippetRepository
    {
        private readonly AppDbContext _context;

        public SqlSnippetRepository()
        {
            _context = new AppDbContext();
        }

        public async Task InitializeAsync()
        {
            await _context.Database.EnsureCreatedAsync();

            if (!await _context.Snippets.AnyAsync())
            {
                await SeedDatabaseAsync();
            }
        }

        private async Task SeedDatabaseAsync()
        {
            var samples = SnippetSeedData.GetProfessionalSnippets();
            await _context.Snippets.AddRangeAsync(samples);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Snippet>> GetAllAsync()
        {
            return await _context.Snippets.OrderBy(s => s.Language).ThenBy(s => s.Title).ToListAsync();
        }

        public async Task AddOrUpdateAsync(Snippet snippet)
        {
            var existing = await _context.Snippets.FindAsync(snippet.Id);
            if (existing != null)
            {
                _context.Entry(existing).CurrentValues.SetValues(snippet);
            }
            else
            {
                await _context.Snippets.AddAsync(snippet);
            }
            await _context.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<Snippet> snippets)
        {
            await _context.Snippets.AddRangeAsync(snippets);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Snippet snippet)
        {
            var existing = await _context.Snippets.FindAsync(snippet.Id);
            if (existing != null)
            {
                _context.Snippets.Remove(existing);
                await _context.SaveChangesAsync();
            }
        }

        public async Task ClearAsync()
        {
            var all = await _context.Snippets.ToListAsync();
            _context.Snippets.RemoveRange(all);
            await _context.SaveChangesAsync();
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
