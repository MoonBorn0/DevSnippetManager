using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevSnippetManager.Services
{
    public static class LanguageDetector
    {
        public static string Detect(string? code)
        {
            const string other = "\u0044i\u011Fer";
            if (string.IsNullOrWhiteSpace(code)) return other;
            var text = code.Trim();
            var scores = LanguageCatalog.BuiltInLanguages.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
            void Add(string language, int points) => scores[language] += points;
            bool Has(string pattern, RegexOptions options = RegexOptions.Multiline) => Regex.IsMatch(text, pattern, options);

            if (Has(@"^\s*from\s+[\w.]+\s+import\s+|^\s*import\s+\w+(\s+as\s+\w+)?\s*$")) Add("Python", 6);
            if (Has(@"(^|\n)\s*(def\s+|class\s+\w+\s*\(|elif\b)|\bself\b|\bprint\s*\(")) Add("Python", 3);
            if (Has(@"(?<!-)\bSELECT\s+(?:\*|DISTINCT\b|[\w.]+\s*(?:,\s*[\w.]+|\([^)]*\)|\.[\w]+))\s+[\s\S]*?\bFROM\b(?!-)|(?<!-)\bINSERT\s+INTO\b(?!-)|(?<!-)\bUPDATE\s+\w+\s+SET\b(?!-)|(?<!-)\bDELETE\s+FROM\b(?!-)|(?<!-)\bCREATE\s+(TABLE|VIEW|INDEX|PROCEDURE)\b(?!-)|(?<!-)\bALTER\s+TABLE\b(?!-)", RegexOptions.IgnoreCase | RegexOptions.Singleline)) Add("SQL", 7);
            if (Has(@"(?<!-)\bJOIN\b[\s\S]+?(?<!-)\bON\b(?!-)", RegexOptions.IgnoreCase)) Add("SQL", 2);
            if (Has(@"(?<!-)\bGROUP\s+BY\b(?!-)|(?<!-)\bHAVING\b(?!-)|(?<!-)\bORDER\s+BY\b(?!-)|(?<!-)\bCOUNT\s*\(\s*\*\s*\)(?!-)", RegexOptions.IgnoreCase)) Add("SQL", 2);
            if (Has(@"\busing\s+System\b|\bvar\s+\w+\s*=") || Has(@"Console\.WriteLine|\busing\s+var\b|\basync\s+Task\b|string\[\]\s+args|\bnamespace\s+\w+\s*;|\{\s*get;\s*(?:init|set);\s*\}|\bList<|\.ToList\(\)|\bpublic\s+record\b|\bpublic\s+static\s+class\b|^\s*namespace\s+\w+\s*;|^\s*\[\w+(?:\(.*\))?\]\s*$[\r\n]+(?:[ \t]*[\r\n]+)*[ \t]*(?:public|private|protected|internal|class|void|async|static)\b")) Add("C#", 4);
            if (Has(@"\bprivate\s+(?:readonly|const)\b|\bDictionary<|\bTryGetValue\s*\(|\bout\s+var\b|\bclass\s+\w+<[^>]+>\s*\{")) Add("C#", 5);
            if (Has(@"(?:\w|\))\?(?:\?|\.)?(?=\s|\w)")) Add("C#", 1);
            if (Has(@"\bimport\s+java\.|\bpackage\s+(com|org)\.[\w.]+\s*;|@Override|System\.out|\bextends\b|\bimplements\b|\bArrayList<|\bfinal\s+|@(RestController|Controller|Service|Repository|Component|Autowired|GetMapping|PostMapping|RequestMapping|Entity|Id|Column|Data|Builder|SpringBootApplication)\b|ResponseEntity<|\bList<\w+>\s+\w+\s*=\s*new|\bString\s+\w+\s*=|\bprivate\s+final\s+\w+")) Add("Java", 5);
            if (Has(@"\bpublic\s+class\b")) { Add("C#", 1); Add("Java", 1); }
            if (Has(@"\b(function|const|let)\b|console\.log|document\.|addEventListener\b|\.map\s*\(|\bimport\s+[\w*{}, ]+\s+from\s+['""]|require\(|\b(?:if|for|while)\s*\([^)]*\)\s*\{\s*(?:return|const|let|var)\b")) Add("JavaScript", 3);
            if (Has(@"\b\w+\s*:\s*(string|number|boolean|any|void|unknown|\w+\[\])\b|:\s*Promise<|\bas\s+const\b|\bfunction\s+\w+\s*<|@(Component|Injectable)\(")) Add("TypeScript", 6);
            if (Has(@"\bexport\s+type\s+\w+(<[^>]+>)?\s*=|\binterface\s+\w+(<[^>]+>)?\s*\{|\btype\s+\w+(<[^>]+>)?\s*=[\s\S]*\|\s*\{")) Add("TypeScript", 6);
            if (Has(@"#include\s*<[^>]+\.h\s*>|\bprintf\s*\(|\bscanf\s*\(|\bmalloc\s*\(|\bint\s+main\s*\(|\b(strcpy|strlen|memcpy|sizeof|NULL)\b|#define\s+\w+|\bsize_t\b|\bstatic\s+(int|char|void|unsigned|long)\b|\bunsigned\s+(int|char|long)\b|\bstruct\s+\w+\s*\{|\btypedef\b")) Add("C", 4);
            if (Has(@"\b(cout|cin)\b|\btemplate\s*<|\bclass\s+\w+[ \t]*\{[\s\S]*\b(public|private|protected):|(?:^|\s)[\w:<>]+\s+\w+::\w+\s*\([^)]*\)|^\s*(?:public|private|protected):|#include\s*<(?:iostream|vector|string|map|set|algorithm)>", RegexOptions.Multiline)) Add("C++", 7);
            if (Has(@"\bstd::")) Add("C++", 2);
            if (Has(@"\bnamespace\s+\w+\s*\{[\s\S]*\bstd::", RegexOptions.Singleline)) Add("C++", 5);
            if (Has(@"^#!\s*/usr/bin/env\s+(bash|sh)\s*$|^#!\s*/bin/(bash|sh)\s*$|\bfi$|\bthen$|\bdone$|\$\([^\r\n]+\)|\$\{\w+\}[\s\S]*\becho\b")) Add("Bash", 5);
            if (Has(@"echo\s+\$\w+")) Add("Bash", 2);
            if (Has(@"\|\s*grep\b")) Add("Bash", 2);
            if (Has(@"&&\s*ls\b")) Add("Bash", 2);
            if (Has(@"\bcd\s+")) Add("Bash", 2);
            if (Has(@"\bls\s+")) Add("Bash", 2);
            if (Has(@"\bexport\s+\w+=")) Add("Bash", 2);
            if (Has(@"\bchmod\b")) Add("Bash", 2);
            if (Has(@"\bsudo\b")) Add("Bash", 2);
            if (Has(@"\b(Where-Object|Select-Object|ForEach-Object|Get-ChildItem)\b|\$\._|\bparam\s*\(|\[CmdletBinding\(\)\]|-(eq|ne|like)\b|\b(Write-Host|Write-Output)\b|\$PSVersionTable\b|\b(Get|Set|New)-\w+\b|\$env:")) Add("PowerShell", 7);
            if (Has(@"\bpackage\s+main\b|\bfunc\b|:=|\bmake\(chan|\bgo\s+func\b|<-\s*\w+|\w+\s*<-|\brange\s+\w+|\bfmt\.|\bpackage\s+\w+|\berr\s*!=\s*nil")) Add("Go", 4);
            if (Has(@"\bfn\s+|\bpub\s+fn\b|\blet\s+mut\b|\buse\s+std::|\bimpl\s+|println!|\bVec<|&str\b|\b(Option|Result)<")) Add("Rust", 9);
            if (text.Contains("<?php", StringComparison.OrdinalIgnoreCase)) Add("PHP", 8);
            if (Has(@"\bfunction\s+\w+\s*\([^)]*\$\w+|\$this->|\becho\s+")) Add("PHP", 4);
            var validJson = false;
            try { using var document = JsonDocument.Parse(text); if (document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array) { Add("JSON", 6); validJson = true; } } catch (JsonException) { }
            if (!validJson && Has(@"^\s*(?:\{\s*""[^""]+""\s*:|\[\s*\{)[\s\S]*\}\s*$", RegexOptions.Singleline)) Add("JSON", 6);
            var yamlLines = Regex.Matches(text, @"^[ \t]*[\w.-]+:(?!:)(?=[ \t]|$)(?![^\r\n]*;[ \t]*$)[^\r\n]*$", RegexOptions.Multiline).Count;
            var yamlKeyBeforeList = Has(@"^[ \t]*[\w.-]+:(?!:)(?=[ \t]|$)(?![^\r\n]*;[ \t]*$)[ \t]+\S[^\r\n]*$[\s\S]*^[ \t]*-[ \t]+", RegexOptions.Multiline);
            if (yamlLines >= 2 || Has(@"^---[ \t]*$", RegexOptions.Multiline) || (yamlLines >= 1 && yamlKeyBeforeList) || Has(@"^[a-z_][\w.-]*[ \t]*:[ \t]+\S[^\r\n]*$", RegexOptions.Multiline)) Add("YAML", 4);
            if (Has(@"^\s*(try|except|finally|elif|else|with|for .* in .*|while .*):\s*$|except\s+\w+(\s+as\s+\w+)?:|\bf[""']|\[[^\]]+\bfor\b[^\]]+\bin\b", RegexOptions.Multiline)) Add("Python", 3);
            var markdownNonList = Has(@"^\s*#{1,6}\s+\S|^\s*\d+\.\s+|\*\*[^*]+\*\*|\[[^\]]+\]\([^\)]+\)|^\s*```", RegexOptions.Multiline);
            var markdownList = Has(@"^\s*[-*]\s+", RegexOptions.Multiline) && !yamlKeyBeforeList;
            if (markdownNonList || markdownList) Add("Markdown", 4);
            if (Has(@"^\s*(RUN|CMD|COPY|WORKDIR|ENTRYPOINT|EXPOSE|ENV|ADD)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline) && Has(@"^\s*FROM\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Multiline)) Add("Dockerfile", 7);
            if (Has(@"<!DOCTYPE|<html\b|<head\b|<body\b|<div\b|<p\b|<(?:form|label|input|button|nav|ul|ol|li|a|span|header|footer|main|section|article|aside|h[1-6]|table|tr|td|th|img|br|hr)\b|[.#]?[a-z][\w-]*\s*\{[^{}]*:\s*[^;{}]+;", RegexOptions.IgnoreCase | RegexOptions.Singleline)) Add("HTML/CSS", 5);
            var xmlRoot = Has(@"^\s*<([A-Za-z_][\w:.-]*)(?:\s[^>]*)?>[\s\S]*</\1>\s*$", RegexOptions.Singleline);
            if (text.Contains("<?xml", StringComparison.OrdinalIgnoreCase) || (xmlRoot && !Has(@"<\s*(html|head|body|div|span|p|a|ul|ol|li|nav|header|footer|main|section|article|aside|h[1-6]|table|tr|td|th|thead|tbody|form|input|button|label|select|option|img|br|hr|script|style|link|meta)\b", RegexOptions.IgnoreCase))) Add("XML", 5);

            var priority = new[] { "TypeScript", "C#", "Java", "Python", "PowerShell", "Bash", "C++", "C", "SQL", "XML", "HTML/CSS", "Dockerfile", "Markdown", "YAML", "JSON", "JavaScript", "Go", "Rust", "PHP", other };
            var ranked = scores.OrderByDescending(x => x.Value).ThenBy(x => Array.IndexOf(priority, x.Key)).ToList();
            var best = ranked[0];
            var runnerUp = ranked.Count > 1 ? ranked[1].Value : 0;
            return best.Value >= 3 && best.Value > runnerUp ? best.Key : other;
        }
    }
}
