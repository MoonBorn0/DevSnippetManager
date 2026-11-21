using DevSnippetManager.Services;
using System.Collections.Generic;
using Xunit;

namespace DevVault.Tests;

public class TemplateServiceTests
{
    private static readonly Dictionary<string, string> Empty = new();

    [Fact]
    public void Parse_DistinctAndDefaultValues()
    {
        var vars = TemplateService.Parse("a ${A} b ${A} c ${B:x y: z}");
        Assert.Equal(2, vars.Count);
        Assert.Equal("A", vars[0].Name);
        Assert.Equal(string.Empty, vars[0].DefaultValue);
        Assert.Equal("B", vars[1].Name);
        Assert.Equal("x y: z", vars[1].DefaultValue);

        var firstDefault = TemplateService.Parse("${A:one} ${A:two} ${A}");
        Assert.Single(firstDefault);
        Assert.Equal("one", firstDefault[0].DefaultValue);

        var firstEmpty = TemplateService.Parse("${A} ${A:two}");
        Assert.Single(firstEmpty);
        Assert.Equal(string.Empty, firstEmpty[0].DefaultValue);
    }

    [Fact]
    public void ParseAndFill_TurkishIdentifiers()
    {
        var vars = TemplateService.Parse("SELECT * FROM ${Tabloİsmi} WHERE ${şart:1=1}");
        Assert.Equal(2, vars.Count);
        Assert.Equal("Tabloİsmi", vars[0].Name);
        Assert.Equal("şart", vars[1].Name);
        Assert.Equal("1=1", vars[1].DefaultValue);

        var filled = TemplateService.Fill("FROM ${Tabloİsmi}", new Dictionary<string, string> { ["Tabloİsmi"] = "Müşteri" });
        Assert.Equal("FROM Müşteri", filled);
    }

    [Fact]
    public void Fill_RepeatsAndDoesNotReExpand()
    {
        Assert.Equal("z-z", TemplateService.Fill("${A}-${A}", new Dictionary<string, string> { ["A"] = "z" }));
        Assert.Equal("${B} q", TemplateService.Fill("${A} ${B:q}", new Dictionary<string, string> { ["A"] = "${B}" }));
        Assert.Equal("def ", TemplateService.Fill("${A:def} ${B}", Empty));
    }

    [Fact]
    public void EscapeSequence_ProducesLiteralPlaceholder()
    {
        Assert.Empty(TemplateService.Parse("$${A}"));
        Assert.Equal("${A} q", TemplateService.Fill("$${A} ${A:q}", Empty));
        Assert.Equal("${A}v", TemplateService.Fill("$${A}${A}", new Dictionary<string, string> { ["A"] = "v" }));
        Assert.Single(TemplateService.Parse("${A}$${B}"));
    }

    [Fact]
    public void InvalidOrEdgePlaceholders_HandledSafely()
    {
        Assert.Empty(TemplateService.Parse(null));
        Assert.Empty(TemplateService.Parse(""));
        Assert.Equal("", TemplateService.Fill("", Empty));
        Assert.Empty(TemplateService.Parse("${A"));
        Assert.Equal("x ${A", TemplateService.Fill("x ${A", Empty));
        Assert.Empty(TemplateService.Parse("${A:abc"));
        Assert.Empty(TemplateService.Parse("`${a.b}`"));
        Assert.Empty(TemplateService.Parse("${1+1}"));
        Assert.Empty(TemplateService.Parse("${1a}"));
        Assert.Single(TemplateService.Parse("${_a}"));
        Assert.Equal(2, TemplateService.Parse("${a} ${A}").Count);
        Assert.Equal("x{ }", TemplateService.Fill("${A}{ }", new Dictionary<string, string> { ["A"] = "x" }));
        Assert.Equal("[]", TemplateService.Fill("[${A:}]", Empty));
        Assert.Single(TemplateService.Parse("${A:x\ny}"));
    }
}
