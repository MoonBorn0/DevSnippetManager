using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevSnippetManager.Data;
using DevSnippetManager.Services;
using DevSnippetManager.Views;
using KodDepolama;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DevSnippetManager.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AppSettings _settings;
        private readonly ISnippetRepository _repository;
        private readonly BackupService _backupService;
        private DispatcherTimer? _backupTimer;
        private DispatcherTimer? _sidebarSaveTimer;
        private bool _sidebarSettingsDirty;
        private bool _isAutoBackupRunning;
        private List<Snippet> _allSnippets = new();
        private bool _isRevertingSelection;
        private bool _shouldRevertSelection;
        private Snippet? _pendingRevertTarget;
        private bool _suppressUnsavedCheck;
        private bool _suppressEditorReload;
        private Snippet? _editingSnippet;
        private string? _newSnippetPrefilledFolder;
        private readonly CultureInfo _turkish = new("tr-TR");

        public ISnackbarMessageQueue MessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(2));
        public AppSettings Settings => _settings;

        public ObservableCollection<string> ThemeOptions { get; } = new(new[] { "Koyu", "Açık", "Sistem" });

        [ObservableProperty]
        private string _selectedTheme = "Koyu";

        private bool _isLightTheme;
        public bool IsLightTheme
        {
            get => _isLightTheme;
            private set => SetProperty(ref _isLightTheme, value);
        }

        [ObservableProperty]
        private double _sidebarWidth = 340;

        [ObservableProperty]
        private int _editorFontSize = 14;

        [ObservableProperty]
        private bool _editorWordWrap;

        [ObservableProperty]
        private bool _editorMinimap;

        [ObservableProperty]
        private bool _showCodePreview = true;

        [ObservableProperty]
        private bool _isDescriptionPreview;

        public sealed record EditorOptions(string MonacoTheme, int FontSize, bool WordWrap, bool Minimap);
        public Action<EditorOptions>? UpdateEditorOptionsAction { get; set; }
        public EditorOptions CurrentEditorOptions => new(
            ThemeService.MonacoThemeFor(IsLightTheme), EditorFontSize, EditorWordWrap, EditorMinimap);

        public void SaveSettings()
        {
            _settings.Save();
        }

        [ObservableProperty]
        private ObservableCollection<Snippet> _filteredSnippets = new();

        [ObservableProperty]
        private Snippet? _selectedSnippet;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedLanguageFilter = "Tüm diller";

        [ObservableProperty]
        private ObservableCollection<string> _languages = new();

        [ObservableProperty]
        private ObservableCollection<string> _editLanguages = new();

        [ObservableProperty]
        private ObservableCollection<string> _editFolders = new();

        [ObservableProperty]
        private ObservableCollection<string> _folderFilters = new();

        [ObservableProperty]
        private string _selectedFolderFilter = "Tüm klasörler";

        [ObservableProperty]
        private ObservableCollection<TagFilterItem> _tagFilters = new();

        [ObservableProperty]
        private bool _hasTagFilter;

        [ObservableProperty]
        private ObservableCollection<string> _sortOptions = new(new[] { "Ada göre", "Son güncellenen", "En yeni", "En çok kullanılan" });

        [ObservableProperty]
        private string _selectedSortOption = "Ada göre";

        // Editor properties
        [ObservableProperty]
        private string _editTitle = string.Empty;

        [ObservableProperty]
        private string _editTags = string.Empty;

        [ObservableProperty]
        private string _editLanguage = "C#";

        [ObservableProperty]
        private string _editDescription = string.Empty;

        [ObservableProperty]
        private string _editFolder = string.Empty;

        [ObservableProperty]
        private bool _editIsFavorite;

        [ObservableProperty]
        private bool _isCodeModified;

        // Note: Code is handled via WebView2 directly to avoid WPF bindings lag on large text, 
        // but we'll use an event or action to request/send code to the View.
        public Action<string>? UpdateEditorCodeAction { get; set; }
        public Action<string>? UpdateEditorLanguageAction { get; set; }
        public Func<Task<string>>? GetEditorCodeFunc { get; set; }
        public Func<IReadOnlyList<TemplateVariable>, Task<IReadOnlyDictionary<string, string>?>>? RequestTemplateValuesFunc { get; set; }
        public IReadOnlyList<Snippet> AllSnippets => _allSnippets;

        public MainViewModel()
        {
            _settings = AppSettings.Load();
            _selectedTheme = _settings.Theme;
            _sidebarWidth = Math.Clamp(_settings.SidebarWidth, 260, 600);
            _editorFontSize = Math.Clamp(_settings.EditorFontSize, 10, 28);
            _editorWordWrap = _settings.EditorWordWrap;
            _editorMinimap = _settings.EditorMinimap;
            _showCodePreview = _settings.ShowCodePreview;
            IsLightTheme = ThemeService.Apply(_selectedTheme);
            ThemeService.EffectiveThemeChanged += OnEffectiveThemeChanged;
            _repository = new SnippetRepository(_settings);
            _backupService = new BackupService(_settings);
            InitializeAsync();
        }

        private void OnEffectiveThemeChanged(object? sender, bool isLight)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                ApplyEffectiveTheme(isLight);
            else
                dispatcher.BeginInvoke(() => ApplyEffectiveTheme(isLight));
        }

        private void ApplyEffectiveTheme(bool isLight)
        {
            IsLightTheme = isLight;
            UpdateEditorOptionsAction?.Invoke(CurrentEditorOptions);
        }

        partial void OnSelectedThemeChanged(string value)
        {
            _settings.Theme = value;
            _settings.Save();
            IsLightTheme = ThemeService.Apply(value);
        }

        partial void OnSidebarWidthChanged(double value)
        {
            var clamped = Math.Clamp(value, 260, 600);
            if (Math.Abs(value - clamped) > 0.01)
            {
                SidebarWidth = clamped;
                return;
            }

            _settings.SidebarWidth = clamped;
            _sidebarSettingsDirty = true;
            _sidebarSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _sidebarSaveTimer.Stop();
            _sidebarSaveTimer.Tick -= SaveSidebarWidth;
            _sidebarSaveTimer.Tick += SaveSidebarWidth;
            _sidebarSaveTimer.Start();
        }

        private void SaveSidebarWidth(object? sender, EventArgs e)
        {
            _sidebarSaveTimer?.Stop();
            _settings.Save();
            _sidebarSettingsDirty = false;
        }

        public void FlushPendingSettings()
        {
            _sidebarSaveTimer?.Stop();
            if (_sidebarSettingsDirty)
            {
                _settings.Save();
                _sidebarSettingsDirty = false;
            }
        }

        partial void OnEditorFontSizeChanged(int value)
        {
            var clamped = Math.Clamp(value, 10, 28);
            if (value != clamped) { EditorFontSize = clamped; return; }
            _settings.EditorFontSize = clamped;
            _settings.Save();
            UpdateEditorOptionsAction?.Invoke(CurrentEditorOptions);
        }

        partial void OnEditorWordWrapChanged(bool value)
        {
            _settings.EditorWordWrap = value;
            _settings.Save();
            UpdateEditorOptionsAction?.Invoke(CurrentEditorOptions);
        }

        partial void OnEditorMinimapChanged(bool value)
        {
            _settings.EditorMinimap = value;
            _settings.Save();
            UpdateEditorOptionsAction?.Invoke(CurrentEditorOptions);
        }

        partial void OnShowCodePreviewChanged(bool value)
        {
            _settings.ShowCodePreview = value;
            _settings.Save();
        }

        [RelayCommand]
        private void ToggleTheme() => SelectedTheme = IsLightTheme ? "Koyu" : "Açık";

        [RelayCommand]
        private void IncreaseFontSize() => EditorFontSize = Math.Min(28, EditorFontSize + 1);

        [RelayCommand]
        private void DecreaseFontSize() => EditorFontSize = Math.Max(10, EditorFontSize - 1);

        [RelayCommand]
        private void ResetFontSize() => EditorFontSize = 14;

        [RelayCommand]
        private void ToggleDescriptionPreview() => IsDescriptionPreview = !IsDescriptionPreview;

        private async void InitializeAsync()
        {
            try
            {
                await _repository.InitializeAsync();
                await LoadDataAsync();
                StartBackupTimer();
                await RunAutoBackupIfDueAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void StartBackupTimer()
        {
            if (_backupTimer != null)
                return;

            _backupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromHours(1)
            };
            _backupTimer.Tick += async (_, _) => await RunAutoBackupIfDueAsync();
            _backupTimer.Start();
        }

        private async Task RunAutoBackupIfDueAsync()
        {
            if (_isAutoBackupRunning || !_backupService.IsBackupDue())
                return;

            _isAutoBackupRunning = true;
            try
            {
                await _backupService.CreateBackupAsync(_allSnippets.ToList());
            }
            catch (Exception)
            {
                MessageQueue.Enqueue("Otomatik yedek alınamadı");
            }
            finally
            {
                _isAutoBackupRunning = false;
            }
        }

        private async Task LoadDataAsync()
        {
            _allSnippets = (await _repository.GetAllAsync()).ToList();
            RebuildOrganizationLists();
            FilterList();
        }

        partial void OnSearchTextChanged(string value) => FilterList();
        partial void OnSelectedLanguageFilterChanged(string value) => FilterList();
        partial void OnSelectedFolderFilterChanged(string value) => FilterList();
        partial void OnSelectedSortOptionChanged(string value) => FilterList();

        partial void OnEditLanguageChanged(string value)
        {
            UpdateEditorLanguageAction?.Invoke(value);
        }

        private void RebuildOrganizationLists()
        {
            var selectedLanguage = SelectedLanguageFilter;
            var selectedFolder = SelectedFolderFilter;
            var selectedTagNames = TagFilters.Where(t => t.IsSelected).Select(t => t.Name)
                .ToHashSet(StringComparer.Create(_turkish, true));

            var known = new List<string>(LanguageCatalog.BuiltInLanguages);
            var knownSet = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
            foreach (var language in _settings.CustomLanguages ?? new List<string>())
            {
                var normalized = LanguageCatalog.Normalize(language);
                if (knownSet.Add(normalized) && !string.Equals(normalized, "Diğer", StringComparison.OrdinalIgnoreCase))
                    known.Add(normalized);
            }
            foreach (var snippet in _allSnippets)
            {
                var normalized = LanguageCatalog.Normalize(snippet.Language);
                if (knownSet.Add(normalized) && !string.Equals(normalized, "Diğer", StringComparison.OrdinalIgnoreCase))
                    known.Add(normalized);
            }

            var customLanguages = known.Skip(LanguageCatalog.BuiltInLanguages.Count).OrderBy(x => x, StringComparer.Create(_turkish, true)).ToList();
            var languageList = LanguageCatalog.BuiltInLanguages.Where(x => !string.Equals(x, "Diğer", StringComparison.Ordinal)).ToList();
            languageList.AddRange(customLanguages);
            languageList.Add("Diğer");
            Languages = new ObservableCollection<string>(new[] { "Tüm diller" }.Concat(languageList));
            EditLanguages = new ObservableCollection<string>(languageList);
            SelectedLanguageFilter = Languages.Contains(selectedLanguage) ? selectedLanguage : "Tüm diller";

            var folders = _allSnippets.Select(s => s.Folder?.Trim() ?? string.Empty)
                .Where(f => f.Length > 0).Distinct(StringComparer.Ordinal).OrderBy(f => f, StringComparer.Create(_turkish, true)).ToList();
            EditFolders = new ObservableCollection<string>(folders);
            FolderFilters = new ObservableCollection<string>(new[] { "Tüm klasörler", "Klasörsüz" }.Concat(folders));
            SelectedFolderFilter = FolderFilters.Contains(selectedFolder) ? selectedFolder : "Tüm klasörler";

            var tagGroups = _allSnippets.SelectMany(s => GetTags(s.Tags)
                    .Distinct(StringComparer.Create(_turkish, true)))
                .GroupBy(t => t, StringComparer.Create(_turkish, true));
            var newTagFilters = tagGroups.Select(group => new TagFilterItem(
                group.GroupBy(x => x, StringComparer.Create(_turkish, true))
                    .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Create(_turkish, true)).First().Key,
                group.Count()))
                .OrderByDescending(t => t.Count).ThenBy(t => t.Name, StringComparer.Create(_turkish, true)).ToList();
            foreach (var item in newTagFilters)
            {
                item.IsSelected = selectedTagNames.Contains(item.Name);
                item.PropertyChanged += TagFilterItemPropertyChanged;
            }
            TagFilters = new ObservableCollection<TagFilterItem>(newTagFilters);
            HasTagFilter = TagFilters.Any(t => t.IsSelected);
        }

        private static IEnumerable<string> GetTags(string? tags)
        {
            return (tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim()).Where(tag => tag.Length > 0);
        }

        private void TagFilterItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TagFilterItem.IsSelected))
            {
                HasTagFilter = TagFilters.Any(t => t.IsSelected);
                FilterList();
            }
        }

        [RelayCommand]
        private void ClearTagFilters()
        {
            foreach (var tag in TagFilters)
                tag.IsSelected = false;
            HasTagFilter = false;
            FilterList();
        }

        public bool HasUnsavedChanges
        {
            get
            {
                if (IsCodeModified)
                    return true;

                var editingSnippet = _editingSnippet ?? SelectedSnippet;
                if (editingSnippet == null)
                {
                    return !string.IsNullOrEmpty(EditTitle)
                        || !string.IsNullOrEmpty(EditTags)
                        || !string.IsNullOrEmpty(EditDescription)
                        || !string.Equals(EditFolder, _newSnippetPrefilledFolder ?? string.Empty, StringComparison.Ordinal);
                }

                var selectedLanguage = string.IsNullOrWhiteSpace(editingSnippet.Language) ? "Diğer" : editingSnippet.Language;
                return !string.Equals(EditTitle, editingSnippet.Title ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(EditTags, editingSnippet.Tags ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(EditLanguage, selectedLanguage, StringComparison.Ordinal)
                    || !string.Equals(EditDescription, editingSnippet.Description ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(EditFolder, editingSnippet.Folder ?? string.Empty, StringComparison.Ordinal);
            }
        }

        public bool ConfirmDiscardChanges()
        {
            if (!HasUnsavedChanges)
                return true;

            return System.Windows.MessageBox.Show(
                "Kaydedilmemiş değişiklikler var. Kaydetmeden devam edilsin mi?",
                "Uyarı",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        partial void OnSelectedSnippetChanging(Snippet? oldValue, Snippet? newValue)
        {
            if (_isRevertingSelection || _suppressUnsavedCheck || _suppressEditorReload)
                return;

            if (!ConfirmDiscardChanges())
            {
                _pendingRevertTarget = oldValue;
                _shouldRevertSelection = true;
            }
        }

        partial void OnSelectedSnippetChanged(Snippet? value)
        {
            if (_isRevertingSelection || _suppressEditorReload)
                return;

            if (_shouldRevertSelection)
            {
                _shouldRevertSelection = false;
                var target = _pendingRevertTarget;
                _pendingRevertTarget = null;
                void RevertSelection()
                {
                    try
                    {
                        SelectedSnippet = target;
                    }
                    finally
                    {
                        _isRevertingSelection = false;
                    }
                }

                _isRevertingSelection = true;
                try
                {
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher != null)
                        dispatcher.BeginInvoke(RevertSelection);
                    else
                        RevertSelection();
                }
                catch
                {
                    _isRevertingSelection = false;
                    throw;
                }
                return;
            }

            if (value != null)
            {
                _editingSnippet = value;
                _newSnippetPrefilledFolder = null;
                EditTitle = value.Title;
                EditTags = value.Tags;
                EditLanguage = string.IsNullOrWhiteSpace(value.Language) ? "Diğer" : value.Language;
                EditDescription = value.Description;
                IsDescriptionPreview = !string.IsNullOrWhiteSpace(value.Description);
                EditFolder = value.Folder ?? string.Empty;
                EditIsFavorite = value.IsFavorite;
                IsCodeModified = false;
                UpdateEditorCodeAction?.Invoke(value.Code);
            }
            else
            {
                _newSnippetPrefilledFolder = null;
                ClearEditor();
            }
        }

        private void FilterList()
        {
            var editingSnippet = _editingSnippet;
            var query = _allSnippets.AsEnumerable();

            if (SelectedLanguageFilter != "Tüm diller")
                query = query.Where(s => string.Equals(
                    LanguageCatalog.Normalize(s.Language),
                    SelectedLanguageFilter,
                    StringComparison.OrdinalIgnoreCase));

            if (SelectedFolderFilter == "Klasörsüz")
                query = query.Where(s => string.IsNullOrWhiteSpace(s.Folder));
            else if (SelectedFolderFilter != "Tüm klasörler")
                query = query.Where(s => string.Equals(s.Folder, SelectedFolderFilter, StringComparison.Ordinal));

            var selectedTags = TagFilters.Where(t => t.IsSelected).Select(t => t.Name).ToList();
            if (selectedTags.Count > 0)
                query = query.Where(s => selectedTags.All(selected => GetTags(s.Tags)
                    .Distinct(StringComparer.Create(_turkish, true))
                    .Any(tag => StringComparer.Create(_turkish, true).Equals(selected, tag))));

            var inMemoryQuery = query.ToList();
            var isSearching = !string.IsNullOrWhiteSpace(SearchText);
            IEnumerable<Snippet> ordered;
            if (isSearching)
            {
                var scored = inMemoryQuery
                    .Select(s => (Snippet: s, Score: GetSearchScore(s, SearchText)))
                    .Where(x => x.Score.HasValue)
                    .ToList();
                ordered = scored
                    .OrderByDescending(x => x.Score!.Value)
                    .ThenByDescending(x => x.Snippet.IsFavorite)
                    .ThenBy(x => x.Snippet.Title, StringComparer.Create(_turkish, true))
                    .Select(x => x.Snippet);
            }
            else
            {
                var favoriteOrdered = inMemoryQuery.OrderByDescending(s => s.IsFavorite);
                ordered = SelectedSortOption switch
                {
                    "Son güncellenen" => favoriteOrdered.ThenByDescending(s => s.UpdatedAt),
                    "En yeni" => favoriteOrdered.ThenByDescending(s => s.CreatedAt),
                    "En çok kullanılan" => favoriteOrdered.ThenByDescending(s => s.UsageCount).ThenByDescending(s => s.LastUsedAt).ThenBy(s => s.Title, StringComparer.Create(_turkish, true)),
                    _ => favoriteOrdered.ThenBy(s => s.Title, StringComparer.Create(_turkish, true))
                };
            }
            var filtered = ordered.ToList();
            
            var previousSuppressEditorReload = _suppressEditorReload;
            _suppressEditorReload = true;
            try
            {
                FilteredSnippets.Clear();
                foreach (var s in filtered)
                    FilteredSnippets.Add(s);

                SelectedSnippet = editingSnippet != null && filtered.Any(s => s.Id == editingSnippet.Id)
                    ? filtered.First(s => s.Id == editingSnippet.Id)
                    : null;
            }
            finally
            {
                _suppressEditorReload = previousSuppressEditorReload;
            }
        }

        [RelayCommand]
        private void NewSnippet()
        {
            if (!ConfirmDiscardChanges())
                return;

            BeginNewSnippet();
        }

        private void BeginNewSnippet()
        {

            _suppressUnsavedCheck = true;
            try
            {
                if (SelectedSnippet != null)
                {
                    _editingSnippet = null;
                    SelectedSnippet = null;
                    ClearEditor();
                }
                else
                {
                    _editingSnippet = null;
                    ClearEditor();
                }
                _newSnippetPrefilledFolder = null;
                if (SelectedFolderFilter != "Tüm klasörler" && SelectedFolderFilter != "Klasörsüz")
                {
                    EditFolder = SelectedFolderFilter;
                    _newSnippetPrefilledFolder = EditFolder;
                }
            }
            finally
            {
                _suppressUnsavedCheck = false;
            }

            
        }

        private void ClearEditor()
        {
            EditTitle = string.Empty;
            EditTags = string.Empty;
            EditLanguage = "C#";
            EditDescription = string.Empty;
            IsDescriptionPreview = false;
            EditFolder = string.Empty;
            EditIsFavorite = false;
            IsCodeModified = false;
            UpdateEditorCodeAction?.Invoke(string.Empty);
        }

        public void MarkCodeChanged() => IsCodeModified = true;

        private static int? GetSearchScore(Snippet snippet, string searchText)
        {
            var title = FuzzyMatcher.Match(searchText, snippet.Title ?? string.Empty);
            var tags = FuzzyMatcher.Match(searchText, snippet.Tags ?? string.Empty);
            var folder = FuzzyMatcher.Match(searchText, snippet.Folder ?? string.Empty);
            var fieldScore = Math.Max(tags?.Score ?? 0, folder?.Score ?? 0);
            var fuzzyScore = Math.Max((title?.Score ?? 0) * 3, fieldScore);
            if (fuzzyScore > 0) return fuzzyScore;
            return (snippet.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true ||
                    snippet.Code?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true) ? 10 : null;
        }

        public bool TrySelectSnippet(Snippet snippet)
        {
            var target = _allSnippets.FirstOrDefault(s => ReferenceEquals(s, snippet) || (snippet.Id != 0 && s.Id == snippet.Id));
            if (target == null) return false;

            var isCurrentSnippet = ReferenceEquals(target, SelectedSnippet) || ReferenceEquals(target, _editingSnippet);
            if (isCurrentSnippet)
            {
                if (!HasUnsavedChanges)
                    return true;
                if (!ConfirmDiscardChanges())
                    return false;

                _suppressEditorReload = true;
                try
                {
                    _editingSnippet = target;
                    EditTitle = target.Title;
                    EditTags = target.Tags;
                    EditLanguage = string.IsNullOrWhiteSpace(target.Language) ? "DiÄŸer" : target.Language;
                    EditDescription = target.Description;
                    IsDescriptionPreview = !string.IsNullOrWhiteSpace(target.Description);
                    if (string.IsNullOrWhiteSpace(target.Language))
                        EditLanguage = "\u0044i\u011Fer";
                    EditFolder = target.Folder ?? string.Empty;
                    EditIsFavorite = target.IsFavorite;
                    IsCodeModified = false;
                    UpdateEditorCodeAction?.Invoke(target.Code);
                }
                finally
                {
                    _suppressEditorReload = false;
                }
                return true;
            }

            if (!ConfirmDiscardChanges()) return false;
            SearchText = string.Empty;
            SelectedLanguageFilter = "Tüm diller";
            SelectedFolderFilter = "Tüm klasörler";
            foreach (var tag in TagFilters) tag.IsSelected = false;
            HasTagFilter = false;
            FilterList();
            _suppressUnsavedCheck = true;
            try { SelectedSnippet = target; }
            finally { _suppressUnsavedCheck = false; }
            return ReferenceEquals(SelectedSnippet, target);
        }

        [RelayCommand]
        private async Task CopySnippetAsync()
        {
            var code = GetEditorCodeFunc != null ? await GetEditorCodeFunc() : string.Empty;
            var target = _editingSnippet ?? new Snippet { Code = code };
            await CopySnippetToClipboardAsync(target, code);
        }

        public async Task<bool> CopySnippetToClipboardAsync(Snippet snippet, string? codeOverride = null)
        {
            var code = codeOverride ?? snippet.Code;
            if (string.IsNullOrEmpty(code))
            {
                MessageQueue.Enqueue("Kopyalanacak kod yok");
                return false;
            }

            var variables = TemplateService.Parse(code);
            if (variables.Count > 0)
            {
                var values = RequestTemplateValuesFunc == null
                    ? variables.ToDictionary(v => v.Name, v => v.DefaultValue, StringComparer.Ordinal)
                    : await RequestTemplateValuesFunc(variables);
                if (values == null)
                {
                    MessageQueue.Enqueue("Kopyalama iptal edildi");
                    return false;
                }
                code = TemplateService.Fill(code, values);
            }

            try
            {
                System.Windows.Clipboard.SetText(code);
                var usedSnippet = snippet;
                if (usedSnippet != null && usedSnippet.Id != 0)
                {
                    var originalUsageCount = usedSnippet.UsageCount;
                    var originalLastUsedAt = usedSnippet.LastUsedAt;
                    usedSnippet.UsageCount++;
                    usedSnippet.LastUsedAt = DateTime.Now;
                    try
                    {
                        await _repository.UpdateMetadataAsync(usedSnippet);
                        if (SelectedSortOption == "En çok kullanılan")
                            FilterList();
                    }
                    catch
                    {
                        usedSnippet.UsageCount = originalUsageCount;
                        usedSnippet.LastUsedAt = originalLastUsedAt;
                        MessageQueue.Enqueue("Kullanım bilgisi güncellenemedi");
                    }
                }
                MessageQueue.Enqueue("Kod panoya kopyalandı");
                return true;
            }
            catch (Exception)
            {
                MessageQueue.Enqueue("Panoya kopyalanamadı");
                return false;
            }
        }

        [RelayCommand]
        private async Task NewFromClipboardAsync()
        {
            string text;
            try { text = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : string.Empty; }
            catch { text = string.Empty; }
            if (string.IsNullOrWhiteSpace(text)) { MessageQueue.Enqueue("Panoda metin yok"); return; }
            if (!ConfirmDiscardChanges()) return;

            BeginNewSnippet();
            UpdateEditorCodeAction?.Invoke(text);
            EditLanguage = LanguageDetector.Detect(text);
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            string CleanTitleLine(string line)
            {
                var result = line.Trim();
                while (result.Length > 0)
                {
                    var before = result;
                    result = System.Text.RegularExpressions.Regex.Replace(result, @"(\*/|-->)\s*$", string.Empty).Trim();
                    if (result.StartsWith("///", StringComparison.Ordinal)) result = result[3..].Trim();
                    else if (result.StartsWith("//", StringComparison.Ordinal)) result = result[2..].Trim();
                    else if (result.StartsWith("/**", StringComparison.Ordinal)) result = result[3..].Trim();
                    else if (result.StartsWith("/*", StringComparison.Ordinal)) result = result[2..].Trim();
                    else if (result.StartsWith("*", StringComparison.Ordinal)) result = result[1..].Trim();
                    else if (result.StartsWith("#!", StringComparison.Ordinal)) result = result[2..].Trim();
                    else if (result.StartsWith("#", StringComparison.Ordinal) && !result.StartsWith("#include", StringComparison.OrdinalIgnoreCase) && !result.StartsWith("#define", StringComparison.OrdinalIgnoreCase) && !result.StartsWith("#region", StringComparison.OrdinalIgnoreCase)) result = result[1..].Trim();
                    else if (result.StartsWith("--", StringComparison.Ordinal)) result = result[2..].Trim();
                    else if (result.StartsWith("<!--", StringComparison.Ordinal)) result = result[4..].Trim();
                    else if (result.StartsWith(";", StringComparison.Ordinal) || result.StartsWith("'", StringComparison.Ordinal)) result = result[1..].Trim();
                    if (result == before) break;
                }
                return result.Trim();
            }

            var firstLine = string.Empty;
            foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                firstLine = CleanTitleLine(line);
                if (firstLine.Length > 0) break;
            }
            if (firstLine.Length == 0) firstLine = "Yeni snippet";
            EditTitle = firstLine.Length > 60 ? firstLine[..59] + "…" : firstLine;
            IsCodeModified = true;
            if (firstLine.Length > 60)
                EditTitle = firstLine[..59] + "\u2026";
            MessageQueue.Enqueue($"Panodan yeni snippet oluşturuldu (dil: {EditLanguage})");
        }

        [RelayCommand]
        private async Task ToggleFavoriteAsync(Snippet? snippet)
        {
            var target = snippet ?? _editingSnippet;
            if (target == null)
                return;
            if (target.Id == 0)
            {
                MessageQueue.Enqueue("Önce snippet'i kaydedin");
                return;
            }

            var original = target.IsFavorite;
            target.IsFavorite = !original;
            try
            {
                await _repository.UpdateMetadataAsync(target);
                if (ReferenceEquals(target, _editingSnippet))
                    EditIsFavorite = target.IsFavorite;
                FilterList();
            }
            catch
            {
                target.IsFavorite = original;
                if (ReferenceEquals(target, _editingSnippet))
                    EditIsFavorite = original;
                MessageQueue.Enqueue("Favori güncellenemedi");
            }
        }

        [RelayCommand]
        private async Task SaveSnippetAsync()
        {
            if (string.IsNullOrWhiteSpace(EditTitle))
            {
                System.Windows.MessageBox.Show("Başlık boş olamaz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var code = GetEditorCodeFunc != null ? await GetEditorCodeFunc() : string.Empty;

            var originalEditingSnippet = _editingSnippet;
            var existing = _editingSnippet ?? SelectedSnippet;
            var snippet = existing ?? new Snippet { CreatedAt = DateTime.Now };
            var originalTitle = existing?.Title;
            var originalTags = existing?.Tags;
            var originalLanguage = existing?.Language;
            var originalDescription = existing?.Description;
            var originalCode = existing?.Code;
            var originalUpdatedAt = existing?.UpdatedAt;
            var originalFolder = existing?.Folder;
            var normalizedLanguage = LanguageCatalog.Normalize(EditLanguage);
            var knownLanguage = LanguageCatalog.BuiltInLanguages.Any(x => string.Equals(x, normalizedLanguage, StringComparison.OrdinalIgnoreCase))
                || (_settings.CustomLanguages ?? new List<string>()).Any(x => string.Equals(x, normalizedLanguage, StringComparison.OrdinalIgnoreCase))
                || _allSnippets.Any(x => string.Equals(x.Language, normalizedLanguage, StringComparison.OrdinalIgnoreCase));

            try
            {
            snippet.Title = EditTitle.Trim();
            snippet.Tags = EditTags?.Trim() ?? "";
            snippet.Language = normalizedLanguage;
            snippet.Description = EditDescription?.Trim() ?? "";
            snippet.Folder = EditFolder?.Trim() ?? string.Empty;
            snippet.Code = code;
            snippet.UpdatedAt = DateTime.Now;

            await _repository.AddOrUpdateAsync(snippet);

            if (!knownLanguage)
            {
                _settings.CustomLanguages ??= new List<string>();
                if (!_settings.CustomLanguages.Any(x => string.Equals(x, normalizedLanguage, StringComparison.OrdinalIgnoreCase)))
                    _settings.CustomLanguages.Add(normalizedLanguage);
                _settings.Save();
            }

            _editingSnippet = snippet;
            var previousSuppressUnsavedCheck = _suppressUnsavedCheck;
            _suppressUnsavedCheck = true;
            try
            {
                await LoadDataAsync();
                var previousSuppressEditorReload = _suppressEditorReload;
                _suppressEditorReload = true;
                try
                {
                SelectedSnippet = FilteredSnippets.FirstOrDefault(s => s.Id == snippet.Id);
                }
                finally
                {
                    _suppressEditorReload = previousSuppressEditorReload;
                }
            }
            finally
            {
                _suppressUnsavedCheck = previousSuppressUnsavedCheck;
            }

            IsCodeModified = false;
            MessageQueue.Enqueue("Başarıyla kaydedildi.");
            }

            catch (Exception ex)
            {
                if (existing != null)
                {
                    existing.Title = originalTitle;
                    existing.Tags = originalTags;
                    existing.Language = originalLanguage;
                    existing.Description = originalDescription;
                    existing.Code = originalCode;
                    existing.UpdatedAt = originalUpdatedAt!.Value;
                    existing.Folder = originalFolder ?? string.Empty;
                }

                _editingSnippet = originalEditingSnippet;
                System.Windows.MessageBox.Show($"Kaydetme başarısız: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task DeleteSnippetAsync()
        {
            if (SelectedSnippet == null) return;

            if (System.Windows.MessageBox.Show($"'{SelectedSnippet.Title}' silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var previousSuppressUnsavedCheck = _suppressUnsavedCheck;
                _suppressUnsavedCheck = true;
                try
                {
                    await _repository.DeleteAsync(SelectedSnippet);
                    _editingSnippet = null;
                    await LoadDataAsync();
                    SelectedSnippet = FilteredSnippets.FirstOrDefault();
                    if (SelectedSnippet == null)
                        ClearEditor();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Silme başarısız: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    _suppressUnsavedCheck = previousSuppressUnsavedCheck;
                }
            }
        }

        [RelayCommand]
        private async Task ImportAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON dosyası (*.json)|*.json" };
            if (dialog.ShowDialog() != true)
                return;

            List<Snippet>? imported;
            try
            {
                var content = await File.ReadAllTextAsync(dialog.FileName);
                imported = JsonSerializer.Deserialize<List<Snippet>>(content);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Dosya okunamadı: {ex.Message}",
                    "Uyarı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (imported == null || imported.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Dosyada snippet bulunamadı.",
                    "Uyarı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var optionsWindow = new ImportOptionsWindow(imported.Count);
            if (System.Windows.Application.Current?.MainWindow is Window owner && owner.IsLoaded)
            {
                optionsWindow.Owner = owner;
            }

            if (optionsWindow.ShowDialog() != true)
                return;

            var mode = optionsWindow.SelectedMode;
            var plan = ImportMerger.Plan(_allSnippets, imported, mode);
            var openSnippetId = _editingSnippet?.Id ?? SelectedSnippet?.Id;
            var hadUnsavedChanges = HasUnsavedChanges;
            var previousSuppressUnsavedCheck = _suppressUnsavedCheck;
            var importSucceeded = false;
            Snippet? updatedOpenSnippet = null;
            var preserveOpenChanges = false;
            _suppressUnsavedCheck = true;

            try
            {
            if (mode == ImportMode.ReplaceAll)
            {
                try
                {
                    await _backupService.CreateBackupAsync(_allSnippets.ToList());
                }
                catch (Exception ex)
                {
                    var proceed = System.Windows.MessageBox.Show(
                        $"Otomatik yedek alınamadı: {ex.Message}\nYedek olmadan devam edilsin mi?",
                        "Uyarı",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (proceed != MessageBoxResult.Yes)
                        return;
                }

                await _repository.ReplaceAllAsync(plan.ToAdd);
                _suppressUnsavedCheck = true;
                try
                {
                    _editingSnippet = null;
                    SelectedSnippet = null;
                    ClearEditor();
                }
                finally
                {
                    _suppressUnsavedCheck = false;
                }
            }

            else
            {
                foreach (var updated in plan.ToUpdate)
                    await _repository.AddOrUpdateAsync(updated);

                if (plan.ToAdd.Count > 0)
                    await _repository.AddRangeAsync(plan.ToAdd);
            }

            _suppressUnsavedCheck = true;
            try
            {
                await LoadDataAsync();
            }
            finally
            {
                _suppressUnsavedCheck = false;
            }

            updatedOpenSnippet = openSnippetId.HasValue
                ? plan.ToUpdate.FirstOrDefault(s => s.Id == openSnippetId.Value)
                : null;
            preserveOpenChanges = updatedOpenSnippet != null && hadUnsavedChanges;
            importSucceeded = true;
            MessageQueue.Enqueue($"İçe aktarıldı: {plan.ToAdd.Count} eklendi, {plan.ToUpdate.Count} güncellendi, {plan.Skipped.Count} atlandı" + (preserveOpenChanges ? " (açık snippet'teki değişiklikleriniz korundu)" : string.Empty));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"İçe aktarma başarısız: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (importSucceeded && updatedOpenSnippet != null && !hadUnsavedChanges)
                {
                    var refreshed = _allSnippets.FirstOrDefault(s => s.Id == openSnippetId.Value);
                    if (refreshed != null)
                    {
                        _editingSnippet = refreshed;
                        SelectedSnippet = refreshed;
                        EditTitle = refreshed.Title;
                        EditTags = refreshed.Tags;
                        EditLanguage = string.IsNullOrWhiteSpace(refreshed.Language) ? "Diğer" : refreshed.Language;
                        EditDescription = refreshed.Description;
                        IsDescriptionPreview = !string.IsNullOrWhiteSpace(refreshed.Description);
                        EditFolder = refreshed.Folder ?? string.Empty;
                        EditIsFavorite = refreshed.IsFavorite;
                        IsCodeModified = false;
                        UpdateEditorCodeAction?.Invoke(refreshed.Code);
                    }
                }
                _suppressUnsavedCheck = previousSuppressUnsavedCheck;
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON dosyası (*.json)|*.json", FileName = "snippets.json" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                var json = JsonSerializer.Serialize(_allSnippets, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(dialog.FileName, json);
                MessageQueue.Enqueue("Dışa aktarıldı.");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Dışa aktarma başarısız: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task BackupNowAsync()
        {
            try
            {
                var path = await _backupService.CreateBackupAsync(_allSnippets.ToList());
                MessageQueue.Enqueue($"Yedek alındı: {Path.GetFileName(path)}");
            }
            catch (Exception)
            {
                MessageQueue.Enqueue("Yedek alınamadı");
            }
        }

        [RelayCommand]
        private void OpenBackupFolder()
        {
            try
            {
                Directory.CreateDirectory(_settings.BackupFolder);
                Process.Start(new ProcessStartInfo("explorer.exe", _settings.BackupFolder) { UseShellExecute = true });
            }
            catch (Exception)
            {
                MessageQueue.Enqueue("Yedek klasörü açılamadı");
            }
        }
    }
}
