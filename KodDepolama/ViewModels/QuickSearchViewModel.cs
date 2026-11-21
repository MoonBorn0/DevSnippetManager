using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevSnippetManager.Services;
using KodDepolama;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace DevSnippetManager.ViewModels
{
    public sealed class QuickSearchResult
    {
        public QuickSearchResult(Snippet snippet, IReadOnlyList<TextSegment> titleSegments, string subtitle)
        { Snippet = snippet; TitleSegments = titleSegments; Subtitle = subtitle; }
        public Snippet Snippet { get; }
        public IReadOnlyList<TextSegment> TitleSegments { get; }
        public string Subtitle { get; }
    }

    public partial class QuickSearchViewModel : ObservableObject
    {
        private readonly MainViewModel _main;
        public ObservableCollection<QuickSearchResult> Results { get; } = new();
        [ObservableProperty] private string _query = string.Empty;
        [ObservableProperty] private QuickSearchResult? _selectedResult;
        public event EventHandler? CloseRequested;
        public event EventHandler<Snippet>? OpenInMainRequested;

        public QuickSearchViewModel(MainViewModel main) { _main = main; Refresh(); }
        partial void OnQueryChanged(string value) => Refresh();

        public void Reset() { Query = string.Empty; Refresh(); }

        public void MoveSelection(int delta)
        {
            if (Results.Count == 0) return;
            var index = Math.Clamp(SelectedResult == null ? 0 : Results.IndexOf(SelectedResult) + delta, 0, Results.Count - 1);
            SelectedResult = Results[index];
        }

        [RelayCommand]
        private async Task CopySelectedAsync()
        {
            if (SelectedResult == null) return;
            if (await _main.CopySnippetToClipboardAsync(SelectedResult.Snippet)) CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void OpenSelected()
        {
            if (SelectedResult != null) OpenInMainRequested?.Invoke(this, SelectedResult.Snippet);
        }

        private void Refresh()
        {
            var query = _main.AllSnippets.AsEnumerable();
            IEnumerable<(Snippet Snippet, int Score, IReadOnlyList<int> Indices)> scored;
            if (string.IsNullOrWhiteSpace(Query))
                scored = query.OrderByDescending(s => s.IsFavorite).ThenByDescending(s => s.UsageCount).ThenByDescending(s => s.LastUsedAt).Take(20).Select(s => (Snippet: s, Score: 0, Indices: (IReadOnlyList<int>)Array.Empty<int>()));
            else
                scored = query.Select(s => GetScore(s, Query)).Where(x => x.Score.HasValue)
                    .Select(x => (Snippet: x.Snippet, Score: x.Score!.Value, Indices: x.Indices))
                    .OrderByDescending(x => x.Score).ThenByDescending(x => x.Snippet.IsFavorite)
                    .ThenBy(x => x.Snippet.Title, StringComparer.OrdinalIgnoreCase).Take(20);

            Results.Clear();
            foreach (var item in scored)
            {
                var segments = string.IsNullOrWhiteSpace(Query) ? new[] { new TextSegment(item.Snippet.Title, false) } : FuzzyMatcher.ToSegments(item.Snippet.Title, item.Indices);
                var subtitle = LanguageCatalog.Normalize(item.Snippet.Language) + (string.IsNullOrWhiteSpace(item.Snippet.Folder) ? string.Empty : " · " + item.Snippet.Folder) + (item.Snippet.IsFavorite ? " · ★" : string.Empty);
                Results.Add(new QuickSearchResult(item.Snippet, segments, subtitle));
            }
            SelectedResult = Results.FirstOrDefault();
        }

        private static (Snippet Snippet, int? Score, IReadOnlyList<int> Indices) GetScore(Snippet s, string query)
        {
            var title = FuzzyMatcher.Match(query, s.Title ?? string.Empty);
            var tags = FuzzyMatcher.Match(query, s.Tags ?? string.Empty);
            var folder = FuzzyMatcher.Match(query, s.Folder ?? string.Empty);
            var score = Math.Max((title?.Score ?? 0) * 3, Math.Max(tags?.Score ?? 0, folder?.Score ?? 0));
            if (score == 0 && (s.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true || s.Code?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)) score = 10;
            return (s, score == 0 ? null : score, title?.MatchedIndices ?? Array.Empty<int>());
        }
    }
}
