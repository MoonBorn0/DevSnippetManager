using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DevSnippetManager.Services.ProjectScan;

public sealed class CSharpBlockExtractor : IBlockExtractor
{
    public bool CanHandle(string language)
    {
        return string.Equals(language, "C#", StringComparison.OrdinalIgnoreCase);
    }

    public ExtractionResult Extract(string source, string language)
    {
        var text = source ?? string.Empty;
        var tree = CSharpSyntaxTree.ParseText(text);
        var hasErrors = tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error);
        var root = tree.GetCompilationUnitRoot();

        var blocks = new List<ExtractedBlock>();
        var symbolCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        VisitMembers(root.Members, nsPrefix: string.Empty, typeChain: string.Empty, text, tree, blocks, symbolCounts);

        return new ExtractionResult(blocks, hasErrors);
    }

    private static void VisitMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        string nsPrefix,
        string typeChain,
        string source,
        SyntaxTree tree,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case BaseNamespaceDeclarationSyntax ns:
                    var nextNs = string.IsNullOrEmpty(nsPrefix)
                        ? ns.Name.ToString().Trim()
                        : $"{nsPrefix}.{ns.Name.ToString().Trim()}";
                    VisitMembers(ns.Members, nextNs, typeChain, source, tree, blocks, symbolCounts);
                    break;

                case EnumDeclarationSyntax enumDecl:
                    AddBlock(enumDecl, Combine(typeChain, enumDecl.Identifier.Text), nsPrefix, BlockKind.Enum, source, tree, blocks, symbolCounts);
                    break;

                case InterfaceDeclarationSyntax ifaceDecl:
                    AddBlock(ifaceDecl, Combine(typeChain, ifaceDecl.Identifier.Text), nsPrefix, BlockKind.Interface, source, tree, blocks, symbolCounts);
                    break;

                case RecordDeclarationSyntax recordDecl:
                    AddBlock(recordDecl, Combine(typeChain, recordDecl.Identifier.Text), nsPrefix, BlockKind.Record, source, tree, blocks, symbolCounts);
                    break;

                case StructDeclarationSyntax structDecl:
                    AddBlock(structDecl, Combine(typeChain, structDecl.Identifier.Text), nsPrefix, BlockKind.Struct, source, tree, blocks, symbolCounts);
                    break;

                case ClassDeclarationSyntax classDecl:
                    VisitClass(classDecl, nsPrefix, typeChain, source, tree, blocks, symbolCounts);
                    break;
            }
        }
    }

    private static void VisitClass(
        ClassDeclarationSyntax classDecl,
        string nsPrefix,
        string outerTypeChain,
        string source,
        SyntaxTree tree,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var currentTypeChain = Combine(outerTypeChain, classDecl.Identifier.Text);
        var countBefore = blocks.Count;
        var hasExtractableChild = false;

        foreach (var member in classDecl.Members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    if (method.Body == null && method.ExpressionBody == null)
                        continue;
                    hasExtractableChild = true;
                    var methodParams = FormatParameters(method.ParameterList);
                    var methodDisplay = $"{currentTypeChain}.{method.Identifier.Text}";
                    var methodSymbolBase = Combine(nsPrefix, $"{methodDisplay}({methodParams})");
                    AddBlockWithSymbol(method, methodSymbolBase, methodDisplay, BlockKind.Method, source, tree, blocks, symbolCounts);
                    break;

                case ConstructorDeclarationSyntax ctor:
                    if (ctor.Body == null && ctor.ExpressionBody == null)
                        continue;
                    hasExtractableChild = true;
                    var ctorParams = FormatParameters(ctor.ParameterList);
                    var ctorDisplay = $"{currentTypeChain} (kurucu)";
                    var ctorSymbolBase = Combine(nsPrefix, $"{currentTypeChain}..ctor({ctorParams})");
                    AddBlockWithSymbol(ctor, ctorSymbolBase, ctorDisplay, BlockKind.Constructor, source, tree, blocks, symbolCounts);
                    break;

                case PropertyDeclarationSyntax prop:
                    if (!HasPropertyBody(prop))
                        continue;
                    hasExtractableChild = true;
                    var propDisplay = $"{currentTypeChain}.{prop.Identifier.Text}";
                    var propSymbolBase = Combine(nsPrefix, propDisplay);
                    AddBlockWithSymbol(prop, propSymbolBase, propDisplay, BlockKind.Property, source, tree, blocks, symbolCounts);
                    break;

                case IndexerDeclarationSyntax indexer:
                    if (!HasIndexerBody(indexer))
                        continue;
                    hasExtractableChild = true;
                    var idxParams = FormatBracketedParameters(indexer.ParameterList);
                    var idxDisplay = $"{currentTypeChain}.this[]";
                    var idxSymbolBase = Combine(nsPrefix, $"{currentTypeChain}.this[{idxParams}]");
                    AddBlockWithSymbol(indexer, idxSymbolBase, idxDisplay, BlockKind.Property, source, tree, blocks, symbolCounts);
                    break;

                case OperatorDeclarationSyntax op:
                    if (op.Body == null && op.ExpressionBody == null)
                        continue;
                    hasExtractableChild = true;
                    var opParams = FormatParameters(op.ParameterList);
                    var opDisplay = $"{currentTypeChain}.operator {op.OperatorToken.Text}";
                    var opSymbolBase = Combine(nsPrefix, $"{opDisplay}({opParams})");
                    AddBlockWithSymbol(op, opSymbolBase, opDisplay, BlockKind.Method, source, tree, blocks, symbolCounts);
                    break;

                case ConversionOperatorDeclarationSyntax conv:
                    if (conv.Body == null && conv.ExpressionBody == null)
                        continue;
                    hasExtractableChild = true;
                    var convParams = FormatParameters(conv.ParameterList);
                    var convDisplay = $"{currentTypeChain}.operator {conv.Type}";
                    var convSymbolBase = Combine(nsPrefix, $"{convDisplay}({convParams})");
                    AddBlockWithSymbol(conv, convSymbolBase, convDisplay, BlockKind.Method, source, tree, blocks, symbolCounts);
                    break;

                case BaseTypeDeclarationSyntax nestedType:
                    hasExtractableChild = true;
                    VisitMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(nestedType), nsPrefix, currentTypeChain, source, tree, blocks, symbolCounts);
                    break;
            }
        }

        if (!hasExtractableChild && blocks.Count == countBefore)
        {
            AddBlock(classDecl, currentTypeChain, nsPrefix, BlockKind.Class, source, tree, blocks, symbolCounts);
        }
    }

    private static bool HasPropertyBody(PropertyDeclarationSyntax prop)
    {
        if (prop.ExpressionBody != null)
            return true;

        if (prop.AccessorList == null)
            return false;

        return prop.AccessorList.Accessors.Any(a => a.Body != null || a.ExpressionBody != null);
    }

    private static bool HasIndexerBody(IndexerDeclarationSyntax indexer)
    {
        if (indexer.ExpressionBody != null)
            return true;

        if (indexer.AccessorList == null)
            return false;

        return indexer.AccessorList.Accessors.Any(a => a.Body != null || a.ExpressionBody != null);
    }

    private static void AddBlock(
        SyntaxNode node,
        string displayName,
        string nsPrefix,
        BlockKind kind,
        string source,
        SyntaxTree tree,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var symbolBase = Combine(nsPrefix, displayName);
        AddBlockWithSymbol(node, symbolBase, displayName, kind, source, tree, blocks, symbolCounts);
    }

    private static void AddBlockWithSymbol(
        SyntaxNode node,
        string symbolBase,
        string displayName,
        BlockKind kind,
        string source,
        SyntaxTree tree,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var rawCode = ExtractNodeCodeWithIndent(node, source);
        var dedented = CodeNormalizer.Dedent(rawCode).Trim('\n');

        var nonEmptyLines = dedented.Split('\n').Count(line => line.Trim().Length > 0);
        if (nonEmptyLines < 3)
            return;

        symbolCounts.TryGetValue(symbolBase, out var seenCount);
        seenCount++;
        symbolCounts[symbolBase] = seenCount;
        var symbol = seenCount == 1 ? symbolBase : $"{symbolBase}#{seenCount}";

        var comment = ExtractComment(node);
        var startLine = tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

        blocks.Add(new ExtractedBlock(symbol, displayName, kind, dedented, comment, startLine));
    }

    private static string ExtractNodeCodeWithIndent(SyntaxNode node, string source)
    {
        var start = node.Span.Start;
        var lineStart = start;
        while (lineStart > 0 && source[lineStart - 1] is ' ' or '\t')
            lineStart--;

        return source[lineStart..node.Span.End];
    }

    private static string ExtractComment(SyntaxNode node)
    {
        var triviaList = node.GetLeadingTrivia();
        if (triviaList.Count == 0)
            return string.Empty;

        // 1. Prefer XML doc comments
        var xmlTrivia = triviaList.LastOrDefault(t =>
            t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        if (xmlTrivia != default)
        {
            var parsed = ParseXmlDoc(xmlTrivia.ToFullString());
            if (parsed.Length > 0)
                return parsed;
        }

        // 2. Fall back to contiguous // or /* */ comments directly above the node
        var commentLines = new List<string>();
        var consecutiveNewlines = 0;

        for (var i = triviaList.Count - 1; i >= 0; i--)
        {
            var t = triviaList[i];
            if (t.IsKind(SyntaxKind.WhitespaceTrivia))
                continue;

            if (t.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                consecutiveNewlines++;
                if (consecutiveNewlines > 1 && commentLines.Count > 0)
                    break;
                continue;
            }

            if (t.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                consecutiveNewlines = 0;
                var line = t.ToString().TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal))
                    line = line[2..].Trim();
                commentLines.Add(line);
                continue;
            }

            if (t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                consecutiveNewlines = 0;
                var raw = t.ToString().Trim();
                if (raw.StartsWith("/*", StringComparison.Ordinal) && raw.EndsWith("*/", StringComparison.Ordinal))
                    raw = raw[2..^2];
                var cleaned = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
                    .Select(l => l.Trim().TrimStart('*').Trim())
                    .Where(l => l.Length > 0)
                    .Reverse();
                commentLines.AddRange(cleaned);
                continue;
            }

            // Any other trivia (e.g. preprocessor directive) stops comment collection
            break;
        }

        if (commentLines.Count == 0)
            return string.Empty;

        commentLines.Reverse();
        return string.Join('\n', commentLines).Trim();
    }

    private static string ParseXmlDoc(string rawXmlTrivia)
    {
        var lines = rawXmlTrivia.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(l =>
            {
                var trimmed = l.TrimStart();
                if (trimmed.StartsWith("///", StringComparison.Ordinal))
                    return trimmed[3..];
                if (trimmed.StartsWith("/**", StringComparison.Ordinal))
                    trimmed = trimmed[3..];
                if (trimmed.EndsWith("*/", StringComparison.Ordinal))
                    trimmed = trimmed[..^2];
                return trimmed.TrimStart('*');
            });

        var xmlBody = string.Join('\n', lines).Trim();
        if (xmlBody.Length == 0)
            return string.Empty;

        try
        {
            var doc = XElement.Parse($"<root>{xmlBody}</root>");
            var parts = new List<string>();

            var summary = ExtractXmlElementText(doc.Element("summary"));
            if (summary.Length > 0)
                parts.Add(summary);

            foreach (var param in doc.Elements("param"))
            {
                var name = param.Attribute("name")?.Value?.Trim();
                var text = ExtractXmlElementText(param);
                if (!string.IsNullOrEmpty(name) && text.Length > 0)
                    parts.Add($"- {name}: {text}");
            }

            var returns = ExtractXmlElementText(doc.Element("returns"));
            if (returns.Length > 0)
                parts.Add($"Döndürür: {returns}");

            return string.Join('\n', parts);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractXmlElementText(XElement? element)
    {
        if (element == null)
            return string.Empty;

        var sb = new StringBuilder();
        AppendXmlNodeText(element, sb);
        return NormalizeXmlText(sb.ToString());
    }

    private static void AppendXmlNodeText(XNode node, StringBuilder sb)
    {
        switch (node)
        {
            case XText text:
                sb.Append(text.Value);
                break;

            case XElement el:
                var localName = el.Name.LocalName;
                if (string.Equals(localName, "see", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(localName, "seealso", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(el.Value))
                    {
                        sb.Append(el.Value);
                    }
                    else
                    {
                        var cref = el.Attribute("cref")?.Value?.Trim();
                        if (!string.IsNullOrEmpty(cref))
                        {
                            if (cref.Length > 2 && cref[1] == ':' && char.IsLetter(cref[0]))
                                cref = cref[2..];
                            sb.Append(cref);
                        }
                    }
                }
                else if (string.Equals(localName, "paramref", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(localName, "typeparamref", StringComparison.OrdinalIgnoreCase))
                {
                    var name = el.Attribute("name")?.Value?.Trim();
                    if (!string.IsNullOrEmpty(name))
                        sb.Append(name);
                }
                else
                {
                    foreach (var child in el.Nodes())
                        AppendXmlNodeText(child, sb);
                }
                break;
        }
    }

    private static string NormalizeXmlText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var parts = value.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        return string.Join(' ', parts);
    }

    private static string FormatParameters(ParameterListSyntax? parameterList)
    {
        if (parameterList == null || parameterList.Parameters.Count == 0)
            return string.Empty;

        return string.Join(",", parameterList.Parameters.Select(p => p.Type?.ToString().Trim() ?? string.Empty));
    }

    private static string FormatBracketedParameters(BracketedParameterListSyntax? parameterList)
    {
        if (parameterList == null || parameterList.Parameters.Count == 0)
            return string.Empty;

        return string.Join(",", parameterList.Parameters.Select(p => p.Type?.ToString().Trim() ?? string.Empty));
    }

    private static string Combine(string prefix, string name)
    {
        return string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
    }
}
