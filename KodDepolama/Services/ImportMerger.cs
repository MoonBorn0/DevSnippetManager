using KodDepolama;
using System;
using System.Collections.Generic;
using System.Globalization;

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
                var seenKeys = new HashSet<(string Title, string Language)>();
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

            var existingByKey = new Dictionary<(string Title, string Language), Snippet>();
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

            var seenIncomingKeys = new HashSet<(string Title, string Language)>();

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
                        plan.ToUpdate.Add(new Snippet
                        {
                            Id = existingSnippet.Id,
                            Title = existingSnippet.Title,
                            Language = existingSnippet.Language,
                            Code = item.Code ?? string.Empty,
                            Tags = item.Tags ?? string.Empty,
                            Description = item.Description ?? string.Empty,
                             Folder = item.Folder ?? string.Empty,
                             IsFavorite = existingSnippet.IsFavorite,
                             UsageCount = existingSnippet.UsageCount,
                             LastUsedAt = existingSnippet.LastUsedAt,
                            CreatedAt = existingSnippet.CreatedAt,
                            UpdatedAt = now
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

        private static (string Title, string Language) GetKey(Snippet snippet)
        {
            var turkish = new CultureInfo("tr-TR");
            var title = (snippet.Title ?? string.Empty).Trim().ToLower(turkish);
            var language = (string.IsNullOrWhiteSpace(snippet.Language) ? "Diğer" : snippet.Language.Trim())
                .ToLower(turkish);
            return (title, language);
        }

        private static bool HasContentDifference(Snippet existing, Snippet incoming)
        {
            return !string.Equals(existing.Code ?? string.Empty, incoming.Code ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(existing.Tags ?? string.Empty, incoming.Tags ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(existing.Description ?? string.Empty, incoming.Description ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(existing.Folder ?? string.Empty, incoming.Folder ?? string.Empty, StringComparison.Ordinal);
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
                UpdatedAt = source.UpdatedAt == default ? now : source.UpdatedAt
            };
        }
    }
}
