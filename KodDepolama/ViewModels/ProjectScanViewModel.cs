using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevSnippetManager.Services.ProjectScan;
using KodDepolama;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DevSnippetManager.ViewModels
{
    public partial class ProjectScanViewModel : ObservableObject
    {
        private const string SourceFileFilter =
            "Kaynak Dosyaları|*.cs;*.java;*.js;*.jsx;*.mjs;*.cjs;*.ts;*.tsx;*.c;*.h;*.cpp;*.hpp;*.cc;*.cxx;*.go;*.rs;*.php;*.kt;*.kts;*.swift;*.py;*.rb;*.ps1;*.psm1;*.sh;*.bash;*.sql|Tüm Dosyalar|*.*";

        private readonly MainViewModel _main;
        private readonly ISnippetRepository _repository;
        private readonly ProjectScanService _scanService;
        private CancellationTokenSource? _scanCts;

        public ObservableCollection<ScannedProject> ScannedProjects { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ScanFilesCommand))]
        [NotifyCanExecuteChangedFor(nameof(ScanFolderCommand))]
        [NotifyCanExecuteChangedFor(nameof(RescanProjectCommand))]
        [NotifyCanExecuteChangedFor(nameof(RelocateProjectCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelScanCommand))]
        private bool _isScanning;

        [ObservableProperty]
        private string _scanProgressText = string.Empty;

        [ObservableProperty]
        private double _scanProgressValue;

        public bool HasScannedProjects => ScannedProjects.Count > 0;
        public Action<string>? ShowTrayBalloonAction { get; set; }

        public ProjectScanViewModel(MainViewModel main, ISnippetRepository repository)
        {
            _main = main ?? throw new ArgumentNullException(nameof(main));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _scanService = new ProjectScanService(_repository, new IBlockExtractor[]
            {
                new CSharpBlockExtractor(),
                new RuleBasedBlockExtractor()
            });
        }

        public void RefreshProjects(IEnumerable<Snippet> allSnippets)
        {
            var projects = _scanService.GetScannedProjects(allSnippets);
            ScannedProjects.Clear();
            foreach (var p in projects)
                ScannedProjects.Add(p);
            OnPropertyChanged(nameof(HasScannedProjects));
        }

        private bool CanStartScan() => !IsScanning;
        private bool CanCancelScan() => IsScanning;

        [RelayCommand(CanExecute = nameof(CanStartScan))]
        private async Task ScanFilesAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Taranacak kaynak dosyalarını seçin",
                Filter = SourceFileFilter,
                Multiselect = true
            };

            if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
                return;

            await RunScanAsync((progress, ct) => _scanService.ScanPathsAsync(dialog.FileNames, progress, ct));
        }

        [RelayCommand(CanExecute = nameof(CanStartScan))]
        private async Task ScanFolderAsync()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Taranacak proje klasörünü seçin"
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
                return;

            await RunScanAsync((progress, ct) => _scanService.ScanPathsAsync(new[] { dialog.FolderName }, progress, ct));
        }

        [RelayCommand(CanExecute = nameof(CanStartScan))]
        private async Task RescanProjectAsync(ScannedProject? project)
        {
            if (project == null)
                return;

            if (!Directory.Exists(project.Root))
            {
                RefreshProjects(_main.AllSnippets);
                System.Windows.MessageBox.Show(
                    $"Proje klasörü bulunamadı: {project.Root}\n\"Konumu değiştir…\" ile yeni klasörü seçebilirsiniz.",
                    "Uyarı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await RunScanAsync(async (progress, ct) => new[] { await _scanService.RescanProjectAsync(project.Root, progress, ct) });
        }

        [RelayCommand(CanExecute = nameof(CanStartScan))]
        private async Task RelocateProjectAsync(ScannedProject? project)
        {
            if (project == null)
                return;

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = $"'{project.Folder}' projesinin yeni konumunu seçin"
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
                return;

            try
            {
                var count = await _repository.RelocateSourceRootAsync(project.Root, dialog.FolderName);
                await _main.RefreshAfterScanAsync(openSnippetModifiedBeforeScan: false);
                _main.MessageQueue.Enqueue($"Proje konumu güncellendi ({count} snippet)");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Proje konumu güncellenemedi: {ex.Message}",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand(CanExecute = nameof(CanCancelScan))]
        private void CancelScan()
        {
            try
            {
                _scanCts?.Cancel();
            }
            catch
            {
                // Ignore if already disposed
            }
        }

        private IProgress<ScanProgress> CreateProgress()
        {
            return new Progress<ScanProgress>(p =>
            {
                ScanProgressValue = p.Total > 0 ? (double)p.Done / p.Total * 100.0 : 0;
                ScanProgressText = p.Total > 0
                    ? $"Taranıyor ({p.Done}/{p.Total}): {Path.GetFileName(p.CurrentFile)}"
                    : "Taranıyor…";
            });
        }

        private async Task RunScanAsync(Func<IProgress<ScanProgress>, CancellationToken, Task<IReadOnlyList<ScanSummary>>> work)
        {
            var hadUnsavedAtStart = _main.HasUnsavedChanges;
            var progress = CreateProgress();
            _scanCts = new CancellationTokenSource();
            IsScanning = true;
            ScanProgressValue = 0;
            ScanProgressText = "Dosyalar hazırlanıyor…";

            try
            {
                var token = _scanCts.Token;
                var summaries = await Task.Run(() => work(progress, token), token);
                var hadUnsaved = hadUnsavedAtStart || _main.HasUnsavedChanges;
                await _main.RefreshAfterScanAsync(hadUnsaved);

                if (summaries.Count == 0)
                {
                    _main.MessageQueue.Enqueue("Taranacak uygun kaynak dosya bulunamadı.");
                }
                else
                {
                    foreach (var summary in summaries)
                    {
                        var text = summary.ToString();
                        _main.MessageQueue.Enqueue(text);
                        ShowTrayBalloonAction?.Invoke(text);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _main.MessageQueue.Enqueue("Tarama iptal edildi.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Tarama başarısız: {ex.Message}",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _scanCts.Dispose();
                _scanCts = null;
                IsScanning = false;
                ScanProgressText = string.Empty;
                ScanProgressValue = 0;
            }
        }
    }
}
