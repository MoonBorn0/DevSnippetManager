using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevSnippetManager.Data;
using DevSnippetManager.Services;
using KodDepolama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace DevSnippetManager.ViewModels
{
    public partial class SyncViewModel : ObservableObject
    {
        private static readonly HttpClient SharedHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private readonly MainViewModel _main;
        private readonly AppSettings _settings;
        private readonly GistSyncService _gistService;
        private bool _updatingGistId;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PushToGistCommand))]
        [NotifyCanExecuteChangedFor(nameof(PullFromGistCommand))]
        [NotifyCanExecuteChangedFor(nameof(ClearGitHubTokenCommand))]
        private bool _hasGitHubToken;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PullFromGistCommand))]
        private string _gistId = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PushToGistCommand))]
        [NotifyCanExecuteChangedFor(nameof(PullFromGistCommand))]
        [NotifyCanExecuteChangedFor(nameof(ClearGitHubTokenCommand))]
        private bool _isSyncBusy;

        public string SyncStatusText => _settings.LastGistSyncAt.HasValue
            ? $"Son senkronizasyon: {_settings.LastGistSyncAt.Value.ToLocalTime():dd.MM.yyyy HH:mm}"
            : "Henüz senkronize edilmedi";

        public SyncViewModel(MainViewModel main, AppSettings settings)
        {
            _main = main ?? throw new ArgumentNullException(nameof(main));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _gistService = new GistSyncService(SharedHttpClient);

            _hasGitHubToken = !string.IsNullOrWhiteSpace(_settings.GitHubTokenProtected);
            _gistId = NormalizeGistId(_settings.GistId);
        }

        partial void OnGistIdChanged(string value)
        {
            if (_updatingGistId)
                return;

            var normalized = NormalizeGistId(value);
            if (!string.Equals(value, normalized, StringComparison.Ordinal))
            {
                _updatingGistId = true;
                try
                {
                    GistId = normalized;
                }
                finally
                {
                    _updatingGistId = false;
                }
            }

            if (!string.Equals(_settings.GistId, normalized, StringComparison.Ordinal))
            {
                _settings.GistId = normalized;
                _settings.Save();
            }
        }

        public static string NormalizeGistId(string? input)
        {
            var trimmed = (input ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
                return string.Empty;

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                    return segments[^1].Trim();
            }

            return trimmed;
        }

        public async Task<bool> SaveGitHubTokenAsync(string token)
        {
            if (IsSyncBusy)
                return false;

            var trimmed = (token ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                System.Windows.MessageBox.Show(
                    "Lütfen geçerli bir GitHub token'ı girin.",
                    "Uyarı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            IsSyncBusy = true;
            try
            {
                var result = await _gistService.ValidateTokenAsync(trimmed);
                if (result.Success)
                {
                    _settings.GitHubTokenProtected = SecretProtector.Protect(trimmed);
                    _settings.Save();
                    HasGitHubToken = true;
                    _main.MessageQueue.Enqueue($"GitHub hesabı doğrulandı: {result.Message}");
                    return true;
                }

                System.Windows.MessageBox.Show(
                    result.Message,
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(
                    "GitHub token'ı doğrulanırken beklenmeyen bir hata oluştu.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
            finally
            {
                IsSyncBusy = false;
            }
        }

        private bool CanClearToken() => HasGitHubToken && !IsSyncBusy;

        [RelayCommand(CanExecute = nameof(CanClearToken))]
        private void ClearGitHubToken()
        {
            _settings.GitHubTokenProtected = string.Empty;
            _settings.Save();
            HasGitHubToken = false;
            _main.MessageQueue.Enqueue("GitHub token'ı kaldırıldı.");
        }

        private bool CanPush() => HasGitHubToken && !IsSyncBusy;

        [RelayCommand(CanExecute = nameof(CanPush))]
        private async Task PushToGistAsync()
        {
            var token = SecretProtector.Unprotect(_settings.GitHubTokenProtected);
            if (string.IsNullOrEmpty(token))
            {
                System.Windows.MessageBox.Show(
                    "Kayıtlı GitHub token'ı okunamadı. Lütfen token'ı yeniden kaydedin.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            IsSyncBusy = true;
            try
            {
                var json = _main.SerializeAllSnippets();
                var snippetCount = _main.AllSnippets.Count;
                var targetGistId = string.IsNullOrWhiteSpace(GistId) ? null : GistId;

                if (targetGistId != null)
                {
                    var remote = await _gistService.PullAsync(token, targetGistId);
                    if (remote.Success)
                    {
                        if (remote.UpdatedAt.HasValue &&
                            _settings.LastGistRemoteUpdatedAt.HasValue &&
                            remote.UpdatedAt.Value > _settings.LastGistRemoteUpdatedAt.Value)
                        {
                            var overwrite = System.Windows.MessageBox.Show(
                                "Gist başka bir yerden güncellenmiş görünüyor. Üzerine yazılsın mı?",
                                "Onay",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                            if (overwrite != MessageBoxResult.Yes)
                                return;
                        }
                        else if (!_settings.LastGistRemoteUpdatedAt.HasValue &&
                                 !string.IsNullOrWhiteSpace(remote.Content))
                        {
                            var overwrite = System.Windows.MessageBox.Show(
                                "Gist'te zaten veri var. Üzerine yazılsın mı?",
                                "Onay",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                            if (overwrite != MessageBoxResult.Yes)
                                return;
                        }
                    }
                    else if (remote.Status == GistStatus.NotFound)
                    {
                        var createNew = System.Windows.MessageBox.Show(
                            "Gist bulunamadı. Yeni bir gist oluşturulsun mu?",
                            "Onay",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        if (createNew != MessageBoxResult.Yes)
                            return;
                        targetGistId = null;
                    }
                    else if (remote.Status != GistStatus.InvalidResponse)
                    {
                        System.Windows.MessageBox.Show(
                            remote.Message,
                            "Hata",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }

                var pushResult = await _gistService.PushAsync(token, targetGistId, json);
                if (!pushResult.Success && pushResult.Status == GistStatus.NotFound && targetGistId != null)
                {
                    var createNew = System.Windows.MessageBox.Show(
                        "Gist bulunamadı. Yeni bir gist oluşturulsun mu?",
                        "Onay",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (createNew != MessageBoxResult.Yes)
                        return;

                    pushResult = await _gistService.PushAsync(token, null, json);
                }

                if (pushResult.Success)
                {
                    if (!string.IsNullOrWhiteSpace(pushResult.GistId))
                        GistId = pushResult.GistId;

                    var now = DateTimeOffset.Now;
                    _settings.GistId = GistId;
                    _settings.LastGistSyncAt = now;
                    _settings.LastGistRemoteUpdatedAt = pushResult.UpdatedAt ?? now;
                    _settings.Save();
                    OnPropertyChanged(nameof(SyncStatusText));
                    _main.MessageQueue.Enqueue($"Gist'e gönderildi ({snippetCount} snippet)");
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        pushResult.Message,
                        "Hata",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(
                    "Gist'e gönderme sırasında beklenmeyen bir hata oluştu.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSyncBusy = false;
            }
        }

        private bool CanPull() => HasGitHubToken && !string.IsNullOrWhiteSpace(GistId) && !IsSyncBusy;

        [RelayCommand(CanExecute = nameof(CanPull))]
        private async Task PullFromGistAsync()
        {
            var token = SecretProtector.Unprotect(_settings.GitHubTokenProtected);
            if (string.IsNullOrEmpty(token))
            {
                System.Windows.MessageBox.Show(
                    "Kayıtlı GitHub token'ı okunamadı. Lütfen token'ı yeniden kaydedin.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            IsSyncBusy = true;
            try
            {
                var pullResult = await _gistService.PullAsync(token, GistId);
                if (!pullResult.Success)
                {
                    System.Windows.MessageBox.Show(
                        pullResult.Message,
                        "Hata",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                List<Snippet>? imported;
                try
                {
                    imported = JsonSerializer.Deserialize<List<Snippet>>(pullResult.Content ?? string.Empty);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Gist içeriği okunamadı: {ex.Message}",
                        "Uyarı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (imported == null || imported.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "Gist'te snippet bulunamadı.",
                        "Uyarı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var plan = ImportMerger.Plan(_main.AllSnippets, imported, ImportMode.Merge);
                if (plan.ToAdd.Count == 0 && plan.ToUpdate.Count == 0)
                {
                    var nowSame = DateTimeOffset.Now;
                    _settings.LastGistSyncAt = nowSame;
                    _settings.LastGistRemoteUpdatedAt = pullResult.UpdatedAt ?? nowSame;
                    _settings.Save();
                    OnPropertyChanged(nameof(SyncStatusText));
                    _main.MessageQueue.Enqueue("Gist ile yerel veriler zaten aynı.");
                    return;
                }

                var confirm = System.Windows.MessageBox.Show(
                    $"Gist'ten alınacak: {plan.ToAdd.Count} yeni, {plan.ToUpdate.Count} güncellenecek, {plan.Skipped.Count} aynı.\n\nEşleşen snippet'ler Gist sürümüyle güncellenecek (eski sürümler geçmişte saklanır). Devam edilsin mi?",
                    "Gist'ten Al",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;

                var applied = await _main.ApplyImportPlanAsync(plan, ImportMode.Merge, "Gist'ten alındı", createBackupBeforeApply: true);
                if (applied)
                {
                    var now = DateTimeOffset.Now;
                    _settings.LastGistSyncAt = now;
                    _settings.LastGistRemoteUpdatedAt = pullResult.UpdatedAt ?? now;
                    _settings.Save();
                    OnPropertyChanged(nameof(SyncStatusText));
                }
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(
                    "Gist'ten alma sırasında beklenmeyen bir hata oluştu.",
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsSyncBusy = false;
            }
        }
    }
}
