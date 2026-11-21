using DevSnippetManager.Data;
using DevSnippetManager.Services;
using KodDepolama;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace DevVault.Tests;

[Collection("SettingsFile")]
public class BackupServiceTests : IDisposable
{
    private readonly string _tempDir;

    public BackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"DevVaultBackupTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void IsBackupDue_RespectsIntervalAndLastBackupAt()
    {
        var settings = new AppSettings { BackupFolder = _tempDir, BackupIntervalHours = 24, LastBackupAt = null };
        var service = new BackupService(settings);
        Assert.True(service.IsBackupDue());

        settings.LastBackupAt = DateTime.Now.AddHours(-1);
        Assert.False(service.IsBackupDue());

        settings.LastBackupAt = DateTime.Now.AddHours(-25);
        Assert.True(service.IsBackupDue());

        settings.BackupIntervalHours = 0;
        Assert.False(service.IsBackupDue());

        settings.BackupIntervalHours = -5;
        Assert.False(service.IsBackupDue());
    }

    [Fact]
    public async Task CreateBackupAsync_WritesValidJsonAndUpdatesLastBackupAt()
    {
        var settings = new AppSettings { BackupFolder = _tempDir, MaxBackups = 5 };
        var service = new BackupService(settings);
        var snippets = new List<Snippet>
        {
            new() { Id = 1, Title = "Test", Language = "C#", Code = "Console.WriteLine();" }
        };

        var filePath = await service.CreateBackupAsync(snippets);

        Assert.True(File.Exists(filePath));
        Assert.NotNull(settings.LastBackupAt);
        var restored = JsonSerializer.Deserialize<List<Snippet>>(await File.ReadAllTextAsync(filePath));
        Assert.NotNull(restored);
        Assert.Single(restored!);
        Assert.Equal("Test", restored[0].Title);
    }

    [Fact]
    public async Task CreateBackupAsync_PrunesOldFilesBeyondMaxBackupsKeepingNewest()
    {
        var settings = new AppSettings { BackupFolder = _tempDir, MaxBackups = 3 };
        var service = new BackupService(settings);

        for (var i = 1; i <= 5; i++)
        {
            var oldPath = Path.Combine(_tempDir, $"devvault-yedek-2026010{i}-120000.json");
            await File.WriteAllTextAsync(oldPath, "[]");
            File.SetLastWriteTimeUtc(oldPath, new DateTime(2026, 1, i, 12, 0, 0, DateTimeKind.Utc));
        }

        var newestPath = await service.CreateBackupAsync(new List<Snippet>());
        var remaining = Directory.GetFiles(_tempDir, "devvault-yedek-*.json");

        Assert.Equal(3, remaining.Length);
        Assert.Contains(newestPath, remaining);
        Assert.Contains(Path.Combine(_tempDir, "devvault-yedek-20260105-120000.json"), remaining);
        Assert.Contains(Path.Combine(_tempDir, "devvault-yedek-20260104-120000.json"), remaining);
    }

    [Fact]
    public async Task CreateBackupAsync_FileNamePattern_UnicodeRoundTrip_UnrelatedFilesKept_And24hBoundary()
    {
        var unrelated = Path.Combine(_tempDir, "notes.txt");
        await File.WriteAllTextAsync(unrelated, "keep me");

        var settings = new AppSettings { BackupFolder = _tempDir, MaxBackups = 1, BackupIntervalHours = 24 };
        var service = new BackupService(settings);

        var snippets = new List<Snippet>
        {
            new() { Id = 1, Title = "Türkçe Başlık 🚀", Language = "C#", Code = "// ığüşöçİĞÜŞÖÇ" }
        };

        var filePath = await service.CreateBackupAsync(snippets);
        var fileName = Path.GetFileName(filePath);
        Assert.Matches(@"^devvault-yedek-\d{8}-\d{6}(-\d+)?\.json$", fileName);
        Assert.True(File.Exists(unrelated));

        var restored = JsonSerializer.Deserialize<List<Snippet>>(await File.ReadAllTextAsync(filePath));
        Assert.Equal("Türkçe Başlık 🚀", restored![0].Title);
        Assert.Equal("// ığüşöçİĞÜŞÖÇ", restored[0].Code);

        // LastBackupAt persisted to settings.json
        var reloadedSettings = AppSettings.Load();
        Assert.NotNull(reloadedSettings.LastBackupAt);

        // 24h boundary check
        settings.LastBackupAt = DateTime.Now.AddHours(-23).AddMinutes(-59);
        Assert.False(service.IsBackupDue());
        settings.LastBackupAt = DateTime.Now.AddHours(-24).AddSeconds(-1);
        Assert.True(service.IsBackupDue());
    }
}
