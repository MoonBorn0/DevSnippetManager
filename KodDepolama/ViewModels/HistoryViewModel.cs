using CommunityToolkit.Mvvm.ComponentModel;
using DevSnippetManager.Services;
using KodDepolama;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DevSnippetManager.ViewModels
{
    public sealed class HistoryItem
    {
        public HistoryItem(SnippetVersion version, string summary)
        {
            Version = version;
            SavedAtDisplay = version.SavedAt.ToString("dd.MM.yyyy HH:mm");
            Summary = summary;
        }

        public SnippetVersion Version { get; }
        public string SavedAtDisplay { get; }
        public string Summary { get; }
    }

    public partial class HistoryViewModel : ObservableObject
    {
        private readonly ISnippetRepository _repository;
        private readonly Snippet _currentSnippet;
        private readonly string _currentCode;
        private int _diffRequestId;

        public ObservableCollection<HistoryItem> Versions { get; } = new();
        public ObservableCollection<DiffLine> DiffLines { get; } = new();

        [ObservableProperty]
        private HistoryItem? _selectedVersion;

        [ObservableProperty]
        private bool _onlyChanges;

        [ObservableProperty]
        private bool _isLoading;

        public bool HasVersions => Versions.Count > 0;
        public bool HasNoVersions => !IsLoading && Versions.Count == 0;
        public string SelectedTitle => SelectedVersion?.Version.Title ?? string.Empty;
        public string SelectedMeta
        {
            get
            {
                if (SelectedVersion == null) return string.Empty;
                var v = SelectedVersion.Version;
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(v.Language))
                    parts.Add(v.Language);
                if (!string.IsNullOrWhiteSpace(v.Folder))
                    parts.Add(v.Folder);
                parts.Add(SelectedVersion.SavedAtDisplay);
                return string.Join(" · ", parts);
            }
        }

        public HistoryViewModel(ISnippetRepository repository, Snippet currentSnippet, string currentCode)
        {
            _repository = repository;
            _currentSnippet = currentSnippet;
            _currentCode = currentCode ?? string.Empty;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            OnPropertyChanged(nameof(HasNoVersions));
            try
            {
                var versions = await _repository.GetVersionsAsync(_currentSnippet.Id);

                var items = await Task.Run(() =>
                {
                    var list = new List<HistoryItem>(versions.Count);
                    for (var i = 0; i < versions.Count; i++)
                    {
                        var version = versions[i];
                        string summary;
                        if (i == 0)
                        {
                            summary = BuildSummary(
                                version,
                                _currentSnippet.Title,
                                _currentSnippet.Language,
                                _currentSnippet.Tags,
                                _currentSnippet.Description,
                                _currentSnippet.Folder,
                                _currentCode);
                        }
                        else
                        {
                            var newer = versions[i - 1];
                            summary = BuildSummary(
                                version,
                                newer.Title,
                                newer.Language,
                                newer.Tags,
                                newer.Description,
                                newer.Folder,
                                newer.Code);
                        }

                        list.Add(new HistoryItem(version, summary));
                    }
                    return list;
                });

                Versions.Clear();
                foreach (var item in items)
                    Versions.Add(item);

                OnPropertyChanged(nameof(HasVersions));
                SelectedVersion = Versions.FirstOrDefault();
                await RefreshDiffAsync();
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(HasNoVersions));
            }
        }

        partial void OnSelectedVersionChanged(HistoryItem? value)
        {
            OnPropertyChanged(nameof(SelectedTitle));
            OnPropertyChanged(nameof(SelectedMeta));
            _ = RefreshDiffAsync();
        }

        partial void OnOnlyChangesChanged(bool value)
        {
            _ = RefreshDiffAsync();
        }

        private async Task RefreshDiffAsync()
        {
            var requestId = Interlocked.Increment(ref _diffRequestId);
            var selected = SelectedVersion;
            var onlyChanges = OnlyChanges;

            if (selected == null)
            {
                DiffLines.Clear();
                return;
            }

            IsLoading = true;
            try
            {
                var versionCode = selected.Version.Code;
                var displayLines = await Task.Run(() =>
                {
                    var allLines = LineDiff.Compute(versionCode, _currentCode);
                    return onlyChanges ? LineDiff.FilterContext(allLines, 2) : allLines;
                });

                if (requestId != _diffRequestId)
                    return;

                DiffLines.Clear();
                foreach (var line in displayLines)
                    DiffLines.Add(line);
            }
            finally
            {
                if (requestId == _diffRequestId)
                {
                    IsLoading = false;
                    OnPropertyChanged(nameof(HasNoVersions));
                }
            }
        }

        private static string BuildSummary(
            SnippetVersion older,
            string? newerTitle,
            string? newerLanguage,
            string? newerTags,
            string? newerDescription,
            string? newerFolder,
            string? newerCode)
        {
            var parts = new List<string>();

            var diff = LineDiff.Compute(older.Code, newerCode);
            var added = diff.Count(d => d.Kind == DiffLineKind.Added);
            var removed = diff.Count(d => d.Kind == DiffLineKind.Removed);
            if (added > 0 || removed > 0)
                parts.Add($"Kod: +{added} / −{removed} satır");

            if (!string.Equals(older.Title ?? string.Empty, newerTitle ?? string.Empty, StringComparison.Ordinal))
                parts.Add("Başlık değişti");
            if (!string.Equals(older.Language ?? string.Empty, newerLanguage ?? string.Empty, StringComparison.Ordinal))
                parts.Add("Dil değişti");
            if (!string.Equals(older.Tags ?? string.Empty, newerTags ?? string.Empty, StringComparison.Ordinal))
                parts.Add("Etiketler değişti");
            if (!string.Equals(older.Description ?? string.Empty, newerDescription ?? string.Empty, StringComparison.Ordinal))
                parts.Add("Açıklama değişti");
            if (!string.Equals(older.Folder ?? string.Empty, newerFolder ?? string.Empty, StringComparison.Ordinal))
                parts.Add("Klasör değişti");

            return parts.Count > 0 ? string.Join(", ", parts) : "Değişiklik yok";
        }
    }
}
