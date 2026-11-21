using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DevSnippetManager.Services.ProjectScan;
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
        Task<IReadOnlyList<SnippetVersion>> GetVersionsAsync(int snippetId) =>
            Task.FromResult<IReadOnlyList<SnippetVersion>>(Array.Empty<SnippetVersion>());
        Task<ScanApplyResult> ApplyScanAsync(ScanPlan plan) =>
            Task.FromResult(new ScanApplyResult(0, 0, 0, 0, 0, 0));
        Task<int> RelocateSourceRootAsync(string oldRoot, string newRoot) =>
            Task.FromResult(0);
        Task<List<Snippet>> ImportAsync(string filePath);
        Task ExportAsync(string filePath, IEnumerable<Snippet> snippets);
    }
}
