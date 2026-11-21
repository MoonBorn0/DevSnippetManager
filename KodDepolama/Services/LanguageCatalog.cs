using System;
using System.Collections.Generic;

namespace DevSnippetManager.Services
{
    public static class LanguageCatalog
    {
        private static readonly IReadOnlyList<string> _builtInLanguages = new[]
        {
            "C#", "Java", "Python", "SQL", "JavaScript", "TypeScript", "HTML/CSS", "C", "C++", "Go", "Rust", "PHP", "Ruby", "Kotlin", "Swift", "Bash", "PowerShell", "JSON", "YAML", "XML", "Markdown", "Dockerfile", "Diğer"
        };

        public static IReadOnlyList<string> BuiltInLanguages => _builtInLanguages;

        public static string GetMonacoId(string? language)
        {
            var normalized = Normalize(language);
            return normalized switch
            {
                "C#" => "csharp",
                "Java" => "java",
                "Python" => "python",
                "SQL" => "sql",
                "JavaScript" => "javascript",
                "TypeScript" => "typescript",
                "HTML/CSS" => "html",
                "C" => "c",
                "C++" => "cpp",
                "Go" => "go",
                "Rust" => "rust",
                "PHP" => "php",
                "Ruby" => "ruby",
                "Kotlin" => "kotlin",
                "Swift" => "swift",
                "Bash" => "shell",
                "PowerShell" => "powershell",
                "JSON" => "json",
                "YAML" => "yaml",
                "XML" => "xml",
                "Markdown" => "markdown",
                "Dockerfile" => "dockerfile",
                _ => "plaintext"
            };
        }

        public static string Normalize(string? language)
        {
            var value = language?.Trim() ?? string.Empty;
            if (value.Length == 0)
                return "Diğer";

            foreach (var builtIn in _builtInLanguages)
            {
                if (string.Equals(value, builtIn, StringComparison.OrdinalIgnoreCase))
                    return builtIn;
            }

            return value;
        }
    }
}
