using KodDepolama;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevSnippetManager.Services.ProjectScan;

public sealed class ScanPlan
{
    public List<Snippet> ToInsert { get; } = new();
    public List<Snippet> ToUpdate { get; } = new();
    public List<int> MarkMissing { get; } = new();
    public List<int> ClearMissing { get; } = new();
    public List<int> Touch { get; } = new();
    public HashSet<int> RelinkedIds { get; } = new();
    public int Unchanged { get; set; }
    public int Relinked { get; set; }
    public int SkippedFiles { get; set; }
    public DateTime ScannedAt { get; set; }
}

public static class ScanReconciler
{
    public static ScanPlan Reconcile(
        string root,
        string projectFolder,
        IReadOnlyList<ScannedFile> files,
        IReadOnlyList<Snippet> existingForRoot,
        DateTime now)
    {
        var plan = new ScanPlan { ScannedAt = now };
        var existingByKey = new Dictionary<(string File, string Symbol), Snippet>();
        var existingSourceSnippets = new List<Snippet>();

        if (existingForRoot != null)
        {
            foreach (var snippet in existingForRoot)
            {
                if (snippet == null || !snippet.IsFromSource)
                    continue;

                existingSourceSnippets.Add(snippet);
                var key = MakeKey(snippet.SourceFile, snippet.SourceSymbol);
                existingByKey.TryAdd(key, snippet);
            }
        }

        var matchedExistingIds = new HashSet<int>();
        var filesEligibleForMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unmatchedNew = new List<(string RelPath, string Language, ExtractedBlock Block)>();

        if (files != null)
        {
            foreach (var file in files)
            {
                if (file == null)
                    continue;

                var relPath = NormalizeRelativePath(file.RelativePath);

                if (file.Exists && file.Result == null)
                {
                    plan.SkippedFiles++;
                    continue;
                }

                if (!file.Exists)
                {
                    filesEligibleForMissing.Add(relPath);
                    continue;
                }

                var result = file.Result!;
                if (!result.HasErrors)
                {
                    filesEligibleForMissing.Add(relPath);
                }

                foreach (var block in result.Blocks)
                {
                    var key = MakeKey(relPath, block.Symbol);
                    if (existingByKey.TryGetValue(key, out var existing))
                    {
                        matchedExistingIds.Add(existing.Id);

                        var codeChanged = !string.Equals(existing.Code ?? string.Empty, block.Code ?? string.Empty, StringComparison.Ordinal);
                        var langChanged = !string.Equals(existing.Language ?? string.Empty, file.Language ?? string.Empty, StringComparison.Ordinal);
                        var commentChanged = !string.Equals(existing.SourceComment ?? string.Empty, block.Comment ?? string.Empty, StringComparison.Ordinal);

                        if (codeChanged || langChanged || commentChanged)
                        {
                            var descriptionUntouched = string.Equals(
                                existing.Description ?? string.Empty,
                                existing.SourceComment ?? string.Empty,
                                StringComparison.Ordinal);

                            plan.ToUpdate.Add(new Snippet
                            {
                                Id = existing.Id,
                                Title = existing.Title,
                                Tags = existing.Tags,
                                Folder = existing.Folder,
                                IsFavorite = existing.IsFavorite,
                                UsageCount = existing.UsageCount,
                                LastUsedAt = existing.LastUsedAt,
                                CreatedAt = existing.CreatedAt,
                                Language = file.Language,
                                Code = block.Code ?? string.Empty,
                                Description = descriptionUntouched ? (block.Comment ?? string.Empty) : (existing.Description ?? string.Empty),
                                SourceRoot = root,
                                SourceFile = relPath,
                                SourceSymbol = block.Symbol,
                                SourceComment = block.Comment ?? string.Empty,
                                IsSourceMissing = false,
                                LastScannedAt = now,
                                UpdatedAt = now
                            });

                            if (existing.IsSourceMissing)
                                plan.ClearMissing.Add(existing.Id);
                        }
                        else
                        {
                            if (existing.IsSourceMissing)
                                plan.ClearMissing.Add(existing.Id);
                            plan.Touch.Add(existing.Id);
                            plan.Unchanged++;
                        }
                    }
                    else
                    {
                        unmatchedNew.Add((relPath, file.Language, block));
                    }
                }
            }
        }

        // Candidates that were NOT matched and whose files were cleanly scanned or deleted
        var missingCandidates = existingSourceSnippets
            .Where(s => !matchedExistingIds.Contains(s.Id) &&
                        filesEligibleForMissing.Contains(NormalizeRelativePath(s.SourceFile)))
            .ToList();

        var missingByHash = new Dictionary<string, Queue<Snippet>>(StringComparer.Ordinal);
        foreach (var candidate in missingCandidates)
        {
            var hash = ComputeRelinkHash(candidate.Code ?? string.Empty, candidate.SourceSymbol);
            if (!missingByHash.TryGetValue(hash, out var queue))
            {
                queue = new Queue<Snippet>();
                missingByHash[hash] = queue;
            }
            queue.Enqueue(candidate);
        }

        var relinkedIds = new HashSet<int>();

        foreach (var (relPath, language, block) in unmatchedNew)
        {
            var hash = ComputeRelinkHash(block.Code ?? string.Empty, block.Symbol);
            if (missingByHash.TryGetValue(hash, out var queue) && queue.Count > 0)
            {
                var relinkTarget = queue.Dequeue();
                relinkedIds.Add(relinkTarget.Id);
                plan.RelinkedIds.Add(relinkTarget.Id);

                var descriptionUntouched = string.Equals(
                    relinkTarget.Description ?? string.Empty,
                    relinkTarget.SourceComment ?? string.Empty,
                    StringComparison.Ordinal);

                var updatedTags = UpdateFileTagOnRelink(relinkTarget.Tags, relinkTarget.SourceFile, relPath);

                plan.ToUpdate.Add(new Snippet
                {
                    Id = relinkTarget.Id,
                    Title = relinkTarget.Title,
                    Tags = updatedTags,
                    Folder = relinkTarget.Folder,
                    IsFavorite = relinkTarget.IsFavorite,
                    UsageCount = relinkTarget.UsageCount,
                    LastUsedAt = relinkTarget.LastUsedAt,
                    CreatedAt = relinkTarget.CreatedAt,
                    Language = language,
                    Code = block.Code ?? string.Empty,
                    Description = descriptionUntouched && !string.IsNullOrEmpty(block.Comment)
                        ? block.Comment
                        : (relinkTarget.Description ?? string.Empty),
                    SourceRoot = root,
                    SourceFile = relPath,
                    SourceSymbol = block.Symbol,
                    SourceComment = block.Comment ?? string.Empty,
                    IsSourceMissing = false,
                    LastScannedAt = now,
                    UpdatedAt = relinkTarget.UpdatedAt
                });

                plan.Relinked++;
            }
            else
            {
                var fileName = Path.GetFileName(relPath);
                var tags = $"{BlockKindTags.ScanTag}, {BlockKindTags.ToTag(block.Kind)}, {fileName}";
                plan.ToInsert.Add(new Snippet
                {
                    Id = 0,
                    Title = block.DisplayName,
                    Language = language,
                    Folder = projectFolder,
                    Tags = tags,
                    Code = block.Code ?? string.Empty,
                    Description = block.Comment ?? string.Empty,
                    SourceComment = block.Comment ?? string.Empty,
                    SourceRoot = root,
                    SourceFile = relPath,
                    SourceSymbol = block.Symbol,
                    IsSourceMissing = false,
                    LastScannedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        foreach (var candidate in missingCandidates)
        {
            if (relinkedIds.Contains(candidate.Id))
                continue;

            if (!candidate.IsSourceMissing)
                plan.MarkMissing.Add(candidate.Id);
        }

        return plan;
    }

    private static (string File, string Symbol) MakeKey(string? relativePath, string? symbol)
    {
        return (NormalizeRelativePath(relativePath).ToLowerInvariant(), (symbol ?? string.Empty).Trim());
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        return (relativePath ?? string.Empty)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Trim()
            .TrimStart(Path.DirectorySeparatorChar);
    }

    internal static string ComputeRelinkHash(string code, string? symbol)
    {
        var dedented = CodeNormalizer.Dedent(code ?? string.Empty);
        var nameToken = ExtractNameToken(symbol);
        if (string.IsNullOrEmpty(nameToken))
            return CodeNormalizer.ContentHash(dedented);

        var lines = dedented.Split('\n');
        var pattern = $@"(?<![\w$]){Regex.Escape(nameToken)}(?![\w$])";
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.Length == 0 ||
                trimmed.StartsWith("[", StringComparison.Ordinal) ||
                trimmed.StartsWith("@", StringComparison.Ordinal) ||
                trimmed.StartsWith("#[", StringComparison.Ordinal))
                continue;

            var replaced = regex.Replace(lines[i], "__RELINK_SYM__", 1);
            lines[i] = replaced;
            break;
        }

        return CodeNormalizer.ContentHash(string.Join('\n', lines));
    }

    private static string ExtractNameToken(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return string.Empty;

        var s = symbol.Trim();
        var hashIdx = s.LastIndexOf('#');
        if (hashIdx > 0)
            s = s[..hashIdx];

        var parenIdx = s.IndexOf('(');
        if (parenIdx >= 0)
            s = s[..parenIdx];

        var bracketIdx = s.IndexOf('[');
        if (bracketIdx >= 0)
            s = s[..bracketIdx];

        if (s.EndsWith("..ctor", StringComparison.Ordinal))
        {
            var beforeCtor = s[..^6];
            var prevDot = beforeCtor.LastIndexOf('.');
            return prevDot >= 0 ? beforeCtor[(prevDot + 1)..] : beforeCtor;
        }

        var lastDot = s.LastIndexOf('.');
        return lastDot >= 0 ? s[(lastDot + 1)..].Trim() : s.Trim();
    }

    private static string UpdateFileTagOnRelink(string? existingTags, string? oldSourceFile, string newRelativePath)
    {
        var raw = existingTags ?? string.Empty;
        var oldFileName = Path.GetFileName(NormalizeRelativePath(oldSourceFile));
        var newFileName = Path.GetFileName(NormalizeRelativePath(newRelativePath));

        if (string.IsNullOrEmpty(oldFileName) ||
            string.IsNullOrEmpty(newFileName) ||
            string.Equals(oldFileName, newFileName, StringComparison.OrdinalIgnoreCase))
            return raw;

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        var changed = false;
        for (var i = 0; i < parts.Count; i++)
        {
            if (string.Equals(parts[i], oldFileName, StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = newFileName;
                changed = true;
            }
        }

        return changed ? string.Join(", ", parts) : raw;
    }
}
