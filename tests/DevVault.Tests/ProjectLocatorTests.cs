using DevSnippetManager.Services.ProjectScan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DevVault.Tests;

public sealed class ProjectLocatorTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"DevVaultProjectLocatorTests_{Guid.NewGuid():N}");

    public ProjectLocatorTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void FindProjectRoot_StopsAtCsprojOrGit()
    {
        var project = Directory.CreateDirectory(Path.Combine(_tempRoot, "Project")).FullName;
        var sourceFolder = Directory.CreateDirectory(Path.Combine(project, "src")).FullName;
        var sourceFile = Path.Combine(sourceFolder, "Example.cs");
        File.WriteAllText(sourceFile, "class Example { }");
        File.WriteAllText(Path.Combine(project, "Example.csproj"), "<Project />");
        Directory.CreateDirectory(Path.Combine(project, ".git"));

        Assert.Equal(project, ProjectLocator.FindProjectRoot(sourceFile));

        File.Delete(Path.Combine(project, "Example.csproj"));
        Assert.Equal(project, ProjectLocator.FindProjectRoot(sourceFile));
    }

    [Fact]
    public void FindProjectRoot_FallsBackToFileFolder()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_tempRoot, "NoProject")).FullName;
        var sourceFile = Path.Combine(folder, "Example.py");
        File.WriteAllText(sourceFile, "def example():\n    pass");

        Assert.Equal(folder, ProjectLocator.FindProjectRoot(sourceFile));
    }

    [Fact]
    public void EnumerateSourceFiles_SkipsBinObjNodeModulesAndGenerated()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempRoot, "Sources")).FullName;
        File.WriteAllText(Path.Combine(root, "Keep.cs"), "class Keep { }");
        File.WriteAllText(Path.Combine(root, "Skip.g.cs"), "class Generated { }");
        File.WriteAllText(Path.Combine(root, "Skip.Designer.cs"), "class Designer { }");
        File.WriteAllText(Path.Combine(root, "Skip.generated.js"), "function generated() { }");
        File.WriteAllText(Path.Combine(root, "Skip.min.js"), "function min() { }");
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not source");

        foreach (var skippedDirectory in new[] { "bin", "obj", "node_modules", "Migrations" })
        {
            var directory = Directory.CreateDirectory(Path.Combine(root, skippedDirectory));
            File.WriteAllText(Path.Combine(directory.FullName, "Hidden.cs"), "class Hidden { }");
        }

        var files = ProjectLocator.EnumerateSourceFiles(root);

        Assert.Equal(new[] { Path.Combine(root, "Keep.cs") }, files);
    }

    [Theory]
    [InlineData(".cs", "C#")]
    [InlineData(".java", "Java")]
    [InlineData(".js", "JavaScript")]
    [InlineData(".jsx", "JavaScript")]
    [InlineData(".mjs", "JavaScript")]
    [InlineData(".cjs", "JavaScript")]
    [InlineData(".ts", "TypeScript")]
    [InlineData(".tsx", "TypeScript")]
    [InlineData(".c", "C")]
    [InlineData(".h", "C")]
    [InlineData(".cpp", "C++")]
    [InlineData(".hpp", "C++")]
    [InlineData(".cc", "C++")]
    [InlineData(".cxx", "C++")]
    [InlineData(".go", "Go")]
    [InlineData(".rs", "Rust")]
    [InlineData(".php", "PHP")]
    [InlineData(".kt", "Kotlin")]
    [InlineData(".kts", "Kotlin")]
    [InlineData(".swift", "Swift")]
    [InlineData(".py", "Python")]
    [InlineData(".rb", "Ruby")]
    [InlineData(".ps1", "PowerShell")]
    [InlineData(".psm1", "PowerShell")]
    [InlineData(".sh", "Bash")]
    [InlineData(".bash", "Bash")]
    [InlineData(".sql", "SQL")]
    public void LanguageFromPath_MapsAllExtensions(string extension, string expectedLanguage)
    {
        Assert.Equal(expectedLanguage, ProjectLocator.LanguageFromPath("source" + extension));
    }

    [Fact]
    public void ProjectFolderName_DisambiguatesSameName()
    {
        var root = Path.Combine(_tempRoot, "KodDepolama", "src");
        var existingRoot = Path.Combine(_tempRoot, "OtherProject", "src");

        Assert.Equal("src (KodDepolama)", ProjectLocator.ProjectFolderName(root,
            new List<(string Root, string Folder)> { (existingRoot, "src") }));
    }

    [Fact]
    public void PathEquals_IgnoresCaseAndTrailingSlash()
    {
        var path = Path.Combine(_tempRoot, "Folder");
        Assert.True(ProjectLocator.PathEquals(path + Path.DirectorySeparatorChar, path.ToUpperInvariant()));
    }

    [Fact]
    public void Dedent_RemovesCommonIndentAndNormalizesNewlines()
    {
        var code = "\r\n    first  \r\n      second\r\n";

        Assert.Equal("\nfirst\n  second\n", CodeNormalizer.Dedent(code));
    }

    [Fact]
    public void ContentHash_SameForCrlfAndLf()
    {
        Assert.Equal(CodeNormalizer.ContentHash("  first\r\n  second\r\n"),
            CodeNormalizer.ContentHash("  first\n  second\n"));
    }

    [Fact]
    public void BlockKindTags_AllKindsHaveTurkishTag()
    {
        var expected = new[] { "metot", "fonksiyon", "sınıf", "arayüz", "kayıt", "yapı", "enum", "özellik", "kurucu", "prosedür" };

        Assert.Equal("proje-tarama", BlockKindTags.ScanTag);
        Assert.Equal(expected, Enum.GetValues<BlockKind>().Select(BlockKindTags.ToTag));
    }
}
