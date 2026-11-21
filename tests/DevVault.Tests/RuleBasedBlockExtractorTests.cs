using DevSnippetManager.Services.ProjectScan;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DevVault.Tests;

public class RuleBasedBlockExtractorTests
{
    private readonly RuleBasedBlockExtractor _extractor = new();

    private static string ReadSample(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "scan", fileName);
        Assert.True(File.Exists(path), $"Sample not found: {path}");
        return File.ReadAllText(path);
    }

    [Fact]
    public void Java_ClassMethodsAndOverloads()
    {
        var result = _extractor.Extract(ReadSample("sample.java"), "Java");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "Calculator.add", "Calculator.add#2" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal("İki tamsayıyı toplar", result.Blocks[0].Comment);
        Assert.StartsWith("public int add", result.Blocks[0].Code);
    }

    [Fact]
    public void JavaScript_FunctionsArrowsClassesExportDefault()
    {
        var result = _extractor.Extract(ReadSample("sample.js"), "JavaScript");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "multiply", "formatName", "Cart.addItem", "default" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal("İki sayıyı çarpar", result.Blocks[0].Comment);
    }

    [Fact]
    public void TypeScript_InterfacesTypedMethods()
    {
        var result = _extractor.Extract(ReadSample("sample.ts"), "TypeScript");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "User", "UserService.findById" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal(BlockKind.Interface, result.Blocks[0].Kind);
        Assert.Equal("Kullanıcıyı bulur", result.Blocks[1].Comment);
    }

    [Fact]
    public void C_FunctionsAndStructs()
    {
        var result = _extractor.Extract(ReadSample("sample.c"), "C");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "Point", "sum_point" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal(BlockKind.Struct, result.Blocks[0].Kind);
        Assert.Equal("Nokta toplamını hesaplar", result.Blocks[1].Comment);
    }

    [Fact]
    public void Cpp_ClassMethodsAndNamespaces()
    {
        var result = _extractor.Extract(ReadSample("sample.cpp"), "C++");
        Assert.False(result.HasErrors);
        var block = Assert.Single(result.Blocks);
        Assert.Equal("VectorOps.sum", block.Symbol);
        Assert.Equal("Elemanları toplar", block.Comment);
    }

    [Fact]
    public void Go_FuncsAndMethodsWithReceivers()
    {
        var result = _extractor.Extract(ReadSample("sample.go"), "Go");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "Server.Start", "HealthCheck" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal(BlockKind.Method, result.Blocks[0].Kind);
        Assert.Equal("Sunucuyu başlatır", result.Blocks[0].Comment);
    }

    [Fact]
    public void Rust_FnImplBlocks()
    {
        var result = _extractor.Extract(ReadSample("sample.rs"), "Rust");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "Counter", "Counter.increment", "reset_value" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal("Sayacı artırır", result.Blocks[1].Comment);
    }

    [Fact]
    public void Php_ClassMethods()
    {
        var result = _extractor.Extract(ReadSample("sample.php"), "PHP");
        Assert.False(result.HasErrors);
        var block = Assert.Single(result.Blocks);
        Assert.Equal("OrderController.show", block.Symbol);
        Assert.Equal("Sipariş özeti döner", block.Comment);
    }

    [Fact]
    public void Python_DefClassDecoratorsDocstringIndent()
    {
        var result = _extractor.Extract(ReadSample("sample.py"), "Python");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "compute_total", "Greeter.hello" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal("Toplam tutarı hesaplar.", result.Blocks[0].Comment);
        Assert.Contains("def helper", result.Blocks[0].Code);
        Assert.StartsWith("@staticmethod", result.Blocks[1].Code);
        Assert.Equal("Selamlama metni üretir.", result.Blocks[1].Comment);
    }

    [Fact]
    public void Ruby_DefClassEnd()
    {
        var result = _extractor.Extract(ReadSample("sample.rb"), "Ruby");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "Formatter.upcase_all", "greet" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal("Metni büyütür", result.Blocks[0].Comment);
    }

    [Fact]
    public void PowerShell_Functions()
    {
        var result = _extractor.Extract(ReadSample("sample.ps1"), "PowerShell");
        Assert.False(result.HasErrors);
        var block = Assert.Single(result.Blocks);
        Assert.Equal("Clear-OldLogs", block.Symbol);
        Assert.Equal("Günlük dosyalarını temizler", block.Comment);
    }

    [Fact]
    public void Bash_Functions()
    {
        var result = _extractor.Extract(ReadSample("sample.sh"), "Bash");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "backup_dir", "deploy_app" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal("Yedek arşivi oluşturur", result.Blocks[0].Comment);
    }

    [Fact]
    public void Sql_ProceduresFunctionsViewsUntilGo()
    {
        var result = _extractor.Extract(ReadSample("sample.sql"), "SQL");
        Assert.False(result.HasErrors);
        Assert.Equal(new[] { "GetActiveUsers", "ActiveOrders" }, result.Blocks.Select(b => b.Symbol));
        Assert.Equal("Aktif kullanıcıları listeler", result.Blocks[0].Comment);
        Assert.DoesNotContain("GO", result.Blocks[0].Code);
    }

    [Fact]
    public void Extract_IgnoresBracesInStringsAndComments()
    {
        const string js = """
            function tricky(a) {
                const s1 = `${a}}`;
                const s2 = "}";
                // }
                const re = /re}/g;
                return s1 + s2 + re.source;
            }
            """;

        var result = _extractor.Extract(js, "JavaScript");
        Assert.False(result.HasErrors);
        var block = Assert.Single(result.Blocks);
        Assert.Equal("tricky", block.Symbol);
        Assert.Contains("return s1 + s2 + re.source;", block.Code);
    }

    [Fact]
    public void Extract_UnbalancedBracesSetsHasErrors()
    {
        const string broken = """
            function good() {
                const x = 1;
                return x;
            }
            function bad() {
                const y = 2;
            """;

        var result = _extractor.Extract(broken, "JavaScript");
        Assert.True(result.HasErrors);
        Assert.Contains(result.Blocks, b => b.Symbol == "good");
    }

    [Fact]
    public void Extract_AnonymousCallbacksNotExtracted()
    {
        const string js = """
            items.map(x => {
                const y = x * 2;
                return y;
            });

            (() => {
                const init = 1;
                console.log(init);
            })();

            function realOne() {
                const a = 1;
                return a;
            }
            """;

        var result = _extractor.Extract(js, "JavaScript");
        Assert.False(result.HasErrors);
        var block = Assert.Single(result.Blocks);
        Assert.Equal("realOne", block.Symbol);
    }
}
