using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using KodDepolama;

namespace DevSnippetManager
{
    public interface ISnippetRepository
    {
        Task InitializeAsync();
        Task<IEnumerable<Snippet>> GetAllAsync();
        Task AddOrUpdateAsync(Snippet snippet);
        Task UpdateMetadataAsync(Snippet snippet);
        Task AddRangeAsync(IEnumerable<Snippet> snippets);
        Task ReplaceAllAsync(IEnumerable<Snippet> snippets);
        Task DeleteAsync(Snippet snippet);
        Task ClearAsync();
        Task<List<Snippet>> ImportAsync(string filePath);
        Task ExportAsync(string filePath, IEnumerable<Snippet> snippets);
    }
}
