using DevSnippetManager.Data;
using DevSnippetManager.Services.ProjectScan;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevVault.Tests;

public class ProjectScanServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ProjectScanServiceTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), $"DevVaultScanSvcTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    private async Task<(SnippetRepository Repo, ProjectScanService Service)> CreateServiceAsync(string dbName)
    {
        var repo = new SnippetRepository(new AppSettings { DatabasePath = Path.Combine(_tempDir, dbName) });
        await repo.InitializeAsync();
        await repo.ClearAsync();
        var service = new ProjectScanService(repo, new IBlockExtractor[]
        {
            new CSharpBlockExtractor(),
            new RuleBasedBlockExtractor()
        });
        return (repo, service);
    }

    [Fact]
    public async Task ScanPaths_FolderGroupsByRootAndApplies()
    {
        var projDir = Path.Combine(_tempDir, "DemoProj");
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "Calc.cs"), """
            namespace Demo;
            public class Calc
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }
            """);

        var (repo, service) = await CreateServiceAsync("folder.db");
        var summaries = await service.ScanPathsAsync(new[] { projDir });

        var summary = Assert.Single(summaries);
        Assert.Equal("DemoProj", summary.ProjectName);
        Assert.Equal(1, summary.Result.Added);

        var snippets = (await repo.GetAllAsync()).ToList();
        Assert.Single(snippets);
        Assert.Equal("Demo.Calc.Add(int,int)", snippets[0].SourceSymbol);
    }

    [Fact]
    public async Task ScanPaths_MixedFilesFromTwoProjects()
    {
        var projA = Path.Combine(_tempDir, "ProjA");
        var projB = Path.Combine(_tempDir, "ProjB");
        Directory.CreateDirectory(projA);
        Directory.CreateDirectory(projB);
        await File.WriteAllTextAsync(Path.Combine(projA, "ProjA.csproj"), "<Project />");
        await File.WriteAllTextAsync(Path.Combine(projB, "package.json"), "{}");

        var fileA = Path.Combine(projA, "A.cs");
        var fileB = Path.Combine(projB, "b.js");
        await File.WriteAllTextAsync(fileA, "public class A { public int F()\n{\n    return 1;\n} }");
        await File.WriteAllTextAsync(fileB, "function g() {\n    const x = 2;\n    return x;\n}");

        var (_, service) = await CreateServiceAsync("mixed.db");
        var summaries = await service.ScanPathsAsync(new[] { fileA, fileB });

        Assert.Equal(2, summaries.Count);
        Assert.Contains(summaries, s => s.ProjectName == "ProjA" && s.Result.Added == 1);
        Assert.Contains(summaries, s => s.ProjectName == "ProjB" && s.Result.Added == 1);
    }

    [Fact]
    public async Task Rescan_UsesPreviouslyScannedFiles()
    {
        var projDir = Path.Combine(_tempDir, "RescanProj");
        Directory.CreateDirectory(projDir);
        var fileA = Path.Combine(projDir, "A.cs");
        await File.WriteAllTextAsync(fileA, "public class A { public int F()\n{\n    return 1;\n} }");

        var (repo, service) = await CreateServiceAsync("rescan.db");
        await service.ScanPathsAsync(new[] { fileA });

        // Modify file and rescan project
        await File.WriteAllTextAsync(fileA, "public class A { public int F()\n{\n    return 2;\n} }");
        var summary = await service.RescanProjectAsync(projDir);

        Assert.Equal(1, summary.Result.Updated);
        var snippet = Assert.Single(await repo.GetAllAsync());
        Assert.Contains("return 2;", snippet.Code);
        Assert.Single(await repo.GetVersionsAsync(snippet.Id));
    }

    [Fact]
    public async Task Scan_CancelledLeavesDatabaseUnchanged()
    {
        var projDir = Path.Combine(_tempDir, "CancelProj");
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "A.cs"), "public class A { public int F()\n{\n    return 1;\n} }");

        var (repo, service) = await CreateServiceAsync("cancel.db");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ScanPathsAsync(new[] { projDir }, ct: cts.Token));
        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public void ReadSource_DetectsEncoding()
    {
        var utf8BomPath = Path.Combine(_tempDir, "utf8bom.cs");
        var utf8NoBomPath = Path.Combine(_tempDir, "utf8nobom.cs");
        var win1254Path = Path.Combine(_tempDir, "win1254.cs");
        var crlfPath = Path.Combine(_tempDir, "crlf.cs");

        const string sample = "// Türkçe: ığüşöçİĞÜŞÖÇ";
        File.WriteAllBytes(utf8BomPath, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(sample)).ToArray());
        File.WriteAllBytes(utf8NoBomPath, Encoding.UTF8.GetBytes(sample));
        File.WriteAllBytes(win1254Path, Encoding.GetEncoding(1254).GetBytes(sample));
        File.WriteAllText(crlfPath, "line1\r\nline2");

        Assert.Equal(sample, ProjectScanService.ReadSource(utf8BomPath));
        Assert.Equal(sample, ProjectScanService.ReadSource(utf8NoBomPath));
        Assert.Equal(sample, ProjectScanService.ReadSource(win1254Path));
        Assert.Equal("line1\r\nline2", ProjectScanService.ReadSource(crlfPath));
    }

    [Fact]
    public void ReadSource_SkipsLargeAndBinary()
    {
        var largePath = Path.Combine(_tempDir, "large.cs");
        var binaryPath = Path.Combine(_tempDir, "bin.cs");

        File.WriteAllBytes(largePath, new byte[(1024 * 1024) + 10]);
        File.WriteAllBytes(binaryPath, new byte[] { (byte)'a', 0, (byte)'b' });

        Assert.Null(ProjectScanService.ReadSource(largePath));
        Assert.Null(ProjectScanService.ReadSource(binaryPath));
    }

    [Fact]
    public void Summary_ToStringTurkish()
    {
        var s1 = new ScanSummary("KodDepolama", @"C:\Repo", new ScanApplyResult(142, 12, 3, 0, 5, 0), 2);
        Assert.Equal("KodDepolama: 142 eklendi, 12 güncellendi, 3 kaynakta yok, 2 dosya atlandı", s1.ToString());

        var s2 = new ScanSummary("KodDepolama", @"C:\Repo", new ScanApplyResult(0, 0, 0, 0, 10, 0), 0);
        Assert.Equal("KodDepolama: değişiklik yok", s2.ToString());
    }

    [Fact]
    public async Task SmokeTest_ScansDevVaultOwnSource()
    {
        // Locate repo root by walking up for DevSnippetManager.slnx
        var dir = AppContext.BaseDirectory;
        string? repoRoot = null;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "DevSnippetManager.slnx")))
            {
                repoRoot = dir;
                break;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(repoRoot);
        var kodDepolamaDir = Path.Combine(repoRoot!, "KodDepolama");
        var (repo, service) = await CreateServiceAsync("smoke.db");

        var summaries = await service.ScanPathsAsync(new[] { kodDepolamaDir });
        var summary = Assert.Single(summaries);
        Assert.True(summary.Result.Added > 100, $"Expected > 100 blocks, got {summary.Result.Added}");

        var all = (await repo.GetAllAsync()).ToList();
        Assert.Contains(all, s => s.SourceSymbol.Contains("MainViewModel.SaveSnippetAsync", StringComparison.Ordinal));
    }
}
