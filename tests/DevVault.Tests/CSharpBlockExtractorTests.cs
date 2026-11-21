using DevSnippetManager.Services.ProjectScan;
using System.Linq;
using Xunit;

namespace DevVault.Tests;

public class CSharpBlockExtractorTests
{
    private readonly CSharpBlockExtractor _extractor = new();

    [Fact]
    public void Extract_MethodsCtorsPropertiesAsSeparateBlocks()
    {
        const string code = """
            namespace Shop.Orders
            {
                public class OrderService
                {
                    private decimal _tax;

                    public OrderService(decimal tax)
                    {
                        _tax = tax;
                    }

                    public decimal Tax
                    {
                        get { return _tax; }
                        set { _tax = value; }
                    }

                    public decimal Total(decimal price)
                    {
                        return price + _tax;
                    }
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");

        Assert.False(result.HasErrors);
        Assert.Equal(3, result.Blocks.Count);

        var ctor = Assert.Single(result.Blocks, b => b.Kind == BlockKind.Constructor);
        Assert.Equal("Shop.Orders.OrderService..ctor(decimal)", ctor.Symbol);
        Assert.Equal("OrderService (kurucu)", ctor.DisplayName);

        var prop = Assert.Single(result.Blocks, b => b.Kind == BlockKind.Property);
        Assert.Equal("Shop.Orders.OrderService.Tax", prop.Symbol);
        Assert.Equal("OrderService.Tax", prop.DisplayName);

        var method = Assert.Single(result.Blocks, b => b.Kind == BlockKind.Method);
        Assert.Equal("Shop.Orders.OrderService.Total(decimal)", method.Symbol);
        Assert.Equal("OrderService.Total", method.DisplayName);
        Assert.StartsWith("public decimal Total", method.Code);
    }

    [Fact]
    public void Extract_OverloadsGetParameterTypesInSymbol()
    {
        const string code = """
            namespace MathLib;

            public class Calc
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }

                public double Add(double a, double b)
                {
                    return a + b;
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        Assert.Equal(2, result.Blocks.Count);
        Assert.Contains(result.Blocks, b => b.Symbol == "MathLib.Calc.Add(int,int)");
        Assert.Contains(result.Blocks, b => b.Symbol == "MathLib.Calc.Add(double,double)");
    }

    [Fact]
    public void Extract_ClassWithoutMembersTakenWhole()
    {
        const string code = """
            namespace Models
            {
                public class Config
                {
                    public string Host = "localhost";
                    public int Port = 5432;
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        var block = Assert.Single(result.Blocks);
        Assert.Equal(BlockKind.Class, block.Kind);
        Assert.Equal("Models.Config", block.Symbol);
        Assert.Equal("Config", block.DisplayName);
    }

    [Fact]
    public void Extract_EnumInterfaceRecordStructWholeOnly()
    {
        const string code = """
            namespace Domain
            {
                public enum Status
                {
                    Pending,
                    Done
                }

                public interface IStore
                {
                    void Save(string key);
                    string Load(string key);
                }

                public record Item(int Id, string Name)
                {
                    public string Label()
                    {
                        return $"{Id}:{Name}";
                    }
                }

                public struct Point
                {
                    public int X;
                    public int Y;
                    public int Sum()
                    {
                        return X + Y;
                    }
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        Assert.Equal(4, result.Blocks.Count);
        Assert.Contains(result.Blocks, b => b.Kind == BlockKind.Enum && b.Symbol == "Domain.Status");
        Assert.Contains(result.Blocks, b => b.Kind == BlockKind.Interface && b.Symbol == "Domain.IStore");
        Assert.Contains(result.Blocks, b => b.Kind == BlockKind.Record && b.Symbol == "Domain.Item");
        Assert.Contains(result.Blocks, b => b.Kind == BlockKind.Struct && b.Symbol == "Domain.Point");
    }

    [Fact]
    public void Extract_NestedTypesUseDottedSymbol()
    {
        const string code = """
            namespace App
            {
                public class Outer
                {
                    public class Inner
                    {
                        public void Run()
                        {
                            var x = 1;
                        }
                    }
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        var block = Assert.Single(result.Blocks);
        Assert.Equal("App.Outer.Inner.Run()", block.Symbol);
        Assert.Equal("Outer.Inner.Run", block.DisplayName);
    }

    [Fact]
    public void Extract_LocalFunctionStaysInsideParent()
    {
        const string code = """
            public class Helper
            {
                public int Compute(int n)
                {
                    int LocalDouble(int x)
                    {
                        return x * 2;
                    }
                    return LocalDouble(n);
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        var block = Assert.Single(result.Blocks);
        Assert.Equal("Helper.Compute(int)", block.Symbol);
        Assert.Contains("LocalDouble", block.Code);
    }

    [Fact]
    public void Extract_XmlDocBecomesComment()
    {
        const string code = """
            public class Greeter
            {
                /// <summary>
                /// Kullanıcıyı selamlar.
                /// </summary>
                /// <param name="name">Kullanıcı adı</param>
                /// <returns>Selamlama metni</returns>
                public string Greet(string name)
                {
                    return $"Merhaba {name}";
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        var block = Assert.Single(result.Blocks);
        Assert.Equal("Kullanıcıyı selamlar.\n- name: Kullanıcı adı\nDöndürür: Selamlama metni", block.Comment);
        Assert.DoesNotContain("///", block.Code);
    }

    [Fact]
    public void Extract_LineCommentsAboveBecomeComment()
    {
        const string code = """
            public class Worker
            {
                // Birinci satır
                // İkinci satır
                public void Work()
                {
                    var a = 1;
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        var block = Assert.Single(result.Blocks);
        Assert.Equal("Birinci satır\nİkinci satır", block.Comment);
        Assert.DoesNotContain("Birinci satır", block.Code);
    }

    [Fact]
    public void Extract_SkipsBlocksShorterThan3Lines()
    {
        const string code = """
            public class Short
            {
                public int OneLiner() => 42;
                public int TwoLiner()
                { return 1; }
                public int ThreeLiner()
                {
                    return 3;
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        var block = Assert.Single(result.Blocks);
        Assert.Equal("Short.ThreeLiner()", block.Symbol);
    }

    [Fact]
    public void Extract_SyntaxErrorSetsHasErrors()
    {
        const string code = """
            public class Broken
            {
                public void Good()
                {
                    var x = 1;
                }
                public void Bad(
            }
            """;

        var result = _extractor.Extract(code, "C#");
        Assert.True(result.HasErrors);
        Assert.Contains(result.Blocks, b => b.Symbol == "Broken.Good()");
    }

    [Fact]
    public void Extract_IgnoresBracesInStringsAndComments()
    {
        const string code = """
            public class Tricky
            {
                public string Format(int a)
                {
                    var s1 = $"{a}}}";
                    var s2 = @"}";
                    /* } */
                    return s1 + s2;
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        Assert.False(result.HasErrors);
        var block = Assert.Single(result.Blocks);
        Assert.Equal("Tricky.Format(int)", block.Symbol);
        Assert.Contains("return s1 + s2;", block.Code);
    }

    [Fact]
    public void Extract_XmlDocPreservesSeeCref()
    {
        const string code = """
            public class Runner
            {
                /// <summary>
                /// <see cref="OrderService"/> ile <see cref="T:Shop.Item"/> işler.
                /// </summary>
                public void Run()
                {
                    var x = 1;
                }
            }
            """;

        var result = _extractor.Extract(code, "C#");
        var block = Assert.Single(result.Blocks);
        Assert.Equal("OrderService ile Shop.Item işler.", block.Comment);
    }
}
