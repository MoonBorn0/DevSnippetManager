using System;
using System.Collections.Generic;

namespace DevSnippetManager.Services
{
    public enum DiffLineKind
    {
        Unchanged,
        Added,
        Removed
    }

    public sealed record DiffLine(DiffLineKind Kind, string Text);

    public static class LineDiff
    {
        public static IReadOnlyList<DiffLine> Compute(string? oldText, string? newText)
        {
            var oldLines = SplitLines(oldText);
            var newLines = SplitLines(newText);

            var oldStart = 0;
            var newStart = 0;
            var oldEnd = oldLines.Length;
            var newEnd = newLines.Length;

            while (oldStart < oldEnd && newStart < newEnd &&
                   string.Equals(oldLines[oldStart], newLines[newStart], StringComparison.Ordinal))
            {
                oldStart++;
                newStart++;
            }

            while (oldEnd > oldStart && newEnd > newStart &&
                   string.Equals(oldLines[oldEnd - 1], newLines[newEnd - 1], StringComparison.Ordinal))
            {
                oldEnd--;
                newEnd--;
            }

            var result = new List<DiffLine>(oldLines.Length + newLines.Length);

            for (var i = 0; i < oldStart; i++)
                result.Add(new DiffLine(DiffLineKind.Unchanged, oldLines[i]));

            var m = oldEnd - oldStart;
            var n = newEnd - newStart;

            if (m == 0)
            {
                for (var j = newStart; j < newEnd; j++)
                    result.Add(new DiffLine(DiffLineKind.Added, newLines[j]));
            }
            else if (n == 0)
            {
                for (var i = oldStart; i < oldEnd; i++)
                    result.Add(new DiffLine(DiffLineKind.Removed, oldLines[i]));
            }
            else if ((long)m * n > 4_000_000L)
            {
                for (var i = oldStart; i < oldEnd; i++)
                    result.Add(new DiffLine(DiffLineKind.Removed, oldLines[i]));
                for (var j = newStart; j < newEnd; j++)
                    result.Add(new DiffLine(DiffLineKind.Added, newLines[j]));
            }
            else
            {
                var dp = new int[m + 1, n + 1];
                for (var i = m - 1; i >= 0; i--)
                {
                    for (var j = n - 1; j >= 0; j--)
                    {
                        if (string.Equals(oldLines[oldStart + i], newLines[newStart + j], StringComparison.Ordinal))
                            dp[i, j] = dp[i + 1, j + 1] + 1;
                        else
                            dp[i, j] = dp[i + 1, j] >= dp[i, j + 1] ? dp[i + 1, j] : dp[i, j + 1];
                    }
                }

                var oi = 0;
                var ni = 0;
                while (oi < m && ni < n)
                {
                    if (string.Equals(oldLines[oldStart + oi], newLines[newStart + ni], StringComparison.Ordinal))
                    {
                        result.Add(new DiffLine(DiffLineKind.Unchanged, oldLines[oldStart + oi]));
                        oi++;
                        ni++;
                    }
                    else if (dp[oi + 1, ni] >= dp[oi, ni + 1])
                    {
                        result.Add(new DiffLine(DiffLineKind.Removed, oldLines[oldStart + oi]));
                        oi++;
                    }
                    else
                    {
                        result.Add(new DiffLine(DiffLineKind.Added, newLines[newStart + ni]));
                        ni++;
                    }
                }

                while (oi < m)
                {
                    result.Add(new DiffLine(DiffLineKind.Removed, oldLines[oldStart + oi]));
                    oi++;
                }

                while (ni < n)
                {
                    result.Add(new DiffLine(DiffLineKind.Added, newLines[newStart + ni]));
                    ni++;
                }
            }

            for (var i = oldEnd; i < oldLines.Length; i++)
                result.Add(new DiffLine(DiffLineKind.Unchanged, oldLines[i]));

            return result;
        }

        public static IReadOnlyList<DiffLine> FilterContext(IReadOnlyList<DiffLine> lines, int contextLines = 2)
        {
            if (lines.Count == 0)
                return Array.Empty<DiffLine>();

            var keep = new bool[lines.Count];
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Kind == DiffLineKind.Unchanged)
                    continue;

                var start = Math.Max(0, i - contextLines);
                var end = Math.Min(lines.Count - 1, i + contextLines);
                for (var j = start; j <= end; j++)
                    keep[j] = true;
            }

            var filtered = new List<DiffLine>();
            for (var i = 0; i < lines.Count; i++)
            {
                if (keep[i])
                    filtered.Add(lines[i]);
            }
            return filtered;
        }

        private static string[] SplitLines(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            return text.Replace("\r\n", "\n", StringComparison.Ordinal)
                       .Replace('\r', '\n')
                       .Split('\n');
        }
    }
}
