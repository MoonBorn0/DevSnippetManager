using KodDepolama;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DevSnippetManager.Services.ProjectScan;

public sealed record ScanProgress(int Done, int Total, string CurrentFile);

public sealed record ScanSummary(string ProjectName, string Root, ScanApplyResult Result, int SkippedFiles)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Result.Added > 0) parts.Add($"{Result.Added} eklendi");
        if (Result.Updated > 0) parts.Add($"{Result.Updated} güncellendi");
        if (Result.MarkedMissing > 0) parts.Add($"{Result.MarkedMissing} kaynakta yok");
        if (SkippedFiles > 0) parts.Add($"{SkippedFiles} dosya atlandı");

        return parts.Count == 0
            ? $"{ProjectName}: değişiklik yok"
            : $"{ProjectName}: {string.Join(", ", parts)}";
    }
}

public sealed record ScannedProject(
    string Root,
    string Folder,
    int SnippetCount,
    bool RootExists,
    DateTime? LastScannedAt);

public sealed class ProjectScanService
{
    private const long MaxFileSizeBytes = 1024 * 1024; // 1 MB
    private static readonly Regex CppHeaderPattern = new(@"\b(class\s+\w+|namespace\s+\w+|template\s*<|std::)", RegexOptions.Compiled);

    static ProjectScanService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly ISnippetRepository _repository;
    private readonly IReadOnlyList<IBlockExtractor> _extractors;

