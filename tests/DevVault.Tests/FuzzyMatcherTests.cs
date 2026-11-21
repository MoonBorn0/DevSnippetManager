using DevSnippetManager.Services;
using System;
using System.Linq;
using Xunit;

namespace DevVault.Tests;

public class FuzzyMatcherTests
{
    [Theory]
    [InlineData("", "abc")]
    [InlineData("   ", "abc")]
    [InlineData("a", "")]
    [InlineData("xyz", "abc")]
    [InlineData("abcd", "abc")]
    public void Match_ReturnsNull_WhenNoMatchOrEmpty(string pattern, string text)
    {
        Assert.Null(FuzzyMatcher.Match(pattern, text));
    }

    [Fact]
    public void Match_ExactSubstringScoresHigherThanScatteredSubsequence()
    {
        var exact = FuzzyMatcher.Match("date", "Format date helper");
        var scattered1 = FuzzyMatcher.Match("dte", "format date");
        var scattered2 = FuzzyMatcher.Match("date", "xdxaxtxe");

        Assert.NotNull(exact);
        Assert.NotNull(scattered1);
        Assert.NotNull(scattered2);
        Assert.True(exact!.Score > scattered1!.Score);
        Assert.True(exact.Score > scattered2!.Score);
    }

    [Fact]
    public void Match_WordStartAndPrefixBonuses()
    {
        var prefix = FuzzyMatcher.Match("for", "for loop");
        var mid = FuzzyMatcher.Match("for", "loop for");
        var wordStart = FuzzyMatcher.Match("fd", "format_date");
        var nonWordStart = FuzzyMatcher.Match("fd", "if dummy");

        Assert.NotNull(prefix);
        Assert.NotNull(mid);
        Assert.True(prefix!.Score > mid!.Score);
        Assert.NotNull(wordStart);
        Assert.NotNull(nonWordStart);
        Assert.True(wordStart!.Score > nonWordStart!.Score);
    }

    [Fact]
    public void Match_MultiWordRequiresAllAndIsOrderIndependent()
    {
        Assert.NotNull(FuzzyMatcher.Match("foo bar", "bar and foo"));
        Assert.NotNull(FuzzyMatcher.Match("bar foo", "foo bar"));
        Assert.Null(FuzzyMatcher.Match("foo baz", "bar and foo"));
    }

    [Fact]
    public void Match_TurkishCaseFoldingRules()
    {
        Assert.NotNull(FuzzyMatcher.Match("istanbul", "İstanbul"));
        Assert.NotNull(FuzzyMatcher.Match("İSTANBUL", "istanbul"));
        Assert.NotNull(FuzzyMatcher.Match("ISIK", "ısık"));
        Assert.NotNull(FuzzyMatcher.Match("ışık", "IŞIK"));
        Assert.NotNull(FuzzyMatcher.Match("ısık", "ISIK"));
        Assert.Null(FuzzyMatcher.Match("ISIK", "ışık"));
        Assert.Null(FuzzyMatcher.Match("i", "I"));
        Assert.NotNull(FuzzyMatcher.Match("i", "İ"));
        Assert.NotNull(FuzzyMatcher.Match("ı", "I"));

        var trIdx = FuzzyMatcher.Match("sta", "İstanbul");
        Assert.NotNull(trIdx);
        Assert.Equal(new[] { 1, 2, 3 }, trIdx!.MatchedIndices);
    }

    [Fact]
    public void Match_IndicesAreSortedDistinctAndInRange()
    {
        var text = "FormatDate and GetThing";
        var match = FuzzyMatcher.Match("fd gt", text);
        Assert.NotNull(match);
        Assert.Equal(match!.MatchedIndices.Distinct().OrderBy(i => i), match.MatchedIndices);
        Assert.All(match.MatchedIndices, i => Assert.InRange(i, 0, text.Length - 1));

        var overlap = FuzzyMatcher.Match("aa a", "aaa");
        Assert.NotNull(overlap);
        Assert.Equal(overlap!.MatchedIndices.Distinct().Count(), overlap.MatchedIndices.Count);
    }

    [Fact]
    public void Match_LongScatteredTextStillPositiveScore()
    {
        var longText = new string('x', 200) + "a" + new string('y', 200) + "b";
        var result = FuzzyMatcher.Match("ab", longText);
        Assert.NotNull(result);
        Assert.True(result!.Score > 0);
    }

    [Fact]
    public void ToSegments_SplitsAndMergesCorrectly()
    {
        var seg = FuzzyMatcher.ToSegments("FormatDate", new[] { 0, 6 });
        Assert.Equal(4, seg.Count);
        Assert.Equal(("F", true), (seg[0].Text, seg[0].IsMatch));
        Assert.Equal(("ormat", false), (seg[1].Text, seg[1].IsMatch));
        Assert.Equal(("D", true), (seg[2].Text, seg[2].IsMatch));
        Assert.Equal(("ate", false), (seg[3].Text, seg[3].IsMatch));

        var merged = FuzzyMatcher.ToSegments("abcdef", new[] { 1, 2, 3 });
        Assert.Equal(3, merged.Count);
        Assert.Equal(("bcd", true), (merged[1].Text, merged[1].IsMatch));

        var none = FuzzyMatcher.ToSegments("abc", Array.Empty<int>());
        Assert.Single(none);
        Assert.False(none[0].IsMatch);
        Assert.Equal("abc", none[0].Text);

        var all = FuzzyMatcher.ToSegments("abc", new[] { 0, 1, 2 });
        Assert.Single(all);
        Assert.True(all[0].IsMatch);

        var outOfRange = FuzzyMatcher.ToSegments("abc", new[] { 5, -1, 2, 2 });
        Assert.Equal(2, outOfRange.Count);
        Assert.Equal(("c", true), (outOfRange[1].Text, outOfRange[1].IsMatch));
    }
}
