using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevSnippetManager.Data;
using KodDepolama;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace DevSnippetManager.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISnippetRepository _repository;
        private List<Snippet> _allSnippets = new();

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

        // Note: Code is handled via WebView2 directly to avoid WPF bindings lag on large text, 
        // but we'll use an event or action to request/send code to the View.
        public Action<string>? UpdateEditorCodeAction { get; set; }
        public Func<Task<string>>? GetEditorCodeFunc { get; set; }

        public MainViewModel()
        {
            _repository = new SqlSnippetRepository();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            await _repository.InitializeAsync();
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            _allSnippets = (await _repository.GetAllAsync()).ToList();
            FilterList();
        }

        partial void OnSearchTextChanged(string value) => FilterList();
        partial void OnSelectedLanguageFilterChanged(string value) => FilterList();

        partial void OnSelectedSnippetChanged(Snippet? value)
        {
            if (value != null)
            {
                EditTitle = value.Title;
                EditTags = value.Tags;
                EditLanguage = string.IsNullOrWhiteSpace(value.Language) ? "Diğer" : value.Language;
                EditDescription = value.Description;
                UpdateEditorCodeAction?.Invoke(value.Code);
            }
            else
            {
                ClearEditor();
            }
        }

        private void FilterList()
        {
            var query = _allSnippets.AsEnumerable();

            if (SelectedLanguageFilter != "Tüm diller")
                query = query.Where(s => s.Language == SelectedLanguageFilter);

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(s => s.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                         s.Tags.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                         s.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            var filtered = query.OrderBy(s => s.Language).ThenBy(s => s.Title).ToList();
            
            FilteredSnippets.Clear();
            foreach (var s in filtered)
                FilteredSnippets.Add(s);
        }

        [RelayCommand]
        private void NewSnippet()
        {
            SelectedSnippet = null;
        }

        private void ClearEditor()
        {
            EditTitle = string.Empty;
            EditTags = string.Empty;
            EditLanguage = "C#";
            EditDescription = string.Empty;
            UpdateEditorCodeAction?.Invoke(string.Empty);
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

            var existing = SelectedSnippet ?? new Snippet { CreatedAt = DateTime.Now };
            existing.Title = EditTitle.Trim();
            existing.Tags = EditTags?.Trim() ?? "";
            existing.Language = EditLanguage;
            existing.Description = EditDescription?.Trim() ?? "";
            existing.Code = code;
            existing.UpdatedAt = DateTime.Now;

            await _repository.AddOrUpdateAsync(existing);
            await LoadDataAsync();
            
            SelectedSnippet = FilteredSnippets.FirstOrDefault(s => s.Id == existing.Id);
            System.Windows.MessageBox.Show("Başarıyla kaydedildi.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task DeleteSnippetAsync()
        {
            if (SelectedSnippet == null) return;

            if (System.Windows.MessageBox.Show($"'{SelectedSnippet.Title}' silinsin mi?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _repository.DeleteAsync(SelectedSnippet);
                await LoadDataAsync();
                SelectedSnippet = FilteredSnippets.FirstOrDefault();
            }
        }

        [RelayCommand]
        private async Task ImportAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "JSON dosyası (*.json)|*.json" };
            if (dialog.ShowDialog() == true)
            {
                var content = await File.ReadAllTextAsync(dialog.FileName);
                var imported = JsonSerializer.Deserialize<List<Snippet>>(content);
                if (imported != null)
                {
                    if (System.Windows.MessageBox.Show("Mevcutlar silinsin mi?", "İçe Aktar", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        await _repository.ClearAsync();
                    }
                    // Reset IDs so EF Core generates new ones
                    imported.ForEach(s => s.Id = 0);
                    await _repository.AddRangeAsync(imported);
                    await LoadDataAsync();
                    System.Windows.MessageBox.Show("İçe aktarıldı.");
                }
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON dosyası (*.json)|*.json", FileName = "snippets.json" };
            if (dialog.ShowDialog() == true)
            {
                var json = JsonSerializer.Serialize(_allSnippets, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(dialog.FileName, json);
                System.Windows.MessageBox.Show("Dışa aktarıldı.");
            }
        }
    }
}
