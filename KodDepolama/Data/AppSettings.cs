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
        public DateTime? LastBackupAt { get; set; }
        public List<string> CustomLanguages { get; set; } = new List<string>();

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
                    LastBackupAt,
                    CustomLanguages
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

        private static string ExpandOrDefault(string? path, string defaultPath)
        {
            return string.IsNullOrWhiteSpace(path)
                ? defaultPath
                : Environment.ExpandEnvironmentVariables(path);
        }

        private static string GetAppDataFolder()
        {
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
