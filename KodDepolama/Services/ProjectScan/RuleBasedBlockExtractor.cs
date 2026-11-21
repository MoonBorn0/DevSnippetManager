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
        "record", "namespace", "impl", "trait", "mod", "where", "protocol", "extension"
    };

    private static readonly Regex StatementStartRegex = new(
        @"^(?:set|import|package|use|require|include|export\s+(?:type|interface|enum|class|function|const|let|var|default)|const|let|var|type|func|fun|fn|def|class|struct|enum|interface|trait|protocol|extension|object|companion|impl|mod|namespace|public|private|protected|internal|fileprivate|open|static|final|abstract|override|virtual|inline|extern|async|suspend|function|filter|typedef|CREATE)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
        var sourceLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var blocks = new List<ExtractedBlock>();
        var symbolCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var hasErrors = false;

        if (string.Equals(language, "Python", StringComparison.OrdinalIgnoreCase))
        {
            var maskedLines = masked.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            ExtractPythonRange(sourceLines, maskedLines, 0, sourceLines.Length, classPrefix: string.Empty, blocks, symbolCounts);
        }
        else if (string.Equals(language, "Ruby", StringComparison.OrdinalIgnoreCase))
        {
            var maskedLines = masked.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            hasErrors = ExtractRuby(sourceLines, maskedLines, blocks, symbolCounts);
        }
        else if (string.Equals(language, "SQL", StringComparison.OrdinalIgnoreCase))
        {
            var maskedLines = masked.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            ExtractSql(sourceLines, maskedLines, blocks, symbolCounts);
        }
        else
        {
            hasErrors = !AreBracesBalanced(masked);
            var lineStarts = ComputeLineStarts(text);
            ExtractBraceRange(text, masked, sourceLines, language, 0, masked.Length, classPrefix: string.Empty, lineStarts, blocks, symbolCounts);
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

    private static int FindNextTopLevelOpenBrace(string masked, int start, int endLimit, string language)
    {
        var parenDepth = 0;
        var bracketDepth = 0;
        var angleDepth = 0;
        var isGo = string.Equals(language, "Go", StringComparison.OrdinalIgnoreCase);
        var isTs = string.Equals(language, "TypeScript", StringComparison.OrdinalIgnoreCase);

        for (var i = start; i < endLimit; i++)
        {
            var c = masked[i];
            if (c == '(') parenDepth++;
            else if (c == ')') { if (parenDepth > 0) parenDepth--; }
            else if (c == '[') bracketDepth++;
            else if (c == ']') { if (bracketDepth > 0) bracketDepth--; }
            else if (isTs && c == '<' && parenDepth == 0 && bracketDepth == 0)
            {
                if (angleDepth > 0 || (i > start && (char.IsLetterOrDigit(masked[i - 1]) || masked[i - 1] is '_' or '$')))
                    angleDepth++;
            }
            else if (isTs && c == '>' && angleDepth > 0 && !(i > start && (masked[i - 1] == '=' || masked[i - 1] == '-')))
            {
                angleDepth--;
            }
            else if (c == '{' && parenDepth == 0 && bracketDepth == 0 && angleDepth == 0)
            {
                // Go empty inline type like "chan struct{}" or "interface{}" in return type
                if (isGo && i + 1 < endLimit && masked[i + 1] == '}')
                {
                    var lookback = i - 1;
                    while (lookback >= start && char.IsWhiteSpace(masked[lookback])) lookback--;
                    var before = masked[start..(lookback + 1)];
                    if (Regex.IsMatch(before, @"\b(?:struct|interface)$") &&
                        !Regex.IsMatch(before, @"\btype\s+\w+(?:\[[^\]]*\])?\s+(?:struct|interface)$"))
                    {
                        i++; // skip '}'
                        continue;
                    }
                }
                return i;
            }
        }

        return -1;
    }

    private static void ExtractBraceRange(
        string source,
        string masked,
        string[] sourceLines,
        string language,
        int rangeStart,
        int rangeEnd,
        string classPrefix,
        int[] lineStarts,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var pos = rangeStart;
        var isC = string.Equals(language, "C", StringComparison.OrdinalIgnoreCase);

        while (pos < rangeEnd)
        {
            var openIdx = FindNextTopLevelOpenBrace(masked, pos, rangeEnd, language);
            if (openIdx < 0)
                break;

            var closeIdx = FindMatchingBrace(masked, openIdx, rangeEnd);
            if (closeIdx < 0)
                break;

            var headerStart = FindHeaderStart(masked, pos, openIdx);
            var header = masked[headerStart..openIdx].Trim();

            // C typedef struct { ... } Name;
            if (isC && Regex.IsMatch(Regex.Replace(header, @"\s+", " "), @"^typedef\s+(struct|enum|union)\b"))
            {
                var afterClose = closeIdx + 1;
                var semiIdx = masked.IndexOf(';', afterClose, rangeEnd - afterClose);
                if (semiIdx > afterClose)
                {
                    var alias = masked[afterClose..semiIdx].Trim();
                    var aliasMatch = Regex.Match(alias, @"([A-Za-z_]\w*)$");
                    if (aliasMatch.Success)
                    {
                        var blockStartLine = GetLineNumber(lineStarts, headerStart);
                        var blockStart = BackUpToLineIndent(source, headerStart);
                        var kind = header.Contains("enum", StringComparison.Ordinal) ? BlockKind.Enum : BlockKind.Struct;
                        var comment = ExtractPrecedingComment(sourceLines, blockStartLine, language);
                        AddBlock(aliasMatch.Groups[1].Value, kind, source[blockStart..(semiIdx + 1)], comment, blockStartLine, blocks, symbolCounts);
                        pos = semiIdx + 1;
                        continue;
                    }
                }
            }

            if (TryClassifyBraceHeader(header, language, classPrefix, out var name, out var blockKind, out var isContainer, out var isTransparent))
            {
                var blockStartLine = GetLineNumber(lineStarts, headerStart);
                var blockStart = BackUpToLineIndent(source, headerStart);
                var blockEnd = closeIdx + 1;
                if (blockEnd < source.Length && source[blockEnd] == ';')
                    blockEnd++;

                if (isTransparent)
                {
                    ExtractBraceRange(source, masked, sourceLines, language, openIdx + 1, closeIdx, classPrefix, lineStarts, blocks, symbolCounts);
                }
                else if (isContainer)
                {
                    var nextPrefix = string.IsNullOrEmpty(classPrefix) ? name : $"{classPrefix}.{name}";
                    var countBefore = blocks.Count;
                    ExtractBraceRange(source, masked, sourceLines, language, openIdx + 1, closeIdx, nextPrefix, lineStarts, blocks, symbolCounts);
                    if (blockKind == BlockKind.Class && blocks.Count == countBefore)
                    {
                        var comment = ExtractPrecedingComment(sourceLines, blockStartLine, language);
                        AddBlock(nextPrefix, BlockKind.Class, source[blockStart..blockEnd], comment, blockStartLine, blocks, symbolCounts);
                    }
                }
                else
                {
                    var fullName = string.IsNullOrEmpty(classPrefix) || name.Contains('.', StringComparison.Ordinal)
                        ? name
                        : $"{classPrefix}.{name}";
                    var comment = ExtractPrecedingComment(sourceLines, blockStartLine, language);
                    AddBlock(fullName, blockKind, source[blockStart..blockEnd], comment, blockStartLine, blocks, symbolCounts);
                }
            }

            pos = closeIdx + 1;
        }
    }

    private static int FindHeaderStart(string masked, int rangeStart, int openBraceIdx)
    {
        // Step 1: scan backward to statement boundary (;, }, {) at paren/bracket/angle depth 0
        var i = openBraceIdx - 1;
        var parenDepth = 0;
        var bracketDepth = 0;
        var angleDepth = 0;
        var seenNonWs = false;

        while (i >= rangeStart)
        {
            var c = masked[i];
            if (c == ')') parenDepth++;
            else if (c == '(') { if (parenDepth > 0) parenDepth--; }
            else if (c == ']') bracketDepth++;
            else if (c == '[') { if (bracketDepth > 0) bracketDepth--; }
            else if (c == '>' && parenDepth == 0 && bracketDepth == 0 && !(i > rangeStart && (masked[i - 1] == '=' || masked[i - 1] == '-')))
            {
                angleDepth++;
            }
            else if (c == '<' && parenDepth == 0 && bracketDepth == 0)
            {
                if (angleDepth > 0) angleDepth--;
            }
            else if (parenDepth == 0 && bracketDepth == 0 && angleDepth == 0)
            {
                if (c == '}' && i > rangeStart && masked[i - 1] == '{')
                {
                    // Skip empty inline braces like Go's struct{} / interface{}
                    seenNonWs = true;
                    i -= 2;
                    continue;
                }
                if (c == ';' || c == '}' || c == '{')
                {
                    i++;
                    break;
                }
                if (c == '\n' && seenNonWs)
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
                seenNonWs = true;
            i--;
        }

        if (i < rangeStart) i = rangeStart;
        while (i < openBraceIdx && char.IsWhiteSpace(masked[i]))
            i++;

        // Step 2: Walk line-by-line from bottom (openBraceIdx) upward through continuation/modifier/decorator lines
        // so preceding semicolon-less statements (set -euo pipefail, import Foundation, const a = 1, var x = 1) are NOT glued in.
        var candidateLines = new List<(int Start, int End, string Trimmed)>();
        var cursor = i;
        while (cursor < openBraceIdx)
        {
            var nl = masked.IndexOf('\n', cursor, openBraceIdx - cursor);
            var end = nl >= 0 ? nl : openBraceIdx;
            var trimmed = masked[cursor..end].Trim();
            if (trimmed.Length > 0)
                candidateLines.Add((cursor, end, trimmed));
            if (nl < 0) break;
            cursor = nl + 1;
        }

        if (candidateLines.Count <= 1)
            return candidateLines.Count == 1 ? TrimLeadingWhitespace(masked, candidateLines[0].Start, openBraceIdx) : i;

        var startIdx = candidateLines.Count - 1;
        var openParensNeeded = CountNetClosing(candidateLines[startIdx].Trimmed, '(', ')');
        var openAnglesNeeded = CountNetClosing(candidateLines[startIdx].Trimmed, '<', '>');

        while (startIdx > 0)
        {
            var prevLine = candidateLines[startIdx - 1].Trimmed;
            var curLine = candidateLines[startIdx].Trimmed;

            // Stop at preprocessor directives or C++ access specifiers
            if (prevLine.StartsWith("#", StringComparison.Ordinal) && !prevLine.StartsWith("#[", StringComparison.Ordinal))
                break;
            if (prevLine is "public:" or "private:" or "protected:")
                break;

            var keepPrev = false;

            if (openParensNeeded > 0 || openAnglesNeeded > 0)
            {
                keepPrev = true;
            }
            else if (IsDecoratorOrAttributeLine(prevLine) || IsModifierOrTemplateLine(prevLine))
            {
                keepPrev = true;
            }
            else if (IsContinuationLine(prevLine, curLine))
            {
                keepPrev = true;
            }

            if (!keepPrev)
                break;

            startIdx--;
            openParensNeeded = Math.Max(0, openParensNeeded + CountNetClosing(prevLine, '(', ')'));
            openAnglesNeeded = Math.Max(0, openAnglesNeeded + CountNetClosing(prevLine, '<', '>'));
        }

        return TrimLeadingWhitespace(masked, candidateLines[startIdx].Start, openBraceIdx);
    }

    private static int TrimLeadingWhitespace(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;
        return start;
    }

    private static int CountNetClosing(string line, char open, char close)
    {
        var net = 0;
        for (var k = 0; k < line.Length; k++)
        {
            if (line[k] == close && !(close == '>' && k > 0 && (line[k - 1] == '-' || line[k - 1] == '=')))
                net++;
            else if (line[k] == open)
                net--;
        }
        return net;
    }

    private static bool IsDecoratorOrAttributeLine(string line)
    {
        return line.StartsWith("@", StringComparison.Ordinal) ||
               line.StartsWith("#[", StringComparison.Ordinal);
    }

    private static bool IsModifierOrTemplateLine(string line)
    {
        return line.StartsWith("template", StringComparison.Ordinal) ||
               Regex.IsMatch(line, @"^(?:public|private|protected|internal|export|static|async|virtual|inline|extern|abstract|override|final|pub(?:\([^)]*\))?)$");
    }

    private static bool IsContinuationLine(string prevLine, string curLine)
    {
        if (curLine == "where" ||
            curLine.StartsWith("where ", StringComparison.Ordinal) ||
            curLine.StartsWith("throws ", StringComparison.Ordinal) ||
            curLine.StartsWith("extends ", StringComparison.Ordinal) ||
            curLine.StartsWith("implements ", StringComparison.Ordinal) ||
            curLine.StartsWith(":", StringComparison.Ordinal) ||
            curLine.StartsWith(")", StringComparison.Ordinal) ||
            curLine.StartsWith("{", StringComparison.Ordinal))
            return true;

        if (prevLine.EndsWith(",", StringComparison.Ordinal) ||
            prevLine.EndsWith("(", StringComparison.Ordinal) ||
            prevLine.EndsWith("=", StringComparison.Ordinal) ||
            prevLine.EndsWith("=>", StringComparison.Ordinal) ||
            prevLine.EndsWith("->", StringComparison.Ordinal) ||
            prevLine.EndsWith(":", StringComparison.Ordinal) ||
            prevLine.EndsWith("where", StringComparison.Ordinal))
            return true;

        // If curLine starts a brand new declaration (func/function/class/const/let/var/...), prevLine is NOT a continuation
        if (StatementStartRegex.IsMatch(curLine))
            return false;

        return false;
    }

    private static bool TryClassifyBraceHeader(
        string header,
        string language,
        string classPrefix,
        out string name,
        out BlockKind kind,
        out bool isContainer,
        out bool isTransparent)
    {
        name = string.Empty;
        kind = BlockKind.Function;
        isContainer = false;
        isTransparent = false;

        if (string.IsNullOrWhiteSpace(header))
            return false;

        var collapsed = Regex.Replace(header, @"\s+", " ").Trim();

        // PowerShell
        if (string.Equals(language, "PowerShell", StringComparison.OrdinalIgnoreCase))
        {
            var psClass = Regex.Match(collapsed, @"^class\s+([A-Za-z_]\w*)\b", RegexOptions.IgnoreCase);
            if (psClass.Success)
            {
                name = psClass.Groups[1].Value;
                kind = BlockKind.Class;
                isContainer = true;
                return true;
            }

            var psFn = Regex.Match(collapsed, @"^(?:function|filter)\s+([\w-]+)\b", RegexOptions.IgnoreCase);
            if (psFn.Success)
            {
                name = psFn.Groups[1].Value;
                kind = BlockKind.Function;
                return true;
            }

            if (!string.IsNullOrEmpty(classPrefix))
            {
                var psMethod = Regex.Match(collapsed, @"^(?:(?:static|hidden)\s+)*(?:\[[^\]]+\]\s*)?([A-Za-z_]\w*)\s*\([^)]*\)$");
                if (psMethod.Success && !ControlKeywords.Contains(psMethod.Groups[1].Value))
                {
                    name = psMethod.Groups[1].Value;
                    kind = string.Equals(name, classPrefix.Split('.').Last(), StringComparison.OrdinalIgnoreCase)
                        ? BlockKind.Constructor
                        : BlockKind.Method;
                    return true;
                }
            }

            return false;
        }

        // Bash
        if (string.Equals(language, "Bash", StringComparison.OrdinalIgnoreCase))
        {
            var bashMatch = Regex.Match(collapsed, @"^(?:function\s+([A-Za-z_][\w-]*)(?:\s*\(\s*\))?|([A-Za-z_][\w-]*)\s*\(\s*\))$");
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
            isTransparent = true;
            return true;
        }

        // Rust
        if (string.Equals(language, "Rust", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(collapsed, @"\bmod\s+[A-Za-z_]\w*\b"))
            {
                isTransparent = true;
                return true;
            }

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
            var goMethod = Regex.Match(collapsed, @"^func\s*\(\s*\w+\s+\*?([A-Za-z_]\w*)(?:\[[^\]]*\])?\s*\)\s*([A-Za-z_]\w*)\s*\(");
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

            var goType = Regex.Match(collapsed, @"^type\s+([A-Za-z_]\w*)(?:\[[^\]]*\])?\s+(struct|interface)\b");
            if (goType.Success)
            {
                name = goType.Groups[1].Value;
                kind = goType.Groups[2].Value == "struct" ? BlockKind.Struct : BlockKind.Interface;
                return true;
            }
            return false;
        }

        // Kotlin
        if (string.Equals(language, "Kotlin", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(collapsed, @"^(?:private\s+|internal\s+|public\s+)?companion\s+object\b"))
            {
                isTransparent = true;
                return true;
            }

            var objMatch = Regex.Match(collapsed, @"\bobject\s+([A-Za-z_]\w*)\b");
            if (objMatch.Success)
            {
                name = objMatch.Groups[1].Value;
                kind = BlockKind.Class;
                isContainer = true;
                return true;
            }

            var ktEnum = Regex.Match(collapsed, @"\benum\s+class\s+([A-Za-z_]\w*)\b");
            if (ktEnum.Success)
            {
                name = ktEnum.Groups[1].Value;
                kind = BlockKind.Enum;
                return true;
            }

            var ktIface = Regex.Match(collapsed, @"\binterface\s+([A-Za-z_]\w*)\b");
            if (ktIface.Success)
            {
                name = ktIface.Groups[1].Value;
                kind = BlockKind.Interface;
                return true;
            }

            var ktClass = Regex.Match(collapsed, @"\bclass\s+([A-Za-z_]\w*)\b");
            if (ktClass.Success)
            {
                name = ktClass.Groups[1].Value;
                kind = BlockKind.Class;
                isContainer = true;
                return true;
            }

            var ktFun = Regex.Match(collapsed, @"\bfun\s+(?:<[^>]*>\s*)?(?:([A-Za-z_]\w*)(?:<[^>]*>)?\.)?([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(");
            if (ktFun.Success)
            {
                name = !string.IsNullOrEmpty(ktFun.Groups[1].Value)
                    ? $"{ktFun.Groups[1].Value}.{ktFun.Groups[2].Value}"
                    : ktFun.Groups[2].Value;
                kind = string.IsNullOrEmpty(classPrefix) ? BlockKind.Function : BlockKind.Method;
                return true;
            }
            return false;
        }

        // Swift
        if (string.Equals(language, "Swift", StringComparison.OrdinalIgnoreCase))
        {
            var extMatch = Regex.Match(collapsed, @"\bextension\s+([A-Za-z_]\w*)\b");
            if (extMatch.Success)
            {
                name = extMatch.Groups[1].Value;
                kind = BlockKind.Class;
                isContainer = true;
                return true;
            }

            var protoMatch = Regex.Match(collapsed, @"\bprotocol\s+([A-Za-z_]\w*)\b");
            if (protoMatch.Success)
            {
                name = protoMatch.Groups[1].Value;
                kind = BlockKind.Interface;
                return true;
            }

            var swType = Regex.Match(collapsed, @"\b(struct|enum)\s+([A-Za-z_]\w*)\b");
            if (swType.Success)
            {
                name = swType.Groups[2].Value;
                kind = swType.Groups[1].Value == "struct" ? BlockKind.Struct : BlockKind.Enum;
                return true;
            }

            var swClass = Regex.Match(collapsed, @"\bclass\s+([A-Za-z_]\w*)\b");
            if (swClass.Success)
            {
                name = swClass.Groups[1].Value;
                kind = BlockKind.Class;
                isContainer = true;
                return true;
            }

            var swInit = Regex.Match(collapsed, @"\binit\??\s*\(");
            if (swInit.Success && !string.IsNullOrEmpty(classPrefix))
            {
                name = "init";
                kind = BlockKind.Constructor;
                return true;
            }

            var swFunc = Regex.Match(collapsed, @"\bfunc\s+([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(");
            if (swFunc.Success)
            {
                name = swFunc.Groups[1].Value;
                kind = string.IsNullOrEmpty(classPrefix) ? BlockKind.Function : BlockKind.Method;
                return true;
            }
            return false;
        }

        // PHP
        if (string.Equals(language, "PHP", StringComparison.OrdinalIgnoreCase))
        {
            var phpClassOrTrait = Regex.Match(collapsed, @"\b(class|trait)\s+([A-Za-z_]\w*)\b");
            if (phpClassOrTrait.Success)
            {
                name = phpClassOrTrait.Groups[2].Value;
                kind = BlockKind.Class;
                isContainer = true;
                return true;
            }

            var phpIface = Regex.Match(collapsed, @"\b(interface|enum)\s+([A-Za-z_]\w*)\b");
            if (phpIface.Success)
            {
                name = phpIface.Groups[2].Value;
                kind = phpIface.Groups[1].Value == "enum" ? BlockKind.Enum : BlockKind.Interface;
                return true;
            }

            var phpFn = Regex.Match(collapsed, @"\bfunction\s+&?\s*([A-Za-z_]\w*)\s*\(");
            if (phpFn.Success)
            {
                name = phpFn.Groups[1].Value;
                kind = string.IsNullOrEmpty(classPrefix)
                    ? BlockKind.Function
                    : (name == "__construct" ? BlockKind.Constructor : BlockKind.Method);
                return true;
            }
            return false;
        }

        // Class container (Java, JS, TS, C++)
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
        if (typeMatch.Success && !Regex.IsMatch(collapsed, @"\)\s*(?:const|noexcept|override|final)*$"))
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

        // JS / TS
        if (string.Equals(language, "JavaScript", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "TypeScript", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(collapsed, @"^export\s+default\s+(?:async\s+)?function\s*\*?\s*\("))
            {
                name = "default";
                kind = BlockKind.Function;
                return true;
            }

            var fnMatch = Regex.Match(collapsed, @"^(?:export\s+)?(?:default\s+)?(?:async\s+)?function\s*\*?\s*([A-Za-z_$][\w$]*)\s*(?:<[^>]*>)?\s*\(");
            if (fnMatch.Success)
            {
                name = fnMatch.Groups[1].Value;
                kind = string.IsNullOrEmpty(classPrefix) ? BlockKind.Function : BlockKind.Method;
                return true;
            }

            var varFn = Regex.Match(collapsed, @"^(?:export\s+)?(?:const|let|var)\s+([A-Za-z_$][\w$]*)\s*(?::[^=]+)?=\s*(?:async\s+)?(?:function\b|\([^)]*\)\s*(?::[^=]+)?=>|[A-Za-z_$][\w$]*\s*=>)");
            if (varFn.Success)
            {
                name = varFn.Groups[1].Value;
                kind = BlockKind.Function;
                return true;
            }

            if (!string.IsNullOrEmpty(classPrefix))
            {
                // Class arrow property: handle = () => {
                var arrowProp = Regex.Match(collapsed, @"^(?:(?:public|private|protected|static|readonly)\s+)*([A-Za-z_$][\w$]*)\s*=\s*(?:async\s+)?(?:\([^)]*\)\s*(?::[^=]+)?=>|[A-Za-z_$][\w$]*\s*=>)");
                if (arrowProp.Success && !ControlKeywords.Contains(arrowProp.Groups[1].Value))
                {
                    name = arrowProp.Groups[1].Value;
                    kind = BlockKind.Method;
                    return true;
                }

                // Class method (supports params with nested braces/types)
                var methodMatch = Regex.Match(collapsed, @"^(?:(?:public|private|protected|static|async|override|readonly|get|set)\s+)*([A-Za-z_$][\w$]*)\s*(?:<[^>]*>)?\s*\([\s\S]*\)(?:\s*:\s*[\s\S]+)?$");
                if (methodMatch.Success && !ControlKeywords.Contains(methodMatch.Groups[1].Value))
                {
                    name = methodMatch.Groups[1].Value;
                    kind = name == "constructor" ? BlockKind.Constructor : BlockKind.Method;
                    return true;
                }
            }

            return false;
        }

        // Java / C / C++
        var cLike = Regex.Match(collapsed, @"(?:([A-Za-z_]\w*)(?:<[^>]*>)?::)?([A-Za-z_~]\w*)\s*\([\s\S]*\)\s*(?:(?:\s*(?:const|noexcept|override|final|throws\s+[\w.,\s]+))|(?:\s*:\s*[^;{]+))*$");
        if (cLike.Success)
        {
            var qualifiedClass = cLike.Groups[1].Value;
            var fnName = cLike.Groups[2].Value;
            if (ControlKeywords.Contains(fnName))
                return false;

            var beforeMatch = collapsed[..cLike.Index].Trim();
            if (beforeMatch.EndsWith("return", StringComparison.Ordinal) ||
                beforeMatch.EndsWith("=", StringComparison.Ordinal) ||
                beforeMatch.Contains("new ", StringComparison.Ordinal))
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
            while (blockStart > startLine &&
                   maskedLines[blockStart - 1].TrimStart().StartsWith("@", StringComparison.Ordinal) &&
                   GetIndent(rawLines[blockStart - 1]) == declIndent)
            {
                blockStart--;
            }

            // Find end of block using MASKED lines (so column-0 lines inside triple-quoted strings don't truncate!)
            var j = i + 1;
            while (j < endLine)
            {
                if (maskedLines[j].Trim().Length > 0 && GetIndent(maskedLines[j]) <= declIndent)
                    break;
                j++;
            }

            // Trim trailing blank lines and trailing # comment lines that belong to the NEXT block
            var blockEnd = j;
            while (blockEnd > i + 1)
            {
                var rawTrim = rawLines[blockEnd - 1].Trim();
                if (rawTrim.Length == 0 || rawTrim.StartsWith("#", StringComparison.Ordinal))
                    blockEnd--;
                else
                    break;
            }

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
        // Advance past multiline signature closing "):"
        var sigEnd = defLine;
        while (sigEnd < blockEnd && !rawLines[sigEnd].TrimEnd().EndsWith(":", StringComparison.Ordinal))
            sigEnd++;

        var bodyLine = sigEnd + 1;
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

        return ExtractHashCommentAbove(rawLines, blockStart);
    }

    private static bool ExtractRuby(
        string[] rawLines,
        string[] maskedLines,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var stack = new Stack<(string Kind, string Name, int StartLine, int BlocksBefore)>();
        var hasErrors = false;

        for (var i = 0; i < maskedLines.Length; i++)
        {
            var line = maskedLines[i].Trim();
            if (line.Length == 0) continue;

            // class << self (singleton class — transparent container)
            if (Regex.IsMatch(line, @"^class\s*<<\s*\S+"))
            {
                stack.Push(("singleton", string.Empty, i, blocks.Count));
                continue;
            }

            var classMatch = Regex.Match(line, @"^(?:class|module)\s+([A-Za-z_][\w:]*)");
            if (classMatch.Success)
            {
                stack.Push(("class", classMatch.Groups[1].Value, i, blocks.Count));
                continue;
            }

            // def / private def / protected def / public def
            var defMatch = Regex.Match(line, @"^(?:(?:private|protected|public)\s+)?def\s+(?:self\.)?([A-Za-z_]\w*[!?=]?)");
            if (defMatch.Success)
            {
                // Endless method: def foo(x) = expr (no matching 'end')
                if (Regex.IsMatch(line, @"\)\s*=(?!=)") || Regex.IsMatch(line, @"^def\s+(?:self\.)?[A-Za-z_]\w*[!?]?\s*=(?!=)"))
                    continue;

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
        string[] rawLines,
        string[] maskedLines,
        List<ExtractedBlock> blocks,
        Dictionary<string, int> symbolCounts)
    {
        var i = 0;
        while (i < maskedLines.Length)
        {
            var match = Regex.Match(
                maskedLines[i].Trim(),
                @"^CREATE\s+(?:OR\s+(?:ALTER|REPLACE)\s+)?(PROCEDURE|PROC|FUNCTION|VIEW|TRIGGER|TABLE)\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:\[?[\w]+\]?\.)?\[?([A-Za-z_][\w]*)\]?",
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
            var j = i;
            var parenDepth = 0;
            var beginDepth = 0;
            var inDollarQuote = false;
            var foundEnd = false;

            while (j < maskedLines.Length)
            {
                var line = maskedLines[j].Trim();
                if (j > start && Regex.IsMatch(line, @"^GO\b", RegexOptions.IgnoreCase))
                {
                    break;
                }
                if (j > start && beginDepth == 0 && !inDollarQuote &&
                    Regex.IsMatch(line, @"^CREATE\s+(?:OR\s+(?:ALTER|REPLACE)\s+)?(PROCEDURE|PROC|FUNCTION|VIEW|TRIGGER|TABLE)\b", RegexOptions.IgnoreCase))
                {
                    break;
                }

                // Track $$ dollar-quoted body (PostgreSQL)
                var dollarCount = Regex.Matches(line, @"\$\$").Count;
                if (dollarCount % 2 == 1)
                    inDollarQuote = !inDollarQuote;

                foreach (var ch in line)
                {
                    if (ch == '(') parenDepth++;
                    else if (ch == ')' && parenDepth > 0) parenDepth--;
                }

                if (Regex.IsMatch(line, @"\bBEGIN\b", RegexOptions.IgnoreCase))
                    beginDepth++;
                if (Regex.IsMatch(line, @"\bEND\b", RegexOptions.IgnoreCase) && beginDepth > 0)
                    beginDepth--;

                // Terminate at statement-ending ';' when not inside parens, BEGIN..END, or $$...$$
                if (parenDepth == 0 && beginDepth == 0 && !inDollarQuote && line.EndsWith(";", StringComparison.Ordinal))
                {
                    j++;
                    foundEnd = true;
                    break;
                }

                j++;
            }

            var end = j;
            while (end > start + 1 && rawLines[end - 1].Trim().Length == 0)
                end--;

            var code = string.Join('\n', rawLines[start..end]);
            var comment = ExtractSqlCommentAbove(rawLines, start);
            AddBlock(name, kind, code, comment, start + 1, blocks, symbolCounts);

            if (!foundEnd && j < maskedLines.Length && Regex.IsMatch(maskedLines[j].Trim(), @"^GO\b", RegexOptions.IgnoreCase))
                i = j + 1;
            else
                i = j;
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

    private static string ExtractPrecedingComment(string[] lines, int blockStartLine, string language)
    {
        var idx = blockStartLine - 2; // 0-based line immediately above blockStartLine
        if (idx < 0 || idx >= lines.Length)
            return string.Empty;

        if (string.Equals(language, "PowerShell", StringComparison.OrdinalIgnoreCase))
            return ExtractPowerShellCommentAbove(lines, idx);

        if (string.Equals(language, "Bash", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "Ruby", StringComparison.OrdinalIgnoreCase))
            return ExtractHashCommentAbove(lines, blockStartLine - 1);

        var isJsTs = string.Equals(language, "JavaScript", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(language, "TypeScript", StringComparison.OrdinalIgnoreCase);

        var result = new List<string>();
        var inBlock = false;
        var isJsDoc = false;

        for (var k = idx; k >= 0; k--)
        {
            var t = lines[k].Trim();
            if (inBlock)
            {
                if (t.StartsWith("/**", StringComparison.Ordinal))
                    isJsDoc = true;

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
            if (string.Equals(language, "PHP", StringComparison.OrdinalIgnoreCase) && t.StartsWith("#", StringComparison.Ordinal))
            {
                result.Add(t[1..].Trim());
                continue;
            }
            if (t.EndsWith("*/", StringComparison.Ordinal))
            {
                if (t.StartsWith("/*", StringComparison.Ordinal))
                {
                    if (t.StartsWith("/**", StringComparison.Ordinal))
                        isJsDoc = true;
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
        if (isJsTs && isJsDoc)
            return FormatJsDoc(result);

        return string.Join('\n', result).Trim();
    }

    private static string FormatJsDoc(List<string> lines)
    {
        var formatted = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith("@param", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(line, @"^@param\s+(?:\{[^}]*\}\s+)?([A-Za-z_$][\w$]*)(?:\s*[-:]?\s*(.*))?$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var paramName = m.Groups[1].Value;
                    var desc = m.Groups[2].Value.Trim();
                    formatted.Add(desc.Length > 0 ? $"- {paramName}: {desc}" : $"- {paramName}");
                }
                continue;
            }

            if (line.StartsWith("@returns", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("@return", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(line, @"^@returns?\s+(?:\{[^}]*\}\s+)?(.*)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    var desc = m.Groups[1].Value.Trim();
                    if (desc.Length > 0)
                        formatted.Add($"Döndürür: {desc}");
                }
                continue;
            }

            if (line.StartsWith("@", StringComparison.Ordinal))
                continue;

            formatted.Add(line);
        }

        return string.Join('\n', formatted).Trim();
    }

    private static string ExtractPowerShellCommentAbove(string[] lines, int idx)
    {
        var t0 = lines[idx].Trim();
        if (t0.EndsWith("#>", StringComparison.Ordinal))
        {
            var result = new List<string>();
            for (var k = idx; k >= 0; k--)
            {
                var t = lines[k].Trim();
                if (t.StartsWith("<#", StringComparison.Ordinal))
                {
                    var inner = t[2..].Replace("#>", string.Empty).Trim();
                    if (inner.Length > 0) result.Add(inner);
                    break;
                }
                var cleaned = t.Replace("#>", string.Empty).Trim();
                if (cleaned.Length > 0) result.Add(cleaned);
            }
            result.Reverse();
            return string.Join('\n', result).Trim();
        }

        return ExtractHashCommentAbove(lines, idx + 1);
    }

    private static string ExtractHashCommentAbove(string[] lines, int startLineIndex)
    {
        var result = new List<string>();
        for (var k = startLineIndex - 1; k >= 0; k--)
        {
            var t = lines[k].Trim();
            if (t.StartsWith("#", StringComparison.Ordinal) &&
                !t.StartsWith("#!", StringComparison.Ordinal) &&
                !t.StartsWith("#Requires", StringComparison.OrdinalIgnoreCase))
                result.Add(t[1..].Trim());
            else
                break;
        }
        result.Reverse();
        return string.Join('\n', result).Trim();
    }

    private static string ExtractSqlCommentAbove(string[] lines, int startLineIndex)
    {
        var idx = startLineIndex - 1;
        if (idx < 0 || idx >= lines.Length)
            return string.Empty;

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
                    break;
                }
                var body = t.TrimStart('*').Trim();
                if (body.Length > 0) result.Add(body);
                continue;
            }

            if (t.StartsWith("--", StringComparison.Ordinal))
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
