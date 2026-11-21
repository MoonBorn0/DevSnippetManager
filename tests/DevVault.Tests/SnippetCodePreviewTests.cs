using KodDepolama;
using Xunit;

namespace DevVault.Tests;

public class SnippetCodePreviewTests
{
    [Fact]
    public void CodePreview_ReturnsEmptyForBlankCode()
    {
        Assert.Equal(string.Empty, new Snippet { Code = "" }.CodePreview);
        Assert.Equal(string.Empty, new Snippet { Code = "   \r\n  \n\t" }.CodePreview);
    }

    [Fact]
    public void CodePreview_TakesMaxThreeNonEmptyLinesAndStripsCommonIndent()
    {
        var snippet = new Snippet
        {
            Code = "\n    line1\n\n      line2\n    line3\n    line4"
        };

        Assert.Equal("line1\n  line2\nline3", snippet.CodePreview);
    }

    [Fact]
    public void CodePreview_TruncatesLinesLongerThan80Chars()
    {
        var exact80 = new string('a', 80);
        var len81 = new string('b', 81);
        var len100 = new string('c', 100);

        Assert.Equal(exact80, new Snippet { Code = exact80 }.CodePreview);
        Assert.Equal(80, new Snippet { Code = len81 }.CodePreview.Length);
        Assert.EndsWith("…", new Snippet { Code = len81 }.CodePreview);
        Assert.Equal(80, new Snippet { Code = len100 }.CodePreview.Length);
        Assert.EndsWith("…", new Snippet { Code = len100 }.CodePreview);
    }

    [Fact]
    public void CodePreview_HandlesNullTabsCrLfLoneCrAndIndentFromShownLinesOnly()
    {
        Assert.Equal(string.Empty, new Snippet { Code = null! }.CodePreview);
        Assert.Equal("a\n\tb\nc", new Snippet { Code = "\ta\r\n\t\tb\r\tc" }.CodePreview);
        Assert.Equal("a\n b", new Snippet { Code = " \ta\n \t b" }.CodePreview);

        // 4th line has 0 indent, but only first 3 non-empty lines determine common indent (4 spaces)
        var snippet = new Snippet { Code = "    one\n      two\n    three\nzeroIndentLine4" };
        Assert.Equal("one\n  two\nthree", snippet.CodePreview);

        // Truncation happens AFTER indent removal (10 spaces + 80 chars -> 80 chars, not truncated)
        var padded80 = new string(' ', 10) + new string('x', 80);
        Assert.Equal(new string('x', 80), new Snippet { Code = padded80 }.CodePreview);
    }
}