    public ProjectScanService(ISnippetRepository repository, IReadOnlyList<IBlockExtractor> extractors)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _extractors = extractors ?? throw new ArgumentNullException(nameof(extractors));
    }

    public static string? ReadSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxFileSizeBytes)
                return null;

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0)
                return string.Empty;

            string text;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }
            else
            {
                if (Array.IndexOf(bytes, (byte)0) >= 0)
                    return null;

                var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                try
                {
                    text = strictUtf8.GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    text = Encoding.GetEncoding(1254).GetString(bytes);
                }
            }

            return text.IndexOf('\0') >= 0 ? null : text;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ScanSummary>> ScanPathsAsync(
        IReadOnlyList<string> paths,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ct.ThrowIfCancellationRequested();

        var (filesByRoot, scannedFolderRoots) = await Task.Run(() =>
        {
            var grouped = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var folderRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawPath in paths)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(rawPath))
                    continue;

                if (Directory.Exists(rawPath))
                {
                    var root = ProjectLocator.NormalizePath(rawPath);
                    folderRoots.Add(root);
                    var enumerated = ProjectLocator.EnumerateSourceFiles(root);
                    if (!grouped.TryGetValue(root, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        grouped[root] = set;
                    }
                    foreach (var f in enumerated)
                        set.Add(ProjectLocator.NormalizePath(f));
                }
                else if (File.Exists(rawPath))
                {
                    var fullFile = ProjectLocator.NormalizePath(rawPath);
                    if (ProjectLocator.ShouldSkipFile(fullFile) || ProjectLocator.LanguageFromPath(fullFile) == null)
                        continue;

                    var root = ProjectLocator.FindProjectRoot(fullFile);
                    if (!grouped.TryGetValue(root, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        grouped[root] = set;
                    }
                    set.Add(fullFile);
                }
            }

            return (grouped, folderRoots);
        }, ct);

        if (filesByRoot.Count == 0)
            return Array.Empty<ScanSummary>();

        var allSnippets = (await _repository.GetAllAsync()).ToList();
        var existingPairs = allSnippets
            .Where(s => s.IsFromSource && !string.IsNullOrWhiteSpace(s.SourceRoot))
            .GroupBy(s => ProjectLocator.NormalizePath(s.SourceRoot), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Root: g.Key, Folder: g.Select(x => x.Folder).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f)) ?? string.Empty))
            .ToList();

        var totalFiles = filesByRoot.Values.Sum(set => set.Count);
        var doneFiles = 0;

        var scannedByRoot = await Task.Run(() =>
        {
            var result = new List<(string Root, string Folder, List<ScannedFile> Files)>();

            foreach (var (root, fileSet) in filesByRoot)
            {
                ct.ThrowIfCancellationRequested();
                var folder = existingPairs.FirstOrDefault(p => ProjectLocator.PathEquals(p.Root, root)).Folder;
                if (string.IsNullOrWhiteSpace(folder))
                {
                    folder = ProjectLocator.ProjectFolderName(root, existingPairs);
                    existingPairs.Add((root, folder));
                }

                var scannedFiles = new List<ScannedFile>(fileSet.Count);
                foreach (var fullFile in fileSet.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    ct.ThrowIfCancellationRequested();
                    var relPath = Path.GetRelativePath(root, fullFile);
                    progress?.Report(new ScanProgress(doneFiles, totalFiles, relPath));

                    scannedFiles.Add(ScanSingleFile(fullFile, relPath));
                    doneFiles++;
                    progress?.Report(new ScanProgress(doneFiles, totalFiles, relPath));
                }

                // For scanned folders, also mark previously known SourceFiles that no longer exist on disk
                if (scannedFolderRoots.Contains(root))
                {
                    var knownRelPaths = allSnippets
                        .Where(s => s.IsFromSource && !string.IsNullOrWhiteSpace(s.SourceRoot) &&
                                    ProjectLocator.PathEquals(s.SourceRoot, root) &&
                                    !string.IsNullOrWhiteSpace(s.SourceFile))
                        .Select(s => s.SourceFile)
                        .Distinct(StringComparer.OrdinalIgnoreCase);

                    foreach (var knownRel in knownRelPaths)
                    {
                        var fullKnown = ProjectLocator.NormalizePath(Path.Combine(root, knownRel));
                        if (!fileSet.Contains(fullKnown) && !File.Exists(fullKnown))
                        {
                            var lang = ProjectLocator.LanguageFromPath(fullKnown) ?? "Diğer";
                            scannedFiles.Add(new ScannedFile(knownRel, lang, null, Exists: false));
                        }
                    }
                }

                result.Add((root, folder, scannedFiles));
            }

            return result;
        }, ct);

        ct.ThrowIfCancellationRequested();

        var now = DateTime.Now;
        var summaries = await Task.Run(async () =>
        {
            var list = new List<ScanSummary>(scannedByRoot.Count);
            foreach (var (root, folder, scannedFiles) in scannedByRoot)
            {
                ct.ThrowIfCancellationRequested();
                var existingForRoot = allSnippets
                    .Where(s => s.IsFromSource && !string.IsNullOrWhiteSpace(s.SourceRoot) && ProjectLocator.PathEquals(s.SourceRoot, root))
                    .ToList();

                var plan = ScanReconciler.Reconcile(root, folder, scannedFiles, existingForRoot, now);
                var applyResult = await _repository.ApplyScanAsync(plan).ConfigureAwait(false);
                list.Add(new ScanSummary(folder, root, applyResult, plan.SkippedFiles));
            }
            return list;
        }, ct).ConfigureAwait(false);

        return summaries;
    }

    public async Task<ScanSummary> RescanProjectAsync(
        string root,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ct.ThrowIfCancellationRequested();

        var normalizedRoot = ProjectLocator.NormalizePath(root);
        var allSnippets = (await _repository.GetAllAsync()).ToList();
        var existingForRoot = allSnippets
            .Where(s => s.IsFromSource && !string.IsNullOrWhiteSpace(s.SourceRoot) && ProjectLocator.PathEquals(s.SourceRoot, normalizedRoot))
            .ToList();

        var existingPairs = allSnippets
            .Where(s => s.IsFromSource && !string.IsNullOrWhiteSpace(s.SourceRoot))
            .GroupBy(s => ProjectLocator.NormalizePath(s.SourceRoot), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Root: g.Key, Folder: g.Select(x => x.Folder).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f)) ?? string.Empty))
            .ToList();

        var folder = existingForRoot.Select(s => s.Folder).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f))
            ?? ProjectLocator.ProjectFolderName(normalizedRoot, existingPairs);

        var relPaths = existingForRoot
            .Select(s => s.SourceFile)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scannedFiles = await Task.Run(() =>
        {
            var list = new List<ScannedFile>(relPaths.Count);
            for (var i = 0; i < relPaths.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var relPath = relPaths[i];
                progress?.Report(new ScanProgress(i, relPaths.Count, relPath));

                var fullPath = Path.Combine(normalizedRoot, relPath);
                if (!File.Exists(fullPath))
                {
                    var lang = ProjectLocator.LanguageFromPath(fullPath) ?? "Diğer";
                    list.Add(new ScannedFile(relPath, lang, null, Exists: false));
                }
                else
                {
                    list.Add(ScanSingleFile(fullPath, relPath));
                }

                progress?.Report(new ScanProgress(i + 1, relPaths.Count, relPath));
            }
            return list;
        }, ct);

        ct.ThrowIfCancellationRequested();

        return await Task.Run(async () =>
        {
            var plan = ScanReconciler.Reconcile(normalizedRoot, folder, scannedFiles, existingForRoot, DateTime.Now);
            var applyResult = await _repository.ApplyScanAsync(plan).ConfigureAwait(false);
            return new ScanSummary(folder, normalizedRoot, applyResult, plan.SkippedFiles);
        }, ct).ConfigureAwait(false);
    }

    public IReadOnlyList<ScannedProject> GetScannedProjects(IEnumerable<Snippet> all)
    {
        if (all == null)
            return Array.Empty<ScannedProject>();

        return all
            .Where(s => s.IsFromSource && !string.IsNullOrWhiteSpace(s.SourceRoot))
            .GroupBy(s => ProjectLocator.NormalizePath(s.SourceRoot), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var root = g.Key;
                var folder = g.Select(s => s.Folder).FirstOrDefault(f => !string.IsNullOrWhiteSpace(f))
                    ?? Path.GetFileName(root);
                return new ScannedProject(
                    root,
                    folder,
                    g.Count(),
                    Directory.Exists(root),
                    g.Max(s => s.LastScannedAt));
            })
            .OrderBy(p => p.Folder, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private ScannedFile ScanSingleFile(string fullPath, string relPath)
    {
        var language = ProjectLocator.LanguageFromPath(fullPath) ?? "Diğer";
        var source = ReadSource(fullPath);
        if (source == null)
            return new ScannedFile(relPath, language, null, Exists: true);

        if (fullPath.EndsWith(".h", StringComparison.OrdinalIgnoreCase) && CppHeaderPattern.IsMatch(source))
            language = "C++";

        var extractor = _extractors.FirstOrDefault(e => e.CanHandle(language));
        if (extractor == null)
            return new ScannedFile(relPath, language, null, Exists: true);

        try
        {
            var result = extractor.Extract(source, language);
            return new ScannedFile(relPath, language, result, Exists: true);
        }
        catch
        {
            return new ScannedFile(relPath, language, null, Exists: true);
        }
    }
}
