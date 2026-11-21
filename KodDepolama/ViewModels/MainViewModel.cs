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
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private bool _isAutoBackupRunning;
        private List<Snippet> _allSnippets = new();
        private bool _isRevertingSelection;
        private bool _shouldRevertSelection;
        private Snippet? _pendingRevertTarget;
        private bool _suppressUnsavedCheck;
        private bool _suppressEditorReload;
        private Snippet? _editingSnippet;

        public ISnackbarMessageQueue MessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(2));

        [ObservableProperty]
        private ObservableCollection<Snippet> _filteredSnippets = new();

        [ObservableProperty]
        private Snippet? _selectedSnippet;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedLanguageFilter = "Tüm diller";

        [ObservableProperty]
        private ObservableCollection<string> _languages = new(new[] { "Tüm diller", "C#", "Java", "Python", "SQL", "JavaScript", "HTML/CSS", "Diğer" });

        [ObservableProperty]
        private ObservableCollection<string> _editLanguages = new(new[] { "C#", "Java", "Python", "SQL", "JavaScript", "HTML/CSS", "Diğer" });

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
        private bool _isCodeModified;

        // Note: Code is handled via WebView2 directly to avoid WPF bindings lag on large text, 
        // but we'll use an event or action to request/send code to the View.
        public Action<string>? UpdateEditorCodeAction { get; set; }
        public Action<string>? UpdateEditorLanguageAction { get; set; }
        public Func<Task<string>>? GetEditorCodeFunc { get; set; }

        public MainViewModel()
        {
            _settings = AppSettings.Load();
            _repository = new SnippetRepository(_settings);
            _backupService = new BackupService(_settings);
            InitializeAsync();
        }

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
            FilterList();
        }

        partial void OnSearchTextChanged(string value) => FilterList();
        partial void OnSelectedLanguageFilterChanged(string value) => FilterList();

        partial void OnEditLanguageChanged(string value)
        {
            UpdateEditorLanguageAction?.Invoke(value);
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
                        || !string.IsNullOrEmpty(EditDescription);
                }

                var selectedLanguage = string.IsNullOrWhiteSpace(editingSnippet.Language) ? "Diğer" : editingSnippet.Language;
                return !string.Equals(EditTitle, editingSnippet.Title ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(EditTags, editingSnippet.Tags ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(EditLanguage, selectedLanguage, StringComparison.Ordinal)
                    || !string.Equals(EditDescription, editingSnippet.Description ?? string.Empty, StringComparison.Ordinal);
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
                EditTitle = value.Title;
                EditTags = value.Tags;
                EditLanguage = string.IsNullOrWhiteSpace(value.Language) ? "Diğer" : value.Language;
                EditDescription = value.Description;
                IsCodeModified = false;
                UpdateEditorCodeAction?.Invoke(value.Code);
            }
            else
            {
                ClearEditor();
            }
        }

        private void FilterList()
        {
            var editingSnippet = _editingSnippet;
            var query = _allSnippets.AsEnumerable();

            if (SelectedLanguageFilter != "Tüm diller")
                query = query.Where(s => s.Language == SelectedLanguageFilter);

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(s => (s.Title?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                         (s.Tags?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                         (s.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                         (s.Code?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

            var filtered = query.OrderBy(s => s.Language).ThenBy(s => s.Title).ToList();
            
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

            _suppressUnsavedCheck = true;
            try
            {
                if (SelectedSnippet != null)
                {
                    _editingSnippet = null;
                    SelectedSnippet = null;
                }
                else
                {
                    _editingSnippet = null;
                    ClearEditor();
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
            IsCodeModified = false;
            UpdateEditorCodeAction?.Invoke(string.Empty);
        }

        public void MarkCodeChanged() => IsCodeModified = true;

        [RelayCommand]
        private async Task CopySnippetAsync()
        {
            var code = GetEditorCodeFunc != null ? await GetEditorCodeFunc() : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                MessageQueue.Enqueue("Kopyalanacak kod yok");
                return;
            }

            try
            {
                System.Windows.Clipboard.SetText(code);
                MessageQueue.Enqueue("Kod panoya kopyalandı");
            }
            catch (Exception)
            {
                MessageQueue.Enqueue("Panoya kopyalanamadı");
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

            try
            {
            snippet.Title = EditTitle.Trim();
            snippet.Tags = EditTags?.Trim() ?? "";
            snippet.Language = EditLanguage;
            snippet.Description = EditDescription?.Trim() ?? "";
            snippet.Code = code;
            snippet.UpdatedAt = DateTime.Now;

            await _repository.AddOrUpdateAsync(snippet);
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
