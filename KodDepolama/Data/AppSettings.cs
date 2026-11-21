using System;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Generic;

namespace DevSnippetManager.Data
{
    public class AppSettings
    {
        public static string AppDataFolder { get; } = GetAppDataFolder();
        public static string SettingsFilePath { get; } = Path.Combine(AppDataFolder, "settings.json");

        private string _databasePath = Path.Combine(AppDataFolder, "devvault.db");
        private string _backupFolder = Path.Combine(AppDataFolder, "Backups");

        public string DatabasePath
        {
            get => ExpandOrDefault(_databasePath, Path.Combine(AppDataFolder, "devvault.db"));
            set => _databasePath = value;
        }

        public string BackupFolder
        {
            get => ExpandOrDefault(_backupFolder, Path.Combine(AppDataFolder, "Backups"));
            set => _backupFolder = value;
        }
        public int BackupIntervalHours { get; set; } = 24;
        public int MaxBackups { get; set; } = 10;
        public bool MinimizeToTray { get; set; } = true;
        public string Theme { get; set; } = "Koyu";
        public double SidebarWidth { get; set; } = 340;
        public int EditorFontSize { get; set; } = 14;
        public bool EditorWordWrap { get; set; }
        public bool EditorMinimap { get; set; }
        public bool ShowCodePreview { get; set; } = true;
        public DateTime? LastBackupAt { get; set; }
        public List<string> CustomLanguages { get; set; } = new List<string>();
        public string GitHubTokenProtected { get; set; } = string.Empty;
        public string GistId { get; set; } = string.Empty;
        public DateTimeOffset? LastGistSyncAt { get; set; }
        public DateTimeOffset? LastGistRemoteUpdatedAt { get; set; }

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    var defaults = new AppSettings();
                    defaults.Save();
                    return defaults;
                }

                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                settings.ApplyDefaultsForEmptyPaths();
                settings.CustomLanguages ??= new List<string>();
                settings.GitHubTokenProtected ??= string.Empty;
                settings.GistId ??= string.Empty;
                settings.NormalizeDisplaySettings();
                return settings;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevVault ayarları okunamadı: {ex.Message}");
                var defaults = new AppSettings();
                defaults.Save();
                return defaults;
            }
        }

        public void Save()
        {
            try
            {
                ApplyDefaultsForEmptyPaths();
                Directory.CreateDirectory(AppDataFolder);
                var rawSettings = new
                {
                    DatabasePath = _databasePath,
                    BackupFolder = _backupFolder,
                    BackupIntervalHours,
                    MaxBackups,
                    MinimizeToTray,
                    Theme,
                    SidebarWidth,
                    EditorFontSize,
                    EditorWordWrap,
                    EditorMinimap,
                    ShowCodePreview,
                    LastBackupAt,
                    CustomLanguages,
                    GitHubTokenProtected,
                    GistId,
                    LastGistSyncAt,
                    LastGistRemoteUpdatedAt
                };
                var json = JsonSerializer.Serialize(rawSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevVault ayarları yazılamadı: {ex.Message}");
            }
        }

        private void ApplyDefaultsForEmptyPaths()
        {
            if (string.IsNullOrWhiteSpace(_databasePath))
                _databasePath = Path.Combine(AppDataFolder, "devvault.db");
            if (string.IsNullOrWhiteSpace(_backupFolder))
                _backupFolder = Path.Combine(AppDataFolder, "Backups");
        }

        private void NormalizeDisplaySettings()
        {
            if (Theme is not ("Koyu" or "Açık" or "Sistem"))
                Theme = "Koyu";
            SidebarWidth = Math.Clamp(SidebarWidth, 260, 600);
            EditorFontSize = Math.Clamp(EditorFontSize, 10, 28);
        }

        private static string ExpandOrDefault(string? path, string defaultPath)
        {
            return string.IsNullOrWhiteSpace(path)
                ? defaultPath
                : Environment.ExpandEnvironmentVariables(path);
        }

        private static string GetAppDataFolder()
        {
            // DEVVAULT_DATA_DIR, when set, overrides the normal %APPDATA%\\DevVault folder for testing.
            var overrideFolder = Environment.GetEnvironmentVariable("DEVVAULT_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(overrideFolder))
            {
                var expandedOverride = Environment.ExpandEnvironmentVariables(overrideFolder);
                try { Directory.CreateDirectory(expandedOverride); } catch (Exception ex) { Debug.WriteLine(ex.Message); }
                return expandedOverride;
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(string.IsNullOrWhiteSpace(appData) ? Path.GetTempPath() : appData, "DevVault");
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DevVault klasörü oluşturulamadı: {ex.Message}");
            }

            return folder;
        }
    }
}
