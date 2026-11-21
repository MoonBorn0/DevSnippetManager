using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DevSnippetManager.Services
{
    public sealed record FuzzyMatch(int Score, IReadOnlyList<int> MatchedIndices);

    public sealed record TextSegment(string Text, bool IsMatch);

    public static class FuzzyMatcher
    {
        private static readonly CultureInfo Turkish = new("tr-TR");

        public static FuzzyMatch? Match(string pattern, string text)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrEmpty(text))
                return null;

            var indices = new List<int>();
            var score = 0;
            foreach (var word in pattern.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var wordMatch = MatchWord(word, text);
                if (wordMatch == null)
                    return null;

                score += wordMatch.Value.Score;
                indices.AddRange(wordMatch.Value.Indices);
            }

            return new FuzzyMatch(score, indices.Distinct().OrderBy(i => i).ToArray());
        }

        public static IReadOnlyList<TextSegment> ToSegments(string text, IReadOnlyList<int> indices)
        {
            if (indices.Count == 0)
                return new[] { new TextSegment(text, false) };

            var matched = indices.Distinct().Where(i => i >= 0 && i < text.Length).OrderBy(i => i).ToArray();
            if (matched.Length == 0)
                return new[] { new TextSegment(text, false) };

            var result = new List<TextSegment>();
            var start = matched[0];
            if (start > 0)
                result.Add(new TextSegment(text[..start], false));

            var end = start;
            for (var i = 1; i < matched.Length; i++)
            {
                if (matched[i] == end + 1)
                {
                    end = matched[i];
                    continue;
                }

                result.Add(new TextSegment(text[start..(end + 1)], true));
                if (end + 1 < matched[i])
                    result.Add(new TextSegment(text[(end + 1)..matched[i]], false));
                start = end = matched[i];
            }

            result.Add(new TextSegment(text[start..(end + 1)], true));
            var cursor = end + 1;
            if (cursor < text.Length)
                result.Add(new TextSegment(text[cursor..], false));
            return result;
        }

        private static (int Score, IReadOnlyList<int> Indices)? MatchWord(string word, string text)
        {
            var normalizedWord = Normalize(word);
            var normalizedText = Normalize(text);
            var bestScore = int.MinValue;
            List<int>? best = null;

            for (var start = normalizedText.IndexOf(normalizedWord, StringComparison.Ordinal); start >= 0;
                 start = normalizedText.IndexOf(normalizedWord, start + 1, StringComparison.Ordinal))
            {
                var indices = Enumerable.Range(start, normalizedWord.Length).ToList();
                var current = 1000 + normalizedWord.Length * 20 + StartBonus(text, start);
                if (current > bestScore)
                {
                    bestScore = current;
                    best = indices;
                }
            }

            if (best != null)
                return (bestScore, best);

            var subsequence = new List<int>();
            var textIndex = 0;
            for (var patternIndex = 0; patternIndex < normalizedWord.Length; patternIndex++)
            {
                var found = normalizedText.IndexOf(normalizedWord[patternIndex], textIndex);
                if (found < 0)
                    return null;
                subsequence.Add(found);
                textIndex = found + 1;
            }

            var score = normalizedWord.Length * 20 + StartBonus(text, subsequence[0]);
            for (var i = 1; i < subsequence.Count; i++)
                score += subsequence[i] == subsequence[i - 1] + 1 ? 15 : -Math.Min(8, subsequence[i] - subsequence[i - 1] - 1);
            return (score, subsequence);
        }

        private static int StartBonus(string text, int index)
        {
            if (index == 0)
                return 80;
            var previous = text[index - 1];
            if (previous is ' ' or '_' or '-' or '.' or '/')
                return 45;
            return char.IsLower(previous) && char.IsUpper(text[index]) ? 35 : 0;
        }

        private static string Normalize(string value)
        {
            return string.Concat(value.Select(c => char.ToLower(c, Turkish)));
        }
    }
}
