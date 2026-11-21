using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevSnippetManager.Services.ProjectScan;

public sealed class RuleBasedBlockExtractor : IBlockExtractor
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Java", "JavaScript", "TypeScript", "C", "C++", "Go", "Rust", "PHP",
        "Kotlin", "Swift", "Python", "Ruby", "PowerShell", "Bash", "SQL"
    };

    private static readonly HashSet<string> ControlKeywords = new(StringComparer.Ordinal)
    {
        "if", "else", "for", "foreach", "while", "do", "switch", "case", "catch", "try",
        "finally", "synchronized", "lock", "using", "return", "sizeof", "typeof", "match",
        "when", "with", "guard", "defer", "select", "struct", "class", "interface", "enum",
        "record", "namespace", "impl", "trait", "mod", "where"
    };

    public bool CanHandle(string language)
    {
        return !string.IsNullOrWhiteSpace(language) && SupportedLanguages.Contains(language);
    }

    public ExtractionResult Extract(string source, string language)
    {
        var text = source ?? string.Empty;
        if (text.Length == 0 || !CanHandle(language))
            return new ExtractionResult(Array.Empty<ExtractedBlock>(), false);

        var masked = SourceMasker.Mask(text, language);
        var blocks = new List<ExtractedBlock>();
        var symbolCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var hasErrors = false;

        if (string.Equals(language, "Python", StringComparison.OrdinalIgnoreCase))
        {
            ExtractPython(text, masked, blocks, symbolCounts);
        }
        else if (string.Equals(language, "Ruby", StringComparison.OrdinalIgnoreCase))
        {
            hasErrors = ExtractRuby(text, masked, blocks, symbolCounts);
        }
        else if (string.Equals(language, "SQL", StringComparison.OrdinalIgnoreCase))
        {
            ExtractSql(text, masked, blocks, symbolCounts);
        }
        else
        {
            hasErrors = !AreBracesBalanced(masked);
            var lineStarts = ComputeLineStarts(text);
            ExtractBraceRange(text, masked, language, 0, masked.Length, classPrefix: string.Empty, lineStarts, blocks, symbolCounts);
        }

        return new ExtractionResult(blocks, hasErrors);
    }

    private static bool AreBracesBalanced(string masked)
    {
        var depth = 0;
        for (var i = 0; i < masked.Length; i++)
        {
            if (masked[i] == '{') depth++;
            else if (masked[i] == '}')
            {
                depth--;
                if (depth < 0) return false;
            }
        }
        return depth == 0;
    }

    private static int FindMatchingBrace(string masked, int openIndex, int endLimit)
    {
        var depth = 0;
        for (var i = openIndex; i < endLimit; i++)
        {
            if (masked[i] == '{') depth++;
            else if (masked[i] == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static void ExtractBraceRange(
        string source,
        string masked,
        string language,
        int rangeStart,
        int rangeEnd,
        string classPrefix,
        int[] lineStarts,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var pos = rangeStart;
        while (pos < rangeEnd)
        {
            var openIdx = masked.IndexOf('{', pos, rangeEnd - pos);
            if (openIdx < 0)
                break;

            var closeIdx = FindMatchingBrace(masked, openIdx, rangeEnd);
            if (closeIdx < 0)
                break;

            var headerStart = FindHeaderStart(masked, rangeStart, openIdx);
            var header = masked[headerStart..openIdx].Trim();

            if (TryClassifyBraceHeader(header, language, classPrefix, out var name, out var kind, out var isContainer, out var isTransparentNamespace))
            {
                var blockStartLine = GetLineNumber(lineStarts, headerStart);
                var blockStart = BackUpToLineIndent(source, headerStart);
                var blockEnd = closeIdx + 1;
                if (blockEnd < source.Length && source[blockEnd] == ';')
                    blockEnd++;

                if (isTransparentNamespace)
                {
                    ExtractBraceRange(source, masked, language, openIdx + 1, closeIdx, classPrefix, lineStarts, blocks, symbolCounts);
                }
                else if (isContainer)
                {
                    var nextPrefix = string.IsNullOrEmpty(classPrefix) ? name : $"{classPrefix}.{name}";
                    var countBefore = blocks.Count;
                    ExtractBraceRange(source, masked, language, openIdx + 1, closeIdx, nextPrefix, lineStarts, blocks, symbolCounts);
                    if (kind == BlockKind.Class && blocks.Count == countBefore)
                    {
                        var comment = ExtractPrecedingComment(source, lineStarts, blockStartLine, language);
                        AddBlock(nextPrefix, BlockKind.Class, source[blockStart..blockEnd], comment, blockStartLine, blocks, symbolCounts);
                    }
                }
                else
                {
                    var fullName = string.IsNullOrEmpty(classPrefix) || name.Contains('.', StringComparison.Ordinal)
                        ? name
                        : $"{classPrefix}.{name}";
                    var comment = ExtractPrecedingComment(source, lineStarts, blockStartLine, language);
                    AddBlock(fullName, kind, source[blockStart..blockEnd], comment, blockStartLine, blocks, symbolCounts);
                }
            }

            pos = closeIdx + 1;
        }
    }

    private static int FindHeaderStart(string masked, int rangeStart, int openBraceIdx)
    {
        var i = openBraceIdx - 1;
        var parenDepth = 0;
        var angleDepth = 0;
        var seenNonWhitespace = false;

        while (i >= rangeStart)
        {
            var c = masked[i];
            if (c == ')') parenDepth++;
            else if (c == '(') { if (parenDepth > 0) parenDepth--; }
            else if (c == '>' && (i == rangeStart || (masked[i - 1] != '-' && masked[i - 1] != '='))) angleDepth++;
            else if (c == '<') { if (angleDepth > 0) angleDepth--; }
            else if (parenDepth == 0 && angleDepth == 0)
            {
                if (c == ';' || c == '}' || c == '{')
                {
                    i++;
                    break;
                }

                // Stop at blank line once we've seen the header text
                if (c == '\n' && seenNonWhitespace)
                {
                    var prev = i - 1;
                    while (prev >= rangeStart && (masked[prev] == ' ' || masked[prev] == '\t' || masked[prev] == '\r'))
                        prev--;
                    if (prev >= rangeStart && masked[prev] == '\n')
                    {
                        i++;
                        break;
                    }
                }
            }

            if (!char.IsWhiteSpace(c))
                seenNonWhitespace = true;
            i--;
        }

        if (i < rangeStart) i = rangeStart;
        while (i < openBraceIdx && char.IsWhiteSpace(masked[i]))
            i++;

        // Strip leading preprocessor lines (#include ...) or C++ access specifiers (public:/private:/protected:)
        while (i < openBraceIdx)
        {
            var lineEnd = masked.IndexOf('\n', i, openBraceIdx - i);
            var segEnd = lineEnd >= 0 ? lineEnd : openBraceIdx;
            var lineText = masked[i..segEnd].Trim();
            if (lineText.StartsWith("#", StringComparison.Ordinal) ||
                lineText is "public:" or "private:" or "protected:")
            {
                i = lineEnd >= 0 ? lineEnd + 1 : openBraceIdx;
                while (i < openBraceIdx && char.IsWhiteSpace(masked[i]))
                    i++;
                continue;
            }
            break;
        }

        return i;
    }

    private static bool TryClassifyBraceHeader(
        string header,
        string language,
        string classPrefix,
        out string name,
        out BlockKind kind,
        out bool isContainer,
        out bool isTransparentNamespace)
    {
        name = string.Empty;
        kind = BlockKind.Function;
        isContainer = false;
        isTransparentNamespace = false;

        if (string.IsNullOrWhiteSpace(header))
            return false;

        var collapsed = Regex.Replace(header, @"\s+", " ").Trim();

        // PowerShell
        if (string.Equals(language, "PowerShell", StringComparison.OrdinalIgnoreCase))
        {
            var psMatch = Regex.Match(collapsed, @"^(?:function|filter)\s+([\w-]+)\b", RegexOptions.IgnoreCase);
            if (psMatch.Success)
            {
                name = psMatch.Groups[1].Value;
                kind = BlockKind.Function;
                return true;
            }
            return false;
        }

        // Bash
        if (string.Equals(language, "Bash", StringComparison.OrdinalIgnoreCase))
        {
            var bashMatch = Regex.Match(collapsed, @"(?:^|\b)(?:function\s+([A-Za-z_][\w-]*)(?:\s*\(\s*\))?|([A-Za-z_][\w-]*)\s*\(\s*\))$");
            if (bashMatch.Success)
            {
                name = !string.IsNullOrEmpty(bashMatch.Groups[1].Value)
                    ? bashMatch.Groups[1].Value
                    : bashMatch.Groups[2].Value;
                kind = BlockKind.Function;
                return true;
            }
            return false;
        }

        // Namespace (C++, PHP)
        if (Regex.IsMatch(collapsed, @"^namespace\b"))
        {
            isTransparentNamespace = true;
            return true;
        }

        // Rust impl block
        if (string.Equals(language, "Rust", StringComparison.OrdinalIgnoreCase))
        {
            var implMatch = Regex.Match(collapsed, @"^impl(?:<[^>]*>)?\s+(?:[\w:]+(?:<[^>]*>)?\s+for\s+)?([A-Za-z_]\w*)");
            if (implMatch.Success)
            {
                name = implMatch.Groups[1].Value;
                kind = BlockKind.Struct;
                isContainer = true;
                return true;
            }

            var rustFn = Regex.Match(collapsed, @"\bfn\s+([A-Za-z_]\w*)\b");
            if (rustFn.Success)
            {
                name = rustFn.Groups[1].Value;
                kind = string.IsNullOrEmpty(classPrefix) ? BlockKind.Function : BlockKind.Method;
                return true;
            }

            var rustType = Regex.Match(collapsed, @"\b(struct|enum|trait)\s+([A-Za-z_]\w*)\b");
            if (rustType.Success)
            {
                name = rustType.Groups[2].Value;
                kind = rustType.Groups[1].Value switch
                {
                    "struct" => BlockKind.Struct,
                    "enum" => BlockKind.Enum,
                    _ => BlockKind.Interface
                };
                return true;
            }
            return false;
        }

        // Go
        if (string.Equals(language, "Go", StringComparison.OrdinalIgnoreCase))
        {
            var goMethod = Regex.Match(collapsed, @"^func\s*\(\s*\w+\s+\*?([A-Za-z_]\w*)\s*\)\s*([A-Za-z_]\w*)\s*\(");
            if (goMethod.Success)
            {
                name = $"{goMethod.Groups[1].Value}.{goMethod.Groups[2].Value}";
                kind = BlockKind.Method;
                return true;
            }

            var goFunc = Regex.Match(collapsed, @"^func\s+([A-Za-z_]\w*)\s*(?:\[[^\]]*\])?\s*\(");
            if (goFunc.Success)
            {
                name = goFunc.Groups[1].Value;
                kind = BlockKind.Function;
                return true;
            }

            var goType = Regex.Match(collapsed, @"^type\s+([A-Za-z_]\w*)\s+(struct|interface)\b");
            if (goType.Success)
            {
                name = goType.Groups[1].Value;
                kind = goType.Groups[2].Value == "struct" ? BlockKind.Struct : BlockKind.Interface;
                return true;
            }
            return false;
        }

        // Class container
        var classMatch = Regex.Match(collapsed, @"\bclass\s+([A-Za-z_]\w*)\b");
        if (classMatch.Success && !collapsed.Contains("=>", StringComparison.Ordinal))
        {
            name = classMatch.Groups[1].Value;
            kind = BlockKind.Class;
            isContainer = true;
            return true;
        }

        // Whole types: interface, enum, struct, record
        var typeMatch = Regex.Match(collapsed, @"\b(interface|enum|struct|record)\s+([A-Za-z_]\w*)\b");
        if (typeMatch.Success && !Regex.IsMatch(collapsed, @"\)\s*$"))
        {
            name = typeMatch.Groups[2].Value;
            kind = typeMatch.Groups[1].Value switch
            {
                "interface" => BlockKind.Interface,
                "enum" => BlockKind.Enum,
                "record" => BlockKind.Record,
                _ => BlockKind.Struct
            };
            return true;
        }

        // JS / TS specific
        if (string.Equals(language, "JavaScript", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "TypeScript", StringComparison.OrdinalIgnoreCase))
        {
            // export default function (...) {
            if (Regex.IsMatch(collapsed, @"^export\s+default\s+(?:async\s+)?function\s*\*?\s*\("))
            {
                name = "default";
                kind = BlockKind.Function;
                return true;
            }

            // (export)? (default)? (async)? function name(...) {
            var fnMatch = Regex.Match(collapsed, @"\bfunction\s*\*?\s*([A-Za-z_$][\w$]*)\s*(?:<[^>]*>)?\s*\(");
            if (fnMatch.Success)
            {
                name = fnMatch.Groups[1].Value;
                kind = string.IsNullOrEmpty(classPrefix) ? BlockKind.Function : BlockKind.Method;
                return true;
            }

            // const/let/var name = (async)? (...) => { or = function(...) {
            var varFn = Regex.Match(collapsed, @"^(?:export\s+)?(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*(?::[^=]+)?=\s*(?:async\s+)?(?:function\b|\([^)]*\)\s*(?::[^=]+)?=>|[A-Za-z_$][\w$]*\s*=>)");
            if (varFn.Success)
            {
                name = varFn.Groups[1].Value;
                kind = BlockKind.Function;
                return true;
            }

            // Class method inside classPrefix
            if (!string.IsNullOrEmpty(classPrefix))
            {
                var methodMatch = Regex.Match(collapsed, @"^(?:(?:public|private|protected|static|async|override|readonly|get|set)\s+)*([A-Za-z_$][\w$]*)\s*(?:<[^>]*>)?\s*\([^)]*\)(?:\s*:\s*.+)?$");
                if (methodMatch.Success && !ControlKeywords.Contains(methodMatch.Groups[1].Value))
                {
                    name = methodMatch.Groups[1].Value;
                    kind = name == "constructor" ? BlockKind.Constructor : BlockKind.Method;
                    return true;
                }
            }

            return false;
        }

        // PHP
        if (string.Equals(language, "PHP", StringComparison.OrdinalIgnoreCase))
        {
            var phpFn = Regex.Match(collapsed, @"\bfunction\s+&?\s*([A-Za-z_]\w*)\s*\(");
            if (phpFn.Success)
            {
                name = phpFn.Groups[1].Value;
                kind = string.IsNullOrEmpty(classPrefix) ? BlockKind.Function : BlockKind.Method;
                return true;
            }
            return false;
        }

        // Kotlin / Swift
        if (string.Equals(language, "Kotlin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "Swift", StringComparison.OrdinalIgnoreCase))
        {
            var ksFn = Regex.Match(collapsed, @"\bfunc?\s+(?:<[^>]*>\s*)?(?:([A-Za-z_]\w*)\.)?([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(");
            if (ksFn.Success)
            {
                name = !string.IsNullOrEmpty(ksFn.Groups[1].Value)
                    ? $"{ksFn.Groups[1].Value}.{ksFn.Groups[2].Value}"
                    : ksFn.Groups[2].Value;
                kind = string.IsNullOrEmpty(classPrefix) ? BlockKind.Function : BlockKind.Method;
                return true;
            }
            return false;
        }

        // Java / C / C++
        // Match function/method signature ending in (...) [const|noexcept|override|throws ...|: init(...)]
        var cLike = Regex.Match(collapsed, @"(?:([A-Za-z_]\w*)::)?([A-Za-z_~]\w*)\s*\([^;{}]*\)\s*(?:const|noexcept|override|final|throws\s+[\w.,\s]+|:\s*[^;{]+)*$");
        if (cLike.Success)
        {
            var qualifiedClass = cLike.Groups[1].Value;
            var fnName = cLike.Groups[2].Value;
            if (ControlKeywords.Contains(fnName))
                return false;

            // Avoid matching bare control statements like "if (x)" or "return foo()"
            var beforeMatch = collapsed[..cLike.Index].Trim();
            if (beforeMatch.EndsWith("return", StringComparison.Ordinal) ||
                beforeMatch.EndsWith("=", StringComparison.Ordinal))
                return false;

            if (!string.IsNullOrEmpty(qualifiedClass))
            {
                name = $"{qualifiedClass}.{fnName}";
                kind = BlockKind.Method;
            }
            else
            {
                name = fnName;
                kind = !string.IsNullOrEmpty(classPrefix)
                    ? (string.Equals(fnName, classPrefix.Split('.').Last(), StringComparison.Ordinal) ? BlockKind.Constructor : BlockKind.Method)
                    : BlockKind.Function;
            }
            return true;
        }

        return false;
    }

    private static void ExtractPython(
        string source,
        string masked,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var rawLines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var maskedLines = masked.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        ExtractPythonRange(rawLines, maskedLines, 0, rawLines.Length, classPrefix: string.Empty, blocks, symbolCounts);
    }

    private static void ExtractPythonRange(
        string[] rawLines,
        string[] maskedLines,
        int startLine,
        int endLine,
        string classPrefix,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var i = startLine;
        while (i < endLine)
        {
            var trimmed = maskedLines[i].Trim();
            var isClass = Regex.Match(trimmed, @"^class\s+([A-Za-z_]\w*)\b");
            var isDef = Regex.Match(trimmed, @"^(?:async\s+)?def\s+([A-Za-z_]\w*)\s*\(");

            if (!isClass.Success && !isDef.Success)
            {
                i++;
                continue;
            }

            var declIndent = GetIndent(rawLines[i]);

            // Include contiguous @decorators immediately above
            var blockStart = i;
            while (blockStart > startLine && maskedLines[blockStart - 1].TrimStart().StartsWith("@", StringComparison.Ordinal) &&
                   GetIndent(rawLines[blockStart - 1]) == declIndent)
            {
                blockStart--;
            }

            // Find end of block by indentation
            var j = i + 1;
            while (j < endLine)
            {
                if (rawLines[j].Trim().Length > 0 && maskedLines[j].Trim().Length > 0)
                {
                    if (GetIndent(rawLines[j]) <= declIndent)
                        break;
                }
                else if (rawLines[j].Trim().Length > 0 && GetIndent(rawLines[j]) <= declIndent && !rawLines[j].TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    // Triple-quoted docstring or string inside body vs top-level statement
                    if (j > i + 1 && GetIndent(rawLines[j]) == 0 && declIndent == 0)
                        break;
                }
                j++;
            }

            var blockEnd = j;
            while (blockEnd > i + 1 && rawLines[blockEnd - 1].Trim().Length == 0)
                blockEnd--;

            if (isClass.Success)
            {
                var className = isClass.Groups[1].Value;
                var nextPrefix = string.IsNullOrEmpty(classPrefix) ? className : $"{classPrefix}.{className}";
                var countBefore = blocks.Count;
                ExtractPythonRange(rawLines, maskedLines, i + 1, blockEnd, nextPrefix, blocks, symbolCounts);
                if (blocks.Count == countBefore)
                {
                    var comment = ExtractPythonComment(rawLines, blockStart, i, blockEnd);
                    var code = string.Join('\n', rawLines[blockStart..blockEnd]);
                    AddBlock(nextPrefix, BlockKind.Class, code, comment, blockStart + 1, blocks, symbolCounts);
                }
            }
            else
            {
                var fnName = isDef.Groups[1].Value;
                var fullName = string.IsNullOrEmpty(classPrefix) ? fnName : $"{classPrefix}.{fnName}";
                var kind = string.IsNullOrEmpty(classPrefix) ? BlockKind.Function : BlockKind.Method;
                var comment = ExtractPythonComment(rawLines, blockStart, i, blockEnd);
                var code = string.Join('\n', rawLines[blockStart..blockEnd]);
                AddBlock(fullName, kind, code, comment, blockStart + 1, blocks, symbolCounts);
            }

            i = j;
        }
    }

    private static string ExtractPythonComment(string[] rawLines, int blockStart, int defLine, int blockEnd)
    {
        // 1. Check docstring right after def/class header
        var bodyLine = defLine + 1;
        while (bodyLine < blockEnd && rawLines[bodyLine].Trim().Length == 0)
            bodyLine++;

        if (bodyLine < blockEnd)
        {
            var trimmed = rawLines[bodyLine].Trim();
            string? quote = trimmed.StartsWith("\"\"\"", StringComparison.Ordinal) ? "\"\"\""
                : trimmed.StartsWith("'''", StringComparison.Ordinal) ? "'''"
                : null;
            if (quote != null)
            {
                var afterOpen = trimmed[3..];
                if (afterOpen.EndsWith(quote, StringComparison.Ordinal) && afterOpen.Length >= 3)
                    return afterOpen[..^3].Trim();

                var docLines = new List<string>();
                if (afterOpen.Length > 0) docLines.Add(afterOpen.Trim());
                for (var k = bodyLine + 1; k < blockEnd; k++)
                {
                    var t = rawLines[k].Trim();
                    var endIdx = t.IndexOf(quote, StringComparison.Ordinal);
                    if (endIdx >= 0)
                    {
                        var before = t[..endIdx].Trim();
                        if (before.Length > 0) docLines.Add(before);
                        return string.Join('\n', docLines).Trim();
                    }
                    docLines.Add(t);
                }
            }
        }

        // 2. Fallback: contiguous # comments above blockStart
        var comments = new List<string>();
        for (var k = blockStart - 1; k >= 0; k--)
        {
            var t = rawLines[k].Trim();
            if (t.StartsWith("#", StringComparison.Ordinal))
                comments.Add(t[1..].Trim());
            else
                break;
        }
        comments.Reverse();
        return string.Join('\n', comments).Trim();
    }

    private static bool ExtractRuby(
        string source,
        string masked,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var rawLines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var maskedLines = masked.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var stack = new Stack<(string Kind, string Name, int StartLine, int BlocksBefore)>();
        var hasErrors = false;

        for (var i = 0; i < maskedLines.Length; i++)
        {
            var line = maskedLines[i].Trim();
            if (line.Length == 0) continue;

            var classMatch = Regex.Match(line, @"^(?:class|module)\s+([A-Za-z_][\w:]*)");
            if (classMatch.Success)
            {
                stack.Push(("class", classMatch.Groups[1].Value, i, blocks.Count));
                continue;
            }

            var defMatch = Regex.Match(line, @"^def\s+(?:self\.)?([A-Za-z_]\w*[!?=]?)");
            if (defMatch.Success)
            {
                stack.Push(("def", defMatch.Groups[1].Value, i, blocks.Count));
                continue;
            }

            if (Regex.IsMatch(line, @"^(?:if|unless|case|while|until|for|begin)\b") ||
                Regex.IsMatch(line, @"\bdo(?:\s*\|[^|]*\|)?\s*$"))
            {
                stack.Push(("other", string.Empty, i, blocks.Count));
                continue;
            }

            if (Regex.IsMatch(line, @"^end\b"))
            {
                if (stack.Count == 0)
                {
                    hasErrors = true;
                    continue;
                }

                var popped = stack.Pop();
                if (popped.Kind == "def")
                {
                    var enclosingClass = stack.FirstOrDefault(s => s.Kind == "class").Name;
                    var fullName = string.IsNullOrEmpty(enclosingClass) ? popped.Name : $"{enclosingClass}.{popped.Name}";
                    var kind = string.IsNullOrEmpty(enclosingClass) ? BlockKind.Function : BlockKind.Method;
                    var code = string.Join('\n', rawLines[popped.StartLine..(i + 1)]);
                    var comment = ExtractHashCommentAbove(rawLines, popped.StartLine);
                    AddBlock(fullName, kind, code, comment, popped.StartLine + 1, blocks, symbolCounts);
                }
                else if (popped.Kind == "class" && blocks.Count == popped.BlocksBefore)
                {
                    var code = string.Join('\n', rawLines[popped.StartLine..(i + 1)]);
                    var comment = ExtractHashCommentAbove(rawLines, popped.StartLine);
                    AddBlock(popped.Name, BlockKind.Class, code, comment, popped.StartLine + 1, blocks, symbolCounts);
                }
            }
        }

        if (stack.Count > 0)
            hasErrors = true;

        return hasErrors;
    }

    private static void ExtractSql(
        string source,
        string masked,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var rawLines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var maskedLines = masked.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var i = 0;
        while (i < maskedLines.Length)
        {
            var match = Regex.Match(
                maskedLines[i].Trim(),
                @"^CREATE\s+(?:OR\s+ALTER\s+)?(PROCEDURE|PROC|FUNCTION|VIEW|TRIGGER|TABLE)\s+(?:\[?[\w]+\]?\.)?\[?([A-Za-z_][\w]*)\]?",
                RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                i++;
                continue;
            }

            var objType = match.Groups[1].Value.ToUpperInvariant();
            var name = match.Groups[2].Value;
            var kind = objType == "FUNCTION" ? BlockKind.Function : BlockKind.Procedure;
            var start = i;
            var j = i + 1;

            while (j < maskedLines.Length)
            {
                var t = maskedLines[j].Trim();
                if (Regex.IsMatch(t, @"^GO\b", RegexOptions.IgnoreCase))
                    break;
                if (Regex.IsMatch(t, @"^CREATE\s+(?:OR\s+ALTER\s+)?(PROCEDURE|PROC|FUNCTION|VIEW|TRIGGER|TABLE)\b", RegexOptions.IgnoreCase))
                    break;
                j++;
            }

            var end = j;
            while (end > start + 1 && rawLines[end - 1].Trim().Length == 0)
                end--;

            var code = string.Join('\n', rawLines[start..end]);
            var comment = ExtractSqlCommentAbove(rawLines, start);
            AddBlock(name, kind, code, comment, start + 1, blocks, symbolCounts);

            i = j < maskedLines.Length && Regex.IsMatch(maskedLines[j].Trim(), @"^GO\b", RegexOptions.IgnoreCase) ? j + 1 : j;
        }
    }

    private static void AddBlock(
        string displayName,
        BlockKind kind,
        string rawCode,
        string comment,
        int startLine,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var dedented = CodeNormalizer.Dedent(rawCode).Trim('\n');
        var nonEmptyLines = dedented.Split('\n').Count(l => l.Trim().Length > 0);
        if (nonEmptyLines < 3)
            return;

        symbolCounts.TryGetValue(displayName, out var seen);
        seen++;
        symbolCounts[displayName] = seen;
        var symbol = seen == 1 ? displayName : $"{displayName}#{seen}";

        blocks.Add(new ExtractedBlock(symbol, displayName, kind, dedented, comment, startLine));
    }

    private static string ExtractPrecedingComment(string source, int[] lineStarts, int blockStartLine, string language)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var idx = blockStartLine - 2; // 0-based line immediately above blockStartLine
        if (idx < 0 || idx >= lines.Length)
            return string.Empty;

        var isHash = string.Equals(language, "Bash", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(language, "PowerShell", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(language, "Ruby", StringComparison.OrdinalIgnoreCase);

        if (isHash)
            return ExtractHashCommentAbove(lines, blockStartLine - 1);

        var result = new List<string>();
        var inBlock = false;

        for (var k = idx; k >= 0; k--)
        {
            var t = lines[k].Trim();
            if (inBlock)
            {
                if (t.StartsWith("/*", StringComparison.Ordinal))
                {
                    var cleaned = t.TrimStart('/', '*').Trim();
                    if (cleaned.Length > 0) result.Add(cleaned);
                    inBlock = false;
                    break;
                }
                var body = t.TrimStart('*').Trim();
                if (body.Length > 0) result.Add(body);
                continue;
            }

            if (t.StartsWith("///", StringComparison.Ordinal))
            {
                result.Add(t[3..].Trim());
                continue;
            }
            if (t.StartsWith("//", StringComparison.Ordinal))
            {
                result.Add(t[2..].Trim());
                continue;
            }
            if (t.EndsWith("*/", StringComparison.Ordinal))
            {
                if (t.StartsWith("/*", StringComparison.Ordinal))
                {
                    var single = t[2..^2].TrimStart('*').Trim();
                    if (single.Length > 0) result.Add(single);
                    break;
                }
                var tail = t[..^2].TrimStart('*').Trim();
                if (tail.Length > 0) result.Add(tail);
                inBlock = true;
                continue;
            }

            break;
        }

        result.Reverse();
        return string.Join('\n', result).Trim();
    }

    private static string ExtractHashCommentAbove(string[] lines, int startLineIndex)
    {
        var result = new List<string>();
        for (var k = startLineIndex - 1; k >= 0; k--)
        {
            var t = lines[k].Trim();
            if (t.StartsWith("#", StringComparison.Ordinal) && !t.StartsWith("#!", StringComparison.Ordinal))
                result.Add(t[1..].Trim());
            else
                break;
        }
        result.Reverse();
        return string.Join('\n', result).Trim();
    }

    private static string ExtractSqlCommentAbove(string[] lines, int startLineIndex)
    {
        var result = new List<string>();
        for (var k = startLineIndex - 1; k >= 0; k--)
        {
            var t = lines[k].Trim();
            if (t.StartsWith("--", StringComparison.Ordinal))
                result.Add(t[2..].Trim());
            else
                break;
        }
        result.Reverse();
        return string.Join('\n', result).Trim();
    }

    private static int GetIndent(string line)
    {
        var count = 0;
        while (count < line.Length && (line[count] == ' ' || line[count] == '\t'))
            count++;
        return count;
    }

    private static int BackUpToLineIndent(string source, int index)
    {
        var i = index;
        while (i > 0 && source[i - 1] is ' ' or '\t')
            i--;
        return i;
    }

    private static int[] ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts.Add(i + 1);
        }
        return starts.ToArray();
    }

    private static int GetLineNumber(int[] lineStarts, int index)
    {
        var pos = Array.BinarySearch(lineStarts, index);
        return pos >= 0 ? pos + 1 : (~pos);
    }
}
