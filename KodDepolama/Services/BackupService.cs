using DevSnippetManager.Data;
using KodDepolama;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DevSnippetManager.Services
{
    public class BackupService
    {
        private const string BackupFilePrefix = "devvault-yedek-";
        private const string BackupSearchPattern = "devvault-yedek-*.json";

        private readonly AppSettings _settings;

        public BackupService(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public bool IsBackupDue()
        {
            if (_settings.BackupIntervalHours <= 0)
                return false;

            if (_settings.LastBackupAt == null)
                return true;

            return DateTime.Now - _settings.LastBackupAt.Value >= TimeSpan.FromHours(_settings.BackupIntervalHours);
        }

        public async Task<string> CreateBackupAsync(IEnumerable<Snippet> snippets)
        {
            Directory.CreateDirectory(_settings.BackupFolder);

            var now = DateTime.Now;
            var fileName = $"{BackupFilePrefix}{now:yyyyMMdd-HHmmss}.json";
            var filePath = Path.Combine(_settings.BackupFolder, fileName);
            var suffix = 2;
            while (File.Exists(filePath))
            {
                fileName = $"{BackupFilePrefix}{now:yyyyMMdd-HHmmss}-{suffix}.json";
                filePath = Path.Combine(_settings.BackupFolder, fileName);
                suffix++;
            }

            var json = JsonSerializer.Serialize(snippets, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            _settings.LastBackupAt = now;
            _settings.Save();

            PruneOldBackups();

            return filePath;
        }

        private void PruneOldBackups()
        {
            if (_settings.MaxBackups <= 0)
                return;

            var files = Directory.GetFiles(_settings.BackupFolder, BackupSearchPattern)
                .Select(path => new FileInfo(path))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(_settings.MaxBackups);

            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                }
                catch (Exception)
                {
                    // Ignore failure to delete an individual old backup file
                }
            }
        }
    }
}
