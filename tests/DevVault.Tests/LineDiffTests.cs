using DevSnippetManager.Services;
using System.Linq;
using Xunit;

namespace DevVault.Tests;

public class LineDiffTests
{
    [Fact]
    public void Compute_IdenticalAndNormalizedLineEndings()
    {
        var diff = LineDiff.Compute("a\r\nb\r\nc", "a\nb\nc");
        Assert.Equal(3, diff.Count);
        Assert.All(diff, d => Assert.Equal(DiffLineKind.Unchanged, d.Kind));
    }

    [Fact]
    public void Compute_DetectsAddedRemovedAndUnchanged()
    {
        var diff = LineDiff.Compute("a\nb\nc", "a\nx\nc\nd");
        Assert.Equal(1, diff.Count(d => d.Kind == DiffLineKind.Removed && d.Text == "b"));
        Assert.Equal(1, diff.Count(d => d.Kind == DiffLineKind.Added && d.Text == "x"));
        Assert.Equal(1, diff.Count(d => d.Kind == DiffLineKind.Added && d.Text == "d"));
        Assert.Equal(2, diff.Count(d => d.Kind == DiffLineKind.Unchanged));
    }

    [Fact]
    public void Compute_HandlesEmptyInputs()
    {
        Assert.Empty(LineDiff.Compute("", ""));
        Assert.Equal(2, LineDiff.Compute("", "x\ny").Count(d => d.Kind == DiffLineKind.Added));
        Assert.Equal(2, LineDiff.Compute("x\ny", "").Count(d => d.Kind == DiffLineKind.Removed));
    }

    [Fact]
    public void FilterContext_KeepsTwoLinesAroundChanges()
    {
        var oldText = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"line{i}"));
        var newText = string.Join("\n", Enumerable.Range(1, 10).Select(i => i == 5 ? "changed5" : $"line{i}"));

        var full = LineDiff.Compute(oldText, newText);
        var filtered = LineDiff.FilterContext(full, contextLines: 2);

        Assert.DoesNotContain(filtered, d => d.Text == "line1");
        Assert.DoesNotContain(filtered, d => d.Text == "line2");
        Assert.Contains(filtered, d => d.Text == "line3");
        Assert.Contains(filtered, d => d.Text == "line4");
        Assert.Contains(filtered, d => d.Text == "line5" && d.Kind == DiffLineKind.Removed);
        Assert.Contains(filtered, d => d.Text == "changed5" && d.Kind == DiffLineKind.Added);
        Assert.Contains(filtered, d => d.Text == "line6");
        Assert.Contains(filtered, d => d.Text == "line7");
        Assert.DoesNotContain(filtered, d => d.Text == "line8");
    }

    [Fact]
    public void Compute_LargeInputsUseFastFallbackAndReconstructBothSides()
    {
        var oldLines = Enumerable.Range(0, 20_000).Select(i => i is 0 or 19_999 ? $"common-{i}" : $"old-{i}").ToArray();
        var newLines = Enumerable.Range(0, 20_000).Select(i => i is 0 or 19_999 ? $"common-{i}" : $"new-{i}").ToArray();
        var oldText = string.Join("\n", oldLines);
        var newText = string.Join("\n", newLines);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var diff = LineDiff.Compute(oldText, newText);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, $"Expected < 1000ms, took {sw.ElapsedMilliseconds}ms");

        var reconstructedOld = diff.Where(d => d.Kind != DiffLineKind.Added).Select(d => d.Text).ToArray();
        var reconstructedNew = diff.Where(d => d.Kind != DiffLineKind.Removed).Select(d => d.Text).ToArray();
        Assert.Equal(oldLines, reconstructedOld);
        Assert.Equal(newLines, reconstructedNew);
    }
}
