using DevSnippetManager.Data;
using System;
using System.IO;
using Xunit;

namespace DevVault.Tests;

[Collection("SettingsFile")]
public class AppSettingsTests
{
    [Fact]
    public void AppDataFolder_UsesIsolatedTestDirectory()
    {
        var overrideDir = Environment.GetEnvironmentVariable("DEVVAULT_DATA_DIR");
        Assert.False(string.IsNullOrWhiteSpace(overrideDir));
        Assert.Equal(overrideDir, AppSettings.AppDataFolder);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllPropertiesAndPreservesRawEnvPaths()
    {
        var settings = new AppSettings
        {
            DatabasePath = "%TEMP%\\devvault_test_custom.db",
            BackupFolder = "%TEMP%\\devvault_test_backups",
            BackupIntervalHours = 12,
            MaxBackups = 7,
            MinimizeToTray = false,
            Theme = "Açık",
            SidebarWidth = 420,
            EditorFontSize = 18,
            EditorWordWrap = true,
            EditorMinimap = true,
            ShowCodePreview = false,
            GitHubTokenProtected = "encrypted-token",
            GistId = "abc123def",
            LastGistSyncAt = new DateTimeOffset(2026, 11, 21, 18, 0, 0, TimeSpan.Zero),
            LastGistRemoteUpdatedAt = new DateTimeOffset(2026, 11, 21, 17, 55, 0, TimeSpan.Zero)
        };
        settings.Save();

        var rawJson = File.ReadAllText(AppSettings.SettingsFilePath);
        Assert.Contains("%TEMP%", rawJson);

        var loaded = AppSettings.Load();
        Assert.Equal(Environment.ExpandEnvironmentVariables("%TEMP%\\devvault_test_custom.db"), loaded.DatabasePath);
        Assert.Equal(Environment.ExpandEnvironmentVariables("%TEMP%\\devvault_test_backups"), loaded.BackupFolder);
        Assert.Equal(12, loaded.BackupIntervalHours);
        Assert.Equal(7, loaded.MaxBackups);
        Assert.False(loaded.MinimizeToTray);
        Assert.Equal("Açık", loaded.Theme);
        Assert.Equal(420, loaded.SidebarWidth);
        Assert.Equal(18, loaded.EditorFontSize);
        Assert.True(loaded.EditorWordWrap);
        Assert.True(loaded.EditorMinimap);
        Assert.False(loaded.ShowCodePreview);
        Assert.Equal("encrypted-token", loaded.GitHubTokenProtected);
        Assert.Equal("abc123def", loaded.GistId);
        Assert.Equal(settings.LastGistSyncAt, loaded.LastGistSyncAt);
        Assert.Equal(settings.LastGistRemoteUpdatedAt, loaded.LastGistRemoteUpdatedAt);
    }

    [Fact]
    public void Load_NormalizesInvalidDisplaySettings()
    {
        File.WriteAllText(AppSettings.SettingsFilePath, """
            {
              "Theme": "UnknownTheme",
              "SidebarWidth": 9999,
              "EditorFontSize": 2
            }
            """);

        var loaded = AppSettings.Load();
        Assert.Equal("Koyu", loaded.Theme);
        Assert.Equal(600, loaded.SidebarWidth);
        Assert.Equal(10, loaded.EditorFontSize);
    }

    [Fact]
    public void Load_HandlesInvalidJsonEmptyPathsNullThemeClampLowAndCustomLanguages()
    {
        File.WriteAllText(AppSettings.SettingsFilePath, "{ invalid json !!!");
        var fromCorrupt = AppSettings.Load();
        Assert.Equal("Koyu", fromCorrupt.Theme);
        Assert.Equal(340, fromCorrupt.SidebarWidth);

        File.WriteAllText(AppSettings.SettingsFilePath, """{"MaxBackups":"wrong-type"}""");
        var fromWrongType = AppSettings.Load();
        Assert.Equal(10, fromWrongType.MaxBackups);

        File.WriteAllText(AppSettings.SettingsFilePath, """
            {
              "DatabasePath": "   ",
              "BackupFolder": "",
              "Theme": null,
              "SidebarWidth": 50,
              "EditorFontSize": 99,
              "CustomLanguages": null
            }
            """);
        var clamped = AppSettings.Load();
        Assert.EndsWith("devvault.db", clamped.DatabasePath);
        Assert.EndsWith("Backups", clamped.BackupFolder);
        Assert.Equal("Koyu", clamped.Theme);
        Assert.Equal(260, clamped.SidebarWidth);
        Assert.Equal(28, clamped.EditorFontSize);
        Assert.NotNull(clamped.CustomLanguages);
        Assert.Empty(clamped.CustomLanguages);

        clamped.CustomLanguages.Add("Elixir");
        clamped.Save();
        var reloaded = AppSettings.Load();
        Assert.Contains("Elixir", reloaded.CustomLanguages);
    }
}
