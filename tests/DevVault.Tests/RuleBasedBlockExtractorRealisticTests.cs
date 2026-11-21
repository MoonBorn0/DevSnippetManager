using DevSnippetManager.Services.ProjectScan;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace DevVault.Tests;

public class RuleBasedBlockExtractorRealisticTests
{
    private readonly RuleBasedBlockExtractor _extractor = new();

    private static string ReadRealistic(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "scan", "realistic", fileName);
        Assert.True(File.Exists(path), $"Realistic sample not found: {path}");
        return File.ReadAllText(path);
    }

    private static ExtractedBlock GetBlock(ExtractionResult result, string symbol)
    {
        var block = result.Blocks.FirstOrDefault(b => b.Symbol == symbol);
        Assert.True(block is not null, $"Expected symbol '{symbol}' not found. Actual: [{string.Join(", ", result.Blocks.Select(b => b.Symbol))}]");
        return block!;
    }

    [Fact]
    public void Realistic_JavaScript_AppJs()
    {
        var result = _extractor.Extract(ReadRealistic("app.js"), "JavaScript");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "add",
                "greet",
                "double",
                "Component",
                "withDefaults",
                "default",
                "Animal.constructor",
                "Animal.speak",
                "Animal.create",
                "Animal.label",
                "Animal.handle",
                "Dog.speak"
            },
            result.Blocks.Select(b => b.Symbol));

        var add = GetBlock(result, "add");
        Assert.StartsWith("function add", add.Code);
        Assert.EndsWith("}", add.Code);
        Assert.NotNull(add.Comment);
        Assert.Contains("Adds two numbers.", add.Comment);
        Assert.Contains("- a", add.Comment);
        Assert.Contains("- b", add.Comment);
        Assert.DoesNotContain("@param", add.Comment);

        var comp = GetBlock(result, "Component");
        Assert.StartsWith("function Component", comp.Code);
        Assert.EndsWith("}", comp.Code);

        var handle = GetBlock(result, "Animal.handle");
        Assert.StartsWith("handle = ", handle.Code);
        Assert.EndsWith("};", handle.Code);
    }

    [Fact]
    public void Realistic_TypeScript_ServiceTs()
    {
        var result = _extractor.Extract(ReadRealistic("service.ts"), "TypeScript");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "User",
                "Role",
                "UserService.constructor",
                "UserService.findById",
                "UserService.parse",
                "UserService.format",
                "UserService.size",
                "load",
                "configure",
                "makeId",
                "Comp"
            },
            result.Blocks.Select(b => b.Symbol));

        var configure = GetBlock(result, "configure");
        Assert.StartsWith("export function configure", configure.Code);
        Assert.EndsWith("}", configure.Code);

        var comp = GetBlock(result, "Comp");
        Assert.StartsWith("export function Comp", comp.Code);
        Assert.EndsWith("}", comp.Code);
    }

    [Fact]
    public void Realistic_Go_ServerGo()
    {
        var result = _extractor.Extract(ReadRealistic("server.go"), "Go");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Server",
                "Handler",
                "NewServer",
                "Server.Start",
                "Server.Done",
                "Describe",
                "Stack",
                "Stack.Push",
                "Map",
                "Server.String",
                "RuneTest"
            },
            result.Blocks.Select(b => b.Symbol));

        var done = GetBlock(result, "Server.Done");
        Assert.StartsWith("func (s *Server) Done", done.Code);
        Assert.EndsWith("}", done.Code);

        var describe = GetBlock(result, "Describe");
        Assert.StartsWith("func Describe", describe.Code);
        Assert.EndsWith("}", describe.Code);

        var push = GetBlock(result, "Stack.Push");
        Assert.StartsWith("func (st *Stack[T]) Push", push.Code);
        Assert.EndsWith("}", push.Code);
    }

    [Fact]
    public void Realistic_Cpp_ShapesCpp()
    {
        var result = _extractor.Extract(ReadRealistic("shapes.cpp"), "C++");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Stack.push",
                "Stack.pop",
                "Shape.Shape",
                "Shape.~Shape",
                "Shape.compute",
                "Shape.raw",
                "Circle.area",
                "Pair",
                "geo_helper",
                "Free.method",
                "main"
            },
            result.Blocks.Select(b => b.Symbol));

        var pop = GetBlock(result, "Stack.pop");
        Assert.StartsWith("template", pop.Code);
        Assert.EndsWith("}", pop.Code);

        var area = GetBlock(result, "Circle.area");
        Assert.StartsWith("double area", area.Code);
        Assert.EndsWith("}", area.Code);
    }

    [Fact]
    public void Realistic_Python_InventoryPy()
    {
        var result = _extractor.Extract(ReadRealistic("inventory.py"), "Python");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "timed",
                "parse",
                "total",
                "Inventory.__init__",
                "Inventory.size",
                "Inventory.size#2",
                "Inventory.make",
                "Inventory.fetch",
                "Inventory.Meta",
                "Inventory.long_string",
                "tail"
            },
            result.Blocks.Select(b => b.Symbol));

        var parse = GetBlock(result, "parse");
        Assert.StartsWith("@timed", parse.Code);
        Assert.DoesNotContain("# comment for total", parse.Code);

        var longStr = GetBlock(result, "Inventory.long_string");
        Assert.Contains("def not_a_def():", longStr.Code);
        Assert.DoesNotContain("def tail", longStr.Code);

        var tail = GetBlock(result, "tail");
        Assert.StartsWith("def tail", tail.Code);
    }

    [Fact]
    public void Realistic_Ruby_ShopRb()
    {
        var result = _extractor.Extract(ReadRealistic("shop.rb"), "Ruby");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Cart.initialize",
                "Cart.add",
                "Cart.total",
                "Cart.description",
                "Cart.build",
                "Cart.default",
                "Cart.secret",
                "Cart.odd?",
                "Shop.helper",
                "top_level",
                "last_one"
            },
            result.Blocks.Select(b => b.Symbol));

        var secret = GetBlock(result, "Cart.secret");
        Assert.StartsWith("private def secret", secret.Code);
        Assert.EndsWith("end", secret.Code);
    }

    [Fact]
    public void Realistic_PowerShell_ToolsPs1()
    {
        var result = _extractor.Extract(ReadRealistic("tools.ps1"), "PowerShell");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Get-Thing",
                "Set-Thing",
                "Only-Even",
                "Invoke-Work",
                "Person.Person",
                "Person.Greet",
                "Last-One"
            },
            result.Blocks.Select(b => b.Symbol));

        var getThing = GetBlock(result, "Get-Thing");
        Assert.StartsWith("function Get-Thing", getThing.Code);
        Assert.EndsWith("}", getThing.Code);
        Assert.NotNull(getThing.Comment);
        Assert.NotEqual(">", getThing.Comment);
        Assert.Contains("thing", getThing.Comment, StringComparison.OrdinalIgnoreCase);

        var greet = GetBlock(result, "Person.Greet");
        Assert.StartsWith("[string] Greet", greet.Code);
        Assert.EndsWith("}", greet.Code);
    }

    [Fact]
    public void Realistic_Sql_SchemaSql()
    {
        var result = _extractor.Extract(ReadRealistic("schema.sql"), "SQL");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "customers",
                "GetOrders",
                "AddNumbers",
                "v_active",
                "pg_add",
                "audit",
                "trg_audit"
            },
            result.Blocks.Select(b => b.Symbol));

        var addNumbers = GetBlock(result, "AddNumbers");
        Assert.NotNull(addNumbers.Comment);
        Assert.DoesNotContain("CREATE VIEW", addNumbers.Code);

        var vActive = GetBlock(result, "v_active");
        Assert.DoesNotContain("INSERT INTO", vActive.Code, StringComparison.OrdinalIgnoreCase);

        var trg = GetBlock(result, "trg_audit");
        Assert.DoesNotContain("DROP TABLE", trg.Code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", trg.Code, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Realistic_Php_PagePhp()
    {
        var result = _extractor.Extract(ReadRealistic("page.php"), "PHP");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "UserController.__construct",
                "UserController.show",
                "UserController.make",
                "UserController.helper",
                "render_list",
                "Shape",
                "Loggable.log",
                "after_html"
            },
            result.Blocks.Select(b => b.Symbol));

        var afterHtml = GetBlock(result, "after_html");
        Assert.StartsWith("function after_html", afterHtml.Code);
        Assert.EndsWith("}", afterHtml.Code);
        Assert.DoesNotContain("<html>", afterHtml.Code);
    }

    [Fact]
    public void Realistic_Rust_LibRs()
    {
        var result = _extractor.Extract(ReadRealistic("lib.rs"), "Rust");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Counter",
                "Event",
                "Describe",
                "Counter.new",
                "Counter.add",
                "Counter.brace_test",
                "Counter.fmt",
                "Vec.describe",
                "parse_kv",
                "fetch",
                "adds"
            },
            result.Blocks.Select(b => b.Symbol));

        var counter = GetBlock(result, "Counter");
        Assert.StartsWith("#[derive", counter.Code);
        Assert.EndsWith("}", counter.Code);

        var adds = GetBlock(result, "adds");
        Assert.StartsWith("#[test]", adds.Code);
        Assert.EndsWith("}", adds.Code);
    }

    [Fact]
    public void Realistic_Kotlin_RepoKt()
    {
        var result = _extractor.Extract(ReadRealistic("Repo.kt"), "Kotlin");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Repo.add",
                "Repo.find",
                "Repo.load",
                "Repo.create",
                "Singleton.hello",
                "String.shout",
                "List.second",
                "main",
                "Handler"
            },
            result.Blocks.Select(b => b.Symbol));

        var create = GetBlock(result, "Repo.create");
        Assert.StartsWith("fun create", create.Code);
        Assert.EndsWith("}", create.Code);
    }

    [Fact]
    public void Realistic_Swift_ShopSwift()
    {
        var result = _extractor.Extract(ReadRealistic("Shop.swift"), "Swift");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Cart.init",
                "Cart.add",
                "Cart.total",
                "Cart.helper",
                "Point",
                "Direction",
                "Shape",
                "Cart.clear",
                "free",
                "generic"
            },
            result.Blocks.Select(b => b.Symbol));

        var init = GetBlock(result, "Cart.init");
        Assert.StartsWith("init", init.Code);
        Assert.DoesNotContain("import Foundation", init.Code);
    }

    [Fact]
    public void Realistic_C_UtilC()
    {
        var result = _extractor.Extract(ReadRealistic("util.c"), "C");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Point",
                "Node",
                "max_int",
                "print_point",
                "node_new",
                "sum_list",
                "debug_dump",
                "main"
            },
            result.Blocks.Select(b => b.Symbol));

        var point = GetBlock(result, "Point");
        Assert.StartsWith("typedef struct", point.Code);
        Assert.Equal(BlockKind.Struct, point.Kind);
    }

    [Fact]
    public void Realistic_Bash_DeploySh()
    {
        var result = _extractor.Extract(ReadRealistic("deploy.sh"), "Bash");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "usage",
                "log",
                "count_args",
                "write_config",
                "strip_prefix",
                "check",
                "main"
            },
            result.Blocks.Select(b => b.Symbol));

        var usage = GetBlock(result, "usage");
        Assert.DoesNotContain("set -euo pipefail", usage.Code);
        Assert.EndsWith("}", usage.Code);
    }

    [Fact]
    public void Realistic_Java_OrderJava()
    {
        var result = _extractor.Extract(ReadRealistic("Order.java"), "Java");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "Order.Order",
                "Order.add",
                "Order.add#2",
                "Order.total",
                "Order.max",
                "Order.report",
                "Order.Builder.name",
                "Order.Builder.build",
                "Order.Visitor"
            },
            result.Blocks.Select(b => b.Symbol));
    }

    [Theory]
    [InlineData("b1.sh", "Bash", new[] { "foo", "bar", "baz" })]
    [InlineData("b2.sh", "Bash", new[] { "foo", "baz" })]
    [InlineData("b3.sh", "Bash", new[] { "bar", "baz" })]
    [InlineData("b4.sh", "Bash", new[] { "foo", "bar" })]
    [InlineData("c1.cpp", "C++", new[] { "A.area", "A.f", "A.g", "A.h" })]
    [InlineData("d1.jsx", "JavaScript", new[] { "Comp", "Page", "Button" })]
    [InlineData("g1.go", "Go", new[] { "Foo", "Bar", "Baz" })]
    [InlineData("n1.go", "Go", new[] { "f" })]
    [InlineData("n1.js", "JavaScript", new[] { "f" })]
    [InlineData("n1.kt", "Kotlin", new[] { "f" })]
    [InlineData("n1.sh", "Bash", new[] { "foo", "bar" })]
    [InlineData("n1.swift", "Swift", new[] { "f" })]
    [InlineData("r1.rs", "Rust", new[] { "Counter", "Event", "Describe", "Counter.new", "Counter.add", "Counter.brace_test", "Counter.fmt", "Vec.describe", "parse_kv", "fetch", "adds" })]
    [InlineData("r2.rs", "Rust", new[] { "Counter", "Event", "Describe", "Counter.new", "Counter.add", "Counter.fmt", "Vec.describe", "parse_kv", "fetch", "adds" })]
    public void Probes_ExtractExpectedSymbolsWithoutErrors(string fileName, string language, string[] expectedSymbols)
    {
        var result = _extractor.Extract(ReadRealistic(fileName), language);
        Assert.False(result.HasErrors, $"HasErrors was true for {fileName}");
        Assert.Equal(expectedSymbols, result.Blocks.Select(b => b.Symbol));
    }

    [Fact]
    public void Probes_NoPrecedingStatementGluedIntoHeader()
    {
        var go = _extractor.Extract(ReadRealistic("n1.go"), "Go");
        Assert.StartsWith("func f()", go.Blocks[0].Code);

        var js = _extractor.Extract(ReadRealistic("n1.js"), "JavaScript");
        Assert.StartsWith("function f()", js.Blocks[0].Code);

        var kt = _extractor.Extract(ReadRealistic("n1.kt"), "Kotlin");
        Assert.StartsWith("fun f()", kt.Blocks[0].Code);

        var sh = _extractor.Extract(ReadRealistic("n1.sh"), "Bash");
        Assert.StartsWith("foo()", sh.Blocks[0].Code);
        Assert.StartsWith("bar()", sh.Blocks[1].Code);

        var swift = _extractor.Extract(ReadRealistic("n1.swift"), "Swift");
        Assert.StartsWith("func f()", swift.Blocks[0].Code);
    }

    [Fact]
    public void Performance_6000JavaScriptFunctionsExtractUnderTwoSeconds()
    {
        var sb = new StringBuilder(600_000);
        for (int i = 0; i < 6000; i++)
        {
            sb.Append("// Function ").Append(i).AppendLine();
            sb.Append("function fn_").Append(i).AppendLine("(a, b) {");
            sb.AppendLine("  const x = a + b;");
            sb.AppendLine("  const y = x * 2;");
            sb.AppendLine("  return y;");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        var source = sb.ToString();
        var sw = Stopwatch.StartNew();
        var result = _extractor.Extract(source, "JavaScript");
        sw.Stop();

        Assert.False(result.HasErrors);
        Assert.Equal(6000, result.Blocks.Count);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Extraction took {sw.Elapsed.TotalMilliseconds:F1} ms (limit: 2000 ms)");
    }

    [Fact]
    public void JsDoc_FormatsParamAndReturnsAndDropsNoiseTags()
    {
        const string js = """
            /**
             * İki sayıyı çarpar.
             * @param {number} x Birinci sayı
             * @param {number} y İkinci sayı
             * @returns {number} Çarpım sonucu
             * @deprecated Eski API
             * @since 1.0.0
             */
            export function mul(x, y) {
              return x * y;
            }
            """;

        var result = _extractor.Extract(js, "JavaScript");
        Assert.False(result.HasErrors);
        var block = Assert.Single(result.Blocks);
        Assert.Equal("mul", block.Symbol);
        Assert.Equal("İki sayıyı çarpar.\n- x: Birinci sayı\n- y: İkinci sayı\nDöndürür: Çarpım sonucu", block.Comment);
    }

    [Fact]
    public void Realistic_Tsx_TodoListTsx_HandlesClosingAndSelfClosingTags()
    {
        var result = _extractor.Extract(ReadRealistic("TodoList.tsx"), "TypeScript");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[] { "TodoItem", "useTodos", "TodoList", "EmptyBadge" },
            result.Blocks.Select(b => b.Symbol));

        var list = GetBlock(result, "TodoList");
        Assert.StartsWith("export function TodoList", list.Code);
        Assert.EndsWith("}", list.Code);
        Assert.Contains("</li>", list.Code);
        Assert.Contains("<EmptyBadge />", list.Code);

        var badge = GetBlock(result, "EmptyBadge");
        Assert.StartsWith("export const EmptyBadge", badge.Code);
        Assert.EndsWith("};", badge.Code);
    }

    [Fact]
    public void Realistic_Jsx_TodoCardJsx_HandlesClosingAndSelfClosingTags()
    {
        var result = _extractor.Extract(ReadRealistic("TodoCard.jsx"), "JavaScript");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[] { "TodoCard", "UrgentIcon" },
            result.Blocks.Select(b => b.Symbol));

        var card = GetBlock(result, "TodoCard");
        Assert.StartsWith("export function TodoCard", card.Code);
        Assert.EndsWith("}", card.Code);
        Assert.Contains("<UrgentIcon />", card.Code);
        Assert.Contains("</li>", card.Code);

        var icon = GetBlock(result, "UrgentIcon");
        Assert.StartsWith("export const UrgentIcon", icon.Code);
        Assert.EndsWith("};", icon.Code);
    }

    [Fact]
    public void Java_SpringControllerAnnotationsDoNotStealSymbolName()
    {
        const string java = """
            package com.example.api;

            import org.springframework.web.bind.annotation.*;

            @RestController
            @RequestMapping("/api/users")
            public class UserController {
                private final UserService service;

                public UserController(UserService service) {
                    this.service = service;
                    System.out.println("init");
                }

                @GetMapping("/{id}")
                public ResponseEntity<User> get(@PathVariable Long id) {
                    User u = service.find(id);
                    return ResponseEntity.ok(u);
                }

                @PostMapping(value = "", consumes = "application/json")
                public ResponseEntity<User> create(@RequestBody @Valid UserDto dto) {
                    User saved = service.save(dto);
                    return ResponseEntity.ok(saved);
                }

                @DeleteMapping("/{id}")
                public void delete(@PathVariable("id") Long id) {
                    service.remove(id);
                    System.out.println("deleted");
                }
            }
            """;

        var result = _extractor.Extract(java, "Java");
        Assert.False(result.HasErrors);
        Assert.Equal(
            new[]
            {
                "UserController.UserController",
                "UserController.get",
                "UserController.create",
                "UserController.delete"
            },
            result.Blocks.Select(b => b.Symbol));

        var get = GetBlock(result, "UserController.get");
        Assert.StartsWith("@GetMapping(\"/{id}\")", get.Code);
        Assert.EndsWith("}", get.Code);

        var create = GetBlock(result, "UserController.create");
        Assert.StartsWith("@PostMapping", create.Code);
        Assert.EndsWith("}", create.Code);
    }
}
