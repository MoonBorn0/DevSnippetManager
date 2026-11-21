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
        else if (string.Equals(lang, "Bash", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(lang, "Ruby", StringComparison.OrdinalIgnoreCase))
        {
            MaskHashStyle(source, chars, allowBackslashInSingleQuotes: string.Equals(lang, "Ruby", StringComparison.OrdinalIgnoreCase));
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
                {
                    Blank(chars, i);
                    i++;
                }
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
                {
                    Blank(chars, i++);
                }
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

            if (source[i] == '#')
            {
                while (i < len && source[i] != '\n')
                {
                    Blank(chars, i++);
                }
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

    private static void MaskHashStyle(string source, char[] chars, bool allowBackslashInSingleQuotes)
    {
        var i = 0;
        var len = source.Length;
        while (i < len)
        {
            if (source[i] == '#')
            {
                while (i < len && source[i] != '\n')
                {
                    Blank(chars, i++);
                }
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
                    if (allowBackslashInSingleQuotes && source[i] == '\\' && i + 1 < len)
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

    private static void MaskCStyle(string source, char[] chars, string lang)
    {
        var isJsTs = string.Equals(lang, "JavaScript", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(lang, "TypeScript", StringComparison.OrdinalIgnoreCase);
        var isGo = string.Equals(lang, "Go", StringComparison.OrdinalIgnoreCase);
        var isRust = string.Equals(lang, "Rust", StringComparison.OrdinalIgnoreCase);
        var isPhp = string.Equals(lang, "PHP", StringComparison.OrdinalIgnoreCase);
        var nestedBlockComments = isRust ||
                                  string.Equals(lang, "Kotlin", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(lang, "Swift", StringComparison.OrdinalIgnoreCase);

        var i = 0;
        var len = source.Length;

        while (i < len)
        {
            var c = source[i];

            if (c == '/' && i + 1 < len && source[i + 1] == '/')
            {
                while (i < len && source[i] != '\n')
                {
                    Blank(chars, i++);
                }
                continue;
            }

            if (isPhp && c == '#')
            {
                while (i < len && source[i] != '\n')
                {
                    Blank(chars, i++);
                }
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

            if (isJsTs && c == '/' && CanStartJsRegex(chars, i))
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
        if (prev is '(' or '[' or '{' or ',' or ';' or '=' or ':' or '!' or '?' or '&' or '|' or '~' or '+' or '-' or '*' or '%' or '<' or '>' or '^')
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
