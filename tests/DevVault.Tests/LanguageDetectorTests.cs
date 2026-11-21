using DevSnippetManager.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace DevVault.Tests;

public class LanguageDetectorTests
{
    public static IEnumerable<object[]> BlindSamples()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "samples.txt");
        Assert.True(File.Exists(path), $"TestData file not found: {path}");

        string? currentLang = null;
        var buffer = new StringBuilder();
        var index = 0;

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                if (currentLang != null)
                {
                    index++;
                    yield return new object[] { index, currentLang, buffer.ToString().TrimEnd() };
                    buffer.Clear();
                }
                currentLang = line[4..].Trim();
            }
            else if (currentLang != null)
            {
                buffer.AppendLine(line);
            }
        }

        if (currentLang != null)
        {
            index++;
            yield return new object[] { index, currentLang, buffer.ToString().TrimEnd() };
        }
    }

    [Theory]
    [MemberData(nameof(BlindSamples))]
    public void Detect_MatchesBlindSamples(int sampleNumber, string expectedLanguage, string code)
    {
        var detected = LanguageDetector.Detect(code);
        Assert.True(
            string.Equals(expectedLanguage, detected, StringComparison.Ordinal),
            $"Sample #{sampleNumber}: expected '{expectedLanguage}', got '{detected}'");
    }

    [Theory]
    [InlineData("Diğer", null)]
    [InlineData("Diğer", "")]
    [InlineData("Diğer", "   \n  ")]
    [InlineData("Diğer", "Merhaba dünya, bugün hava çok güzel.")]
    [InlineData("C#", "using System.Text.Json;\npublic class Cache<T> { private readonly Dictionary<string, T> _items = new(); }")]
    [InlineData("Python", "class Stack:\n    def __init__(self):\n        self.items = []\n    def push(self, x):\n        self.items.append(x)")]
    [InlineData("SQL", "UPDATE accounts SET balance = balance - 100 WHERE id = 42;\nDELETE FROM sessions WHERE expires_at < NOW();")]
    [InlineData("JavaScript", "const express = require('express');\nconst app = express();\napp.get('/', (req, res) => res.send('ok'));")]
    [InlineData("TypeScript", "enum Status { Active, Inactive }\nexport class UserService {\n  getUser(id: number): User | undefined { return undefined; }\n}")]
    [InlineData("Java", "package com.example.demo;\nimport org.springframework.web.bind.annotation.GetMapping;\n@RestController\npublic class HelloController { @GetMapping(\"/hi\") public String hi() { return \"hi\"; } }")]
    [InlineData("C", "#include <stdio.h>\nstruct node { int value; struct node *next; };\nvoid print_list(struct node *head) { while (head) { printf(\"%d\\n\", head->value); head = head->next; } }")]
    [InlineData("C++", "#include <algorithm>\n#include <vector>\nint main() { std::vector<int> v = {3, 1, 2}; std::sort(v.begin(), v.end()); return v[0]; }")]
    [InlineData("Go", "package main\nimport (\"fmt\"\n\"net/http\")\nfunc handler(w http.ResponseWriter, r *http.Request) { fmt.Fprintf(w, \"hello\") }")]
    [InlineData("Rust", "use std::fs;\nfn read_config(path: &str) -> Result<String, std::io::Error> { let content = fs::read_to_string(path)?; Ok(content) }")]
    [InlineData("PHP", "<?php\nnamespace App\\Controllers;\nclass UserController extends Controller { public function show($id) { return User::find($id); } }")]
    [InlineData("Bash", "#!/bin/bash\nset -euo pipefail\nfor file in logs/*.log; do gzip \"$file\"; done\necho \"done\"")]
    [InlineData("PowerShell", "$services = Get-Service | Where-Object { $_.Status -eq 'Running' }\nforeach ($s in $services) { Write-Output $s.Name }")]
    [InlineData("JSON", "{\"name\": \"x\", \"version\": 1, \"items\": [1, 2, 3]}")]
    [InlineData("YAML", "apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: web\nspec:\n  replicas: 3")]
    [InlineData("XML", "<configuration>\n  <appSettings>\n    <add key=\"Mode\" value=\"Debug\" />\n  </appSettings>\n</configuration>")]
    [InlineData("HTML/CSS", "<form action=\"/login\" method=\"post\">\n  <label for=\"u\">User</label>\n  <input id=\"u\" name=\"user\">\n</form>")]
    [InlineData("Dockerfile", "FROM python:3.12-slim\nWORKDIR /app\nCOPY requirements.txt .\nRUN pip install -r requirements.txt\nCMD [\"python\", \"main.py\"]")]
    [InlineData("Markdown", "# Project Notes\n\n## Install\n\n* open the app\n* sign in\n\nSee [docs](https://example.com) for more.")]
    public void Detect_HandlesExtraSamples(string expected, string? code)
    {
        Assert.Equal(expected, LanguageDetector.Detect(code));
    }
}
