using System;
using System.Collections.Generic;

namespace DevSnippetManager.Services.ProjectScan;

public static class SourceMasker
{
    private static readonly HashSet<string> RegexPrecedingKeywords = new(StringComparer.Ordinal)
    {
        "return", "throw", "case", "in", "of", "typeof", "void", "delete", "await", "yield", "new", "else"
    };

    public static string Mask(string source, string language)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        var chars = source.ToCharArray();
        var lang = language ?? string.Empty;

        if (string.Equals(lang, "Python", StringComparison.OrdinalIgnoreCase))
        {
            MaskPython(source, chars);
        }
        else if (string.Equals(lang, "SQL", StringComparison.OrdinalIgnoreCase))
        {
            MaskSql(source, chars);
        }
        else if (string.Equals(lang, "PowerShell", StringComparison.OrdinalIgnoreCase))
        {
            MaskPowerShell(source, chars);
        }
        else if (string.Equals(lang, "Bash", StringComparison.OrdinalIgnoreCase))
        {
            MaskBash(source, chars);
        }
        else if (string.Equals(lang, "Ruby", StringComparison.OrdinalIgnoreCase))
        {
            MaskRuby(source, chars);
        }
        else
        {
            MaskCStyle(source, chars, lang);
        }

        return new string(chars);
    }

    private static void Blank(char[] chars, int index)
    {
        if (chars[index] != '\r' && chars[index] != '\n')
            chars[index] = ' ';
    }

    private static void MaskPython(string source, char[] chars)
    {
        var i = 0;
        var len = source.Length;
        while (i < len)
        {
            var c = source[i];
            if (c == '#')
            {
                while (i < len && source[i] != '\n')
                    Blank(chars, i++);
                continue;
            }

            if ((c == '"' || c == '\'') && i + 2 < len && source[i + 1] == c && source[i + 2] == c)
            {
                var q = c;
                Blank(chars, i++); Blank(chars, i++); Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '\\' && i + 1 < len)
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    if (source[i] == q && i + 2 < len && source[i + 1] == q && source[i + 2] == q)
                    {
                        Blank(chars, i++); Blank(chars, i++); Blank(chars, i++);
                        break;
                    }
                    Blank(chars, i++);
                }
                continue;
            }

            if (c == '"' || c == '\'')
            {
                var q = c;
                Blank(chars, i++);
                while (i < len && source[i] != '\n')
                {
                    if (source[i] == '\\' && i + 1 < len)
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == q)
                        break;
                }
                continue;
            }

            i++;
        }
    }

    private static void MaskSql(string source, char[] chars)
    {
        var i = 0;
        var len = source.Length;
        while (i < len)
        {
            if (source[i] == '-' && i + 1 < len && source[i + 1] == '-')
            {
                while (i < len && source[i] != '\n')
                    Blank(chars, i++);
                continue;
            }

            if (source[i] == '/' && i + 1 < len && source[i + 1] == '*')
            {
                Blank(chars, i++); Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '*' && i + 1 < len && source[i + 1] == '/')
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        break;
                    }
                    Blank(chars, i++);
                }
                continue;
            }

            if (source[i] == '\'')
            {
                Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '\'' && i + 1 < len && source[i + 1] == '\'')
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == '\'')
                        break;
                }
                continue;
            }

            i++;
        }
    }

    private static void MaskPowerShell(string source, char[] chars)
    {
        var i = 0;
        var len = source.Length;
        while (i < len)
        {
            if (source[i] == '<' && i + 1 < len && source[i + 1] == '#')
            {
                Blank(chars, i++); Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '#' && i + 1 < len && source[i + 1] == '>')
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        break;
                    }
                    Blank(chars, i++);
                }
                continue;
            }

            // Here-strings @" ... "@ and @' ... '@
            if (source[i] == '@' && i + 1 < len && (source[i + 1] == '"' || source[i + 1] == '\''))
            {
                var q = source[i + 1];
                var nl = i + 2;
                while (nl < len && (source[nl] == ' ' || source[nl] == '\t' || source[nl] == '\r')) nl++;
                if (nl < len && source[nl] == '\n')
                {
                    Blank(chars, i++); Blank(chars, i++);
                    while (i < len)
                    {
                        if (source[i] == '\n' && i + 2 < len && source[i + 1] == q && source[i + 2] == '@')
                        {
                            i++;
                            Blank(chars, i++); Blank(chars, i++);
                            break;
                        }
                        Blank(chars, i++);
                    }
                    continue;
                }
            }

            if (source[i] == '#')
            {
                while (i < len && source[i] != '\n')
                    Blank(chars, i++);
                continue;
            }

            if (source[i] == '"')
            {
                Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '`' && i + 1 < len)
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == '"')
                        break;
                }
                continue;
            }

            if (source[i] == '\'')
            {
                Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '\'' && i + 1 < len && source[i + 1] == '\'')
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == '\'')
                        break;
                }
                continue;
            }

            i++;
        }
    }

    private static void MaskBash(string source, char[] chars)
    {
        var i = 0;
        var len = source.Length;
        while (i < len)
        {
            // Heredoc <<EOF or <<'EOF' or <<-EOF
            if (source[i] == '<' && i + 1 < len && source[i + 1] == '<' && (i + 2 >= len || source[i + 2] != '<'))
            {
                var p = i + 2;
                if (p < len && source[p] == '-') p++;
                while (p < len && (source[p] == ' ' || source[p] == '\t')) p++;
                var quote = p < len && (source[p] == '\'' || source[p] == '"') ? source[p++] : '\0';
                var delimStart = p;
                while (p < len && (char.IsLetterOrDigit(source[p]) || source[p] == '_')) p++;
                var delim = source[delimStart..p];
                if (quote != '\0' && p < len && source[p] == quote) p++;

                if (delim.Length > 0)
                {
                    // Advance to end of current line without masking command before newline
                    while (i < len && source[i] != '\n') i++;
                    if (i < len) i++;
                    // Mask heredoc body lines until delimiter line
                    while (i < len)
                    {
                        var lineStart = i;
                        while (i < len && source[i] != '\n') i++;
                        var lineText = source[lineStart..i].Trim();
                        for (var k = lineStart; k < i; k++) Blank(chars, k);
                        if (i < len) i++;
                        if (string.Equals(lineText, delim, StringComparison.Ordinal))
                            break;
                    }
                    continue;
                }
            }

            // Parameter expansion ${...} - mask inner content so # or quotes don't break anything
            if (source[i] == '$' && i + 1 < len && source[i + 1] == '{')
            {
                Blank(chars, i++); Blank(chars, i++);
                var depth = 1;
                while (i < len && depth > 0)
                {
                    if (source[i] == '{') depth++;
                    else if (source[i] == '}') depth--;
                    Blank(chars, i++);
                }
                continue;
            }

            if (source[i] == '#' && (i == 0 || char.IsWhiteSpace(source[i - 1]) || source[i - 1] == ';'))
            {
                while (i < len && source[i] != '\n')
                    Blank(chars, i++);
                continue;
            }

            if (source[i] == '"')
            {
                Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '\\' && i + 1 < len)
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == '"')
                        break;
                }
                continue;
            }

            if (source[i] == '\'')
            {
                Blank(chars, i++);
                while (i < len)
                {
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == '\'')
                        break;
                }
                continue;
            }

            i++;
        }
    }

    private static void MaskRuby(string source, char[] chars)
    {
        var i = 0;
        var len = source.Length;
        while (i < len)
        {
            // Ruby heredoc <<~TEXT / <<-TEXT / <<TEXT
            if (source[i] == '<' && i + 1 < len && source[i + 1] == '<')
            {
                var p = i + 2;
                if (p < len && (source[p] == '~' || source[p] == '-')) p++;
                var quote = p < len && (source[p] == '\'' || source[p] == '"') ? source[p++] : '\0';
                if (p < len && char.IsLetter(source[p]))
                {
                    var delimStart = p;
                    while (p < len && (char.IsLetterOrDigit(source[p]) || source[p] == '_')) p++;
                    var delim = source[delimStart..p];
                    if (quote != '\0' && p < len && source[p] == quote) p++;
                    // Only treat as heredoc if rest of line has no code or delimiter is uppercase/identifier
                    var restEnd = p;
                    while (restEnd < len && source[restEnd] != '\n') restEnd++;
                    if (delim.Length > 0 && string.IsNullOrWhiteSpace(source[p..restEnd]))
                    {
                        for (var k = i; k < restEnd; k++) Blank(chars, k);
                        i = restEnd < len ? restEnd + 1 : len;
                        while (i < len)
                        {
                            var lineStart = i;
                            while (i < len && source[i] != '\n') i++;
                            var lineText = source[lineStart..i].Trim();
                            for (var k = lineStart; k < i; k++) Blank(chars, k);
                            if (i < len) i++;
                            if (string.Equals(lineText, delim, StringComparison.Ordinal))
                                break;
                        }
                        continue;
                    }
                }
            }

            if (source[i] == '#')
            {
                while (i < len && source[i] != '\n')
                    Blank(chars, i++);
                continue;
            }

            if (source[i] == '"' || source[i] == '\'')
            {
                var q = source[i];
                Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '\\' && i + 1 < len)
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == q)
                        break;
                }
                continue;
            }

            i++;
        }
    }

    private static void MaskCStyle(string source, char[] chars, string lang)
    {
        var isJsTs = string.Equals(lang, "JavaScript", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(lang, "TypeScript", StringComparison.OrdinalIgnoreCase);
        var isGo = string.Equals(lang, "Go", StringComparison.OrdinalIgnoreCase);
        var isRust = string.Equals(lang, "Rust", StringComparison.OrdinalIgnoreCase);
        var isPhp = string.Equals(lang, "PHP", StringComparison.OrdinalIgnoreCase);
        var isCpp = string.Equals(lang, "C++", StringComparison.OrdinalIgnoreCase);
        var hasTripleQuotes = string.Equals(lang, "Java", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(lang, "Kotlin", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(lang, "Swift", StringComparison.OrdinalIgnoreCase);
        var nestedBlockComments = isRust ||
                                  string.Equals(lang, "Kotlin", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(lang, "Swift", StringComparison.OrdinalIgnoreCase);

        var i = 0;
        var len = source.Length;

        // PHP: mask leading inline HTML before first <?php if present
        if (isPhp)
        {
            var phpOpen = source.IndexOf("<?", StringComparison.Ordinal);
            if (phpOpen > 0)
            {
                for (var k = 0; k < phpOpen; k++) Blank(chars, k);
                i = phpOpen;
            }
            if (i + 5 <= len && source.AsSpan(i, 5).Equals("<?php", StringComparison.OrdinalIgnoreCase))
            {
                for (var k = 0; k < 5; k++) Blank(chars, i++);
            }
        }

        while (i < len)
        {
            var c = source[i];

            // PHP closing tag ?> ... skip inline HTML until next <?php / <?=
            if (isPhp && c == '?' && i + 1 < len && source[i + 1] == '>')
            {
                Blank(chars, i++); Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '<' && i + 1 < len && source[i + 1] == '?')
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        if (i + 3 <= len && source.AsSpan(i, 3).Equals("php", StringComparison.OrdinalIgnoreCase))
                        {
                            Blank(chars, i++); Blank(chars, i++); Blank(chars, i++);
                        }
                        else if (i < len && source[i] == '=')
                        {
                            Blank(chars, i++);
                        }
                        break;
                    }
                    Blank(chars, i++);
                }
                continue;
            }

            // PHP heredoc / nowdoc <<<EOT ... EOT;
            if (isPhp && c == '<' && i + 2 < len && source[i + 1] == '<' && source[i + 2] == '<')
            {
                var p = i + 3;
                while (p < len && (source[p] == ' ' || source[p] == '\t')) p++;
                var quote = p < len && (source[p] == '\'' || source[p] == '"') ? source[p++] : '\0';
                var delimStart = p;
                while (p < len && (char.IsLetterOrDigit(source[p]) || source[p] == '_')) p++;
                var delim = source[delimStart..p];
                if (quote != '\0' && p < len && source[p] == quote) p++;
                if (delim.Length > 0)
                {
                    while (i < len && source[i] != '\n') Blank(chars, i++);
                    if (i < len) i++;
                    while (i < len)
                    {
                        var lineStart = i;
                        while (i < len && source[i] != '\n') i++;
                        var trimmed = source[lineStart..i].Trim().TrimEnd(';');
                        for (var k = lineStart; k < i; k++) Blank(chars, k);
                        if (i < len) i++;
                        if (string.Equals(trimmed, delim, StringComparison.Ordinal))
                            break;
                    }
                    continue;
                }
            }

            // C++ raw string literal R"delim(...)delim"
            if (isCpp && c == 'R' && i + 1 < len && source[i + 1] == '"' &&
                (i == 0 || !(char.IsLetterOrDigit(source[i - 1]) || source[i - 1] == '_')))
            {
                var p = i + 2;
                var delimStart = p;
                while (p < len && source[p] != '(' && source[p] != '\n' && p - delimStart <= 16) p++;
                if (p < len && source[p] == '(')
                {
                    var endSeq = ")" + source[delimStart..p] + "\"";
                    var endIdx = source.IndexOf(endSeq, p + 1, StringComparison.Ordinal);
                    var stop = endIdx >= 0 ? endIdx + endSeq.Length : len;
                    for (var k = i; k < stop; k++) Blank(chars, k);
                    i = stop;
                    continue;
                }
            }

            // Rust raw strings r"...", r#"..."#, br#"..."#, and byte strings b"...", b'...'
            if (isRust && (c == 'r' || c == 'b') &&
                (i == 0 || !(char.IsLetterOrDigit(source[i - 1]) || source[i - 1] == '_')))
            {
                var p = i;
                if (source[p] == 'b' && p + 1 < len && source[p + 1] == 'r') p += 2;
                else if (source[p] == 'r') p += 1;
                else p = -1;

                if (p > 0)
                {
                    var hashes = 0;
                    while (p < len && source[p] == '#') { hashes++; p++; }
                    if (p < len && source[p] == '"')
                    {
                        var endSeq = "\"" + new string('#', hashes);
                        var endIdx = source.IndexOf(endSeq, p + 1, StringComparison.Ordinal);
                        var stop = endIdx >= 0 ? endIdx + endSeq.Length : len;
                        for (var k = i; k < stop; k++) Blank(chars, k);
                        i = stop;
                        continue;
                    }
                }
            }

            if (c == '/' && i + 1 < len && source[i + 1] == '/')
            {
                while (i < len && source[i] != '\n')
                    Blank(chars, i++);
                continue;
            }

            if (isPhp && c == '#')
            {
                while (i < len && source[i] != '\n')
                    Blank(chars, i++);
                continue;
            }

            if (c == '/' && i + 1 < len && source[i + 1] == '*')
            {
                var depth = 1;
                Blank(chars, i++); Blank(chars, i++);
                while (i < len && depth > 0)
                {
                    if (nestedBlockComments && source[i] == '/' && i + 1 < len && source[i + 1] == '*')
                    {
                        depth++;
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    if (source[i] == '*' && i + 1 < len && source[i + 1] == '/')
                    {
                        depth--;
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    Blank(chars, i++);
                }
                continue;
            }

            // Triple-quoted strings in Java / Kotlin / Swift
            if (hasTripleQuotes && c == '"' && i + 2 < len && source[i + 1] == '"' && source[i + 2] == '"')
            {
                Blank(chars, i++); Blank(chars, i++); Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '"' && i + 2 < len && source[i + 1] == '"' && source[i + 2] == '"')
                    {
                        Blank(chars, i++); Blank(chars, i++); Blank(chars, i++);
                        break;
                    }
                    Blank(chars, i++);
                }
                continue;
            }

            if (c == '"')
            {
                Blank(chars, i++);
                while (i < len)
                {
                    if (source[i] == '\\' && i + 1 < len)
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == '"')
                        break;
                }
                continue;
            }

            if (c == '\'')
            {
                if (isRust && !IsRustCharLiteral(source, i))
                {
                    i++;
                    continue;
                }

                Blank(chars, i++);
                while (i < len && source[i] != '\n')
                {
                    if (source[i] == '\\' && i + 1 < len)
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == '\'')
                        break;
                }
                continue;
            }

            if (c == '`' && (isJsTs || isGo))
            {
                Blank(chars, i++);
                while (i < len)
                {
                    if (isJsTs && source[i] == '\\' && i + 1 < len)
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        continue;
                    }
                    if (isJsTs && source[i] == '$' && i + 1 < len && source[i + 1] == '{')
                    {
                        Blank(chars, i++); Blank(chars, i++);
                        var exprDepth = 1;
                        while (i < len && exprDepth > 0)
                        {
                            if (source[i] == '\\' && i + 1 < len)
                            {
                                Blank(chars, i++); Blank(chars, i++);
                                continue;
                            }
                            if (source[i] == '{') exprDepth++;
                            else if (source[i] == '}') exprDepth--;
                            Blank(chars, i++);
                        }
                        continue;
                    }
                    var cur = source[i];
                    Blank(chars, i++);
                    if (cur == '`')
                        break;
                }
                continue;
            }

            if (isJsTs && c == '/' && !(i + 1 < len && source[i + 1] == '>') && CanStartJsRegex(chars, i))
            {
                var start = i;
                var scan = i + 1;
                var inClass = false;
                var closed = false;
                while (scan < len && source[scan] != '\n')
                {
                    if (source[scan] == '\\' && scan + 1 < len)
                    {
                        scan += 2;
                        continue;
                    }
                    if (source[scan] == '[') inClass = true;
                    else if (source[scan] == ']') inClass = false;
                    else if (source[scan] == '/' && !inClass)
                    {
                        scan++;
                        while (scan < len && char.IsLetter(source[scan])) scan++;
                        closed = true;
                        break;
                    }
                    scan++;
                }

                if (closed)
                {
                    for (var k = start; k < scan; k++)
                        Blank(chars, k);
                    i = scan;
                    continue;
                }
            }

            i++;
        }
    }

    private static bool IsRustCharLiteral(string source, int quoteIndex)
    {
        if (quoteIndex + 2 < source.Length && source[quoteIndex + 1] != '\\' && source[quoteIndex + 2] == '\'')
            return true;
        if (quoteIndex + 3 < source.Length && source[quoteIndex + 1] == '\\')
        {
            for (var k = quoteIndex + 2; k < Math.Min(source.Length, quoteIndex + 10); k++)
            {
                if (source[k] == '\'') return true;
                if (source[k] == '\n') return false;
            }
        }
        return false;
    }

    private static bool CanStartJsRegex(char[] maskedSoFar, int slashIndex)
    {
        var j = slashIndex - 1;
        while (j >= 0 && char.IsWhiteSpace(maskedSoFar[j]))
            j--;

        if (j < 0)
            return true;

        var prev = maskedSoFar[j];
        if (prev == '<')
            return false;
        if (prev is '(' or '[' or '{' or ',' or ';' or '=' or ':' or '!' or '?' or '&' or '|' or '~' or '+' or '-' or '*' or '%' or '>' or '^')
            return true;

        if (char.IsLetterOrDigit(prev) || prev == '_' || prev == '$')
        {
            var end = j;
            while (j >= 0 && (char.IsLetterOrDigit(maskedSoFar[j]) || maskedSoFar[j] == '_' || maskedSoFar[j] == '$'))
                j--;
            var word = new string(maskedSoFar, j + 1, end - j);
            return RegexPrecedingKeywords.Contains(word);
        }

        return false;
    }
}
