using KodDepolama;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DevSnippetManager.Services
{
    public enum ImportMode
    {
        Merge,
        SkipDuplicates,
        ReplaceAll
    }

    public sealed class ImportPlan
    {
        public List<Snippet> ToAdd { get; } = new();
        public List<Snippet> ToUpdate { get; } = new();
        public List<Snippet> Skipped { get; } = new();
    }

    public static class ImportMerger
    {
        private static readonly CultureInfo Turkish = new("tr-TR");

        private readonly record struct IdentityKey(bool IsSource, string Part1, string Part2, string Part3);

        public static ImportPlan Plan(
            IReadOnlyList<Snippet> existing,
            IReadOnlyList<Snippet> incoming,
            ImportMode mode)
        {
            var plan = new ImportPlan();
            if (incoming == null || incoming.Count == 0)
                return plan;

            var now = DateTime.Now;

            if (mode == ImportMode.ReplaceAll)
            {
                var seenKeys = new HashSet<IdentityKey>();
                foreach (var item in incoming)
                {
                    if (item == null)
                        continue;

                    var key = GetKey(item);
                    if (!seenKeys.Add(key))
                    {
                        plan.Skipped.Add(item);
                        continue;
                    }

                    plan.ToAdd.Add(CreateNewSnippet(item, now));
                }

                return plan;
            }

            var existingByKey = new Dictionary<IdentityKey, Snippet>();
            if (existing != null)
            {
                foreach (var item in existing)
                {
                    if (item == null)
                        continue;

                    var key = GetKey(item);
                    existingByKey.TryAdd(key, item);
                }
            }

            var seenIncomingKeys = new HashSet<IdentityKey>();

            foreach (var item in incoming)
            {
                if (item == null)
                    continue;

                var key = GetKey(item);
                if (!seenIncomingKeys.Add(key))
                {
                    plan.Skipped.Add(item);
                    continue;
                }

                if (existingByKey.TryGetValue(key, out var existingSnippet))
                {
                    if (mode == ImportMode.SkipDuplicates)
                    {
                        plan.Skipped.Add(item);
                        continue;
                    }

                    if (HasContentDifference(existingSnippet, item))
                    {
                        var localSourceRoot = !string.IsNullOrWhiteSpace(existingSnippet.SourceRoot)
                            ? existingSnippet.SourceRoot
                            : (item.SourceRoot ?? string.Empty);

                        plan.ToUpdate.Add(new Snippet
                        {
                            Id = existingSnippet.Id,
                            Title = existingSnippet.IsFromSource
                                ? (string.IsNullOrWhiteSpace(item.Title) ? existingSnippet.Title : item.Title.Trim())
                                : existingSnippet.Title,
                            Language = existingSnippet.IsFromSource
                                ? (string.IsNullOrWhiteSpace(item.Language) ? existingSnippet.Language : item.Language.Trim())
                                : existingSnippet.Language,
                            Code = item.Code ?? string.Empty,
                            Tags = item.Tags ?? string.Empty,
                            Description = item.Description ?? string.Empty,
                            Folder = item.Folder ?? string.Empty,
                            IsFavorite = existingSnippet.IsFavorite,
                            UsageCount = existingSnippet.UsageCount,
                            LastUsedAt = existingSnippet.LastUsedAt,
                            CreatedAt = existingSnippet.CreatedAt,
                            UpdatedAt = now,
                            SourceRoot = localSourceRoot,
                            SourceFile = item.SourceFile ?? existingSnippet.SourceFile ?? string.Empty,
                            SourceSymbol = item.SourceSymbol ?? existingSnippet.SourceSymbol ?? string.Empty,
                            SourceComment = item.SourceComment ?? string.Empty,
                            IsSourceMissing = item.IsSourceMissing,
                            LastScannedAt = item.LastScannedAt ?? existingSnippet.LastScannedAt
                        });
                    }
                    else
                    {
                        plan.Skipped.Add(item);
                    }
                }
                else
                {
                    plan.ToAdd.Add(CreateNewSnippet(item, now));
                }
            }

            return plan;
        }

        private static IdentityKey GetKey(Snippet snippet)
        {
            var symbol = (snippet.SourceSymbol ?? string.Empty).Trim();
            if (symbol.Length > 0)
            {
                var folder = (snippet.Folder ?? string.Empty).Trim().ToLower(Turkish);
                var file = (snippet.SourceFile ?? string.Empty)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                    .Trim()
                    .TrimStart(Path.DirectorySeparatorChar)
                    .ToLower(Turkish);
                return new IdentityKey(true, folder, file, symbol.ToLower(Turkish));
            }

            var title = (snippet.Title ?? string.Empty).Trim().ToLower(Turkish);
            var language = (string.IsNullOrWhiteSpace(snippet.Language) ? "Diğer" : snippet.Language.Trim())
                .ToLower(Turkish);
            return new IdentityKey(false, title, language, string.Empty);
        }

        private static bool HasContentDifference(Snippet existing, Snippet incoming)
        {
            var baseDiff = !string.Equals(existing.Code ?? string.Empty, incoming.Code ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(existing.Tags ?? string.Empty, incoming.Tags ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(existing.Description ?? string.Empty, incoming.Description ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(existing.Folder ?? string.Empty, incoming.Folder ?? string.Empty, StringComparison.Ordinal);

            if (baseDiff || !existing.IsFromSource)
                return baseDiff;

            return !string.Equals(existing.Title ?? string.Empty, (incoming.Title ?? string.Empty).Trim(), StringComparison.Ordinal)
                || !string.Equals(existing.SourceComment ?? string.Empty, incoming.SourceComment ?? string.Empty, StringComparison.Ordinal)
                || existing.IsSourceMissing != incoming.IsSourceMissing;
        }

        private static Snippet CreateNewSnippet(Snippet source, DateTime now)
        {
            return new Snippet
            {
                Id = 0,
                Title = (source.Title ?? string.Empty).Trim(),
                Language = string.IsNullOrWhiteSpace(source.Language) ? "Diğer" : source.Language.Trim(),
                Code = source.Code ?? string.Empty,
                Tags = source.Tags ?? string.Empty,
                Description = source.Description ?? string.Empty,
                Folder = source.Folder ?? string.Empty,
                IsFavorite = source.IsFavorite,
                UsageCount = source.UsageCount,
                LastUsedAt = source.LastUsedAt,
                CreatedAt = source.CreatedAt == default ? now : source.CreatedAt,
                UpdatedAt = source.UpdatedAt == default ? now : source.UpdatedAt,
                SourceRoot = source.SourceRoot ?? string.Empty,
                SourceFile = source.SourceFile ?? string.Empty,
                SourceSymbol = source.SourceSymbol ?? string.Empty,
                SourceComment = source.SourceComment ?? string.Empty,
                IsSourceMissing = source.IsSourceMissing,
                LastScannedAt = source.LastScannedAt
            };
        }
    }
}
