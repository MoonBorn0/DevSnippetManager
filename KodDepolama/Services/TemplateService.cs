using System;
using System.Collections.Generic;
using System.Text;

namespace DevSnippetManager.Services
{
    public sealed record TemplateVariable(string Name, string DefaultValue);

    public static class TemplateService
    {
        public static IReadOnlyList<TemplateVariable> Parse(string? code)
        {
            var result = new List<TemplateVariable>();
            if (string.IsNullOrEmpty(code)) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < code.Length - 1; i++)
            {
                if (code[i] != '$' || code[i + 1] != '{' || (i > 0 && code[i - 1] == '$')) continue;
                if (TryRead(code, i + 2, out var variable, out var end))
                {
                    if (seen.Add(variable.Name)) result.Add(variable);
                    i = end;
                }
            }
            return result;
        }

        public static string Fill(string code, IReadOnlyDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(code)) return code;
            var builder = new StringBuilder();
            for (var i = 0; i < code.Length; i++)
            {
                if (i + 2 < code.Length && code[i] == '$' && code[i + 1] == '$' && code[i + 2] == '{')
                {
                    builder.Append("${"); i += 2; continue;
                }
                if (code[i] == '$' && i + 1 < code.Length && code[i + 1] == '{' && TryRead(code, i + 2, out var variable, out var end))
                {
                    builder.Append(values.TryGetValue(variable.Name, out var value) ? value : variable.DefaultValue);
                    i = end; continue;
                }
                builder.Append(code[i]);
            }
            return builder.ToString();
        }

        private static bool TryRead(string code, int start, out TemplateVariable variable, out int end)
        {
            variable = new TemplateVariable(string.Empty, string.Empty); end = start;
            if (start >= code.Length || !(char.IsLetter(code[start]) || code[start] == '_')) return false;
            var i = start + 1;
            while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_')) i++;
            var name = code[start..i];
            var defaultValue = string.Empty;
            if (i < code.Length && code[i] == ':')
            {
                var defaultStart = ++i;
                while (i < code.Length && code[i] != '}') i++;
                if (i >= code.Length) return false;
                defaultValue = code[defaultStart..i];
            }
            else if (i >= code.Length || code[i] != '}') return false;
            variable = new TemplateVariable(name, defaultValue); end = i; return true;
        }
    }
}
