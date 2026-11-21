using DevSnippetManager.Services;
using System.Collections.Generic;
using Xunit;

namespace DevVault.Tests;

public class LanguageCatalogTests
{
    [Fact]
    public void BuiltInLanguages_HasExpected23Entries()
    {
        Assert.Equal(23, LanguageCatalog.BuiltInLanguages.Count);
    }

    [Theory]
    [InlineData("C#", "csharp")]
    [InlineData("Java", "java")]
    [InlineData("Python", "python")]
    [InlineData("SQL", "sql")]
    [InlineData("JavaScript", "javascript")]
    [InlineData("TypeScript", "typescript")]
    [InlineData("HTML/CSS", "html")]
    [InlineData("C", "c")]
    [InlineData("C++", "cpp")]
    [InlineData("Go", "go")]
    [InlineData("Rust", "rust")]
    [InlineData("PHP", "php")]
    [InlineData("Ruby", "ruby")]
    [InlineData("Kotlin", "kotlin")]
    [InlineData("Swift", "swift")]
    [InlineData("Bash", "shell")]
    [InlineData("PowerShell", "powershell")]
    [InlineData("JSON", "json")]
    [InlineData("YAML", "yaml")]
    [InlineData("XML", "xml")]
    [InlineData("Markdown", "markdown")]
    [InlineData("Dockerfile", "dockerfile")]
    [InlineData("Diğer", "plaintext")]
    [InlineData("UnknownLang", "plaintext")]
    [InlineData("", "plaintext")]
    [InlineData(null, "plaintext")]
    public void GetMonacoId_MapsAllBuiltInAndFallbackCorrectly(string? language, string expectedId)
    {
        Assert.Equal(expectedId, LanguageCatalog.GetMonacoId(language));
    }

    [Theory]
    [InlineData(null, "Diğer")]
    [InlineData("", "Diğer")]
    [InlineData("   ", "Diğer")]
    [InlineData("c#", "C#")]
    [InlineData("  python ", "Python")]
    [InlineData("  Elixir  ", "Elixir")]
    public void Normalize_ReturnsCanonicalOrTrimmedName(string? input, string expected)
    {
        Assert.Equal(expected, LanguageCatalog.Normalize(input));
    }
}
