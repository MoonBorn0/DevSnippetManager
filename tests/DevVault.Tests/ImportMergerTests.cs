using DevSnippetManager.Services;
using KodDepolama;
using System;
using System.Collections.Generic;
using Xunit;

namespace DevVault.Tests;

public class ImportMergerTests
{
    [Fact]
    public void Merge_UpdatesChanged_SkipsIdentical_AddsNew_AndDedupesIncoming()
    {
        var created = new DateTime(2025, 1, 1, 10, 0, 0);
        var existing = new List<Snippet>
        {
            new() { Id = 1, Title = "Hello", Language = "C#", Code = "v1", Tags = "t1", Description = "d1", CreatedAt = created },
            new() { Id = 2, Title = "Same", Language = "Python", Code = "print(1)", Tags = "", Description = "", CreatedAt = created }
        };

        var incoming = new List<Snippet>
        {
            new() { Id = 99, Title = "  hello ", Language = "C#", Code = "v2", Tags = "t1", Description = "d1" },
            new() { Id = 100, Title = "Same", Language = "Python", Code = "print(1)", Tags = "", Description = "" },
            new() { Id = 101, Title = "Brand New", Language = "SQL", Code = "SELECT 1;" },
            new() { Id = 102, Title = "brand new", Language = "SQL", Code = "SELECT 2;" }
        };

        var plan = ImportMerger.Plan(existing, incoming, ImportMode.Merge);

        Assert.Single(plan.ToUpdate);
        Assert.Equal(1, plan.ToUpdate[0].Id);
        Assert.Equal(created, plan.ToUpdate[0].CreatedAt);
        Assert.Equal("v2", plan.ToUpdate[0].Code);

        Assert.Single(plan.ToAdd);
        Assert.Equal(0, plan.ToAdd[0].Id);
        Assert.Equal("Brand New", plan.ToAdd[0].Title);

        Assert.Equal(2, plan.Skipped.Count);
    }

    [Fact]
    public void SkipDuplicates_SkipsExistingAndIncomingDuplicates()
    {
        var existing = new List<Snippet>
        {
            new() { Id = 1, Title = "Alpha", Language = "C#", Code = "old" }
        };
        var incoming = new List<Snippet>
        {
            new() { Id = 5, Title = "alpha", Language = "C#", Code = "new" },
            new() { Id = 6, Title = "Beta", Language = "C#", Code = "b1" },
            new() { Id = 7, Title = "BETA", Language = "C#", Code = "b2" }
        };

        var plan = ImportMerger.Plan(existing, incoming, ImportMode.SkipDuplicates);

        Assert.Empty(plan.ToUpdate);
        Assert.Single(plan.ToAdd);
        Assert.Equal(0, plan.ToAdd[0].Id);
        Assert.Equal("Beta", plan.ToAdd[0].Title);
        Assert.Equal(2, plan.Skipped.Count);
    }

    [Fact]
    public void ReplaceAll_AddsAllUniqueIncomingWithResetIds()
    {
        var existing = new List<Snippet>
        {
            new() { Id = 1, Title = "Old", Language = "C#", Code = "old" }
        };
        var incoming = new List<Snippet>
        {
            new() { Id = 10, Title = "Old", Language = "C#", Code = "new" },
            new() { Id = 11, Title = "OLD", Language = "C#", Code = "dup" },
            new() { Id = 12, Title = "Second", Language = "Go", Code = "package main" }
        };

        var plan = ImportMerger.Plan(existing, incoming, ImportMode.ReplaceAll);

        Assert.Empty(plan.ToUpdate);
        Assert.Equal(2, plan.ToAdd.Count);
        Assert.All(plan.ToAdd, s => Assert.Equal(0, s.Id));
        Assert.Single(plan.Skipped);
    }

    [Fact]
    public void Merge_PreservesMetadata_DetectsFolderChanges_AndUsesTurkishDedupe()
    {
        var created = new DateTime(2024, 5, 1);
        var lastUsed = new DateTime(2026, 2, 1);
        var existing = new List<Snippet>
        {
            new() { Id = 7, Title = "ışık", Language = "", Code = "c1", Folder = "F1", IsFavorite = true, UsageCount = 9, LastUsedAt = lastUsed, CreatedAt = created, UpdatedAt = created },
            new() { Id = 8, Title = "Title", Language = "C#", Code = "c2", Folder = "F2", CreatedAt = created }
        };

        var incoming = new List<Snippet>
        {
            // Matches "ışık" with empty language (both normalize to "Diğer"), null Folder triggers update
            new() { Id = 99, Title = "IŞIK", Language = "Diğer", Code = "c1", Folder = null! },
            // In tr-TR, "TITLE" lowercases to "tıtle" != "title", so NOT a duplicate of "Title"
            new() { Id = 100, Title = "TITLE", Language = "C#", Code = "c2" },
            new() { Id = 101, Title = "İstanbul", Language = "SQL", Code = "s1" },
            new() { Id = 102, Title = "istanbul", Language = "SQL", Code = "s2" },
            new() { Id = 103, Title = "ılışkı", Language = "Go", Code = "g1" },
            new() { Id = 104, Title = "ILIŞKI", Language = "Go", Code = "g2" }
        };

        var plan = ImportMerger.Plan(existing, incoming, ImportMode.Merge);

        var updated = Assert.Single(plan.ToUpdate);
        Assert.Equal(7, updated.Id);
        Assert.Equal(created, updated.CreatedAt);
        Assert.True(updated.IsFavorite);
        Assert.Equal(9, updated.UsageCount);
        Assert.Equal(lastUsed, updated.LastUsedAt);
        Assert.Equal(string.Empty, updated.Folder);
        Assert.True(updated.UpdatedAt >= created);

        // TITLE, İstanbul, ılışkı added; istanbul and ILIŞKI deduped as skipped
        Assert.Equal(3, plan.ToAdd.Count);
        Assert.Equal(2, plan.Skipped.Count);
    }

    [Fact]
    public void OldJsonExport_WithoutNewFields_DeserializesWithDefaults()
    {
        const string legacyJson = """[{"Id":3,"Title":"Old","Language":"C#","Code":"x","Tags":"","Description":""}]""";
        var list = System.Text.Json.JsonSerializer.Deserialize<List<Snippet>>(legacyJson);
        Assert.NotNull(list);
        var item = Assert.Single(list!);
        Assert.Equal(string.Empty, item.Folder);
        Assert.False(item.IsFavorite);
        Assert.Equal(0, item.UsageCount);
        Assert.Null(item.LastUsedAt);
    }

    [Fact]
    public void Merge_SourceSnippetsMatchBySourceIdentityNotTitle()
    {
        var existing = new List<Snippet>
        {
            new() { Id = 1, Title = "User Custom Title", Language = "C#", Folder = "Proj", SourceRoot = @"C:\Local\Proj", SourceFile = @"Services\A.cs", SourceSymbol = "Proj.A.Run()", Code = "v1" }
        };
        var incoming = new List<Snippet>
        {
            new() { Id = 9, Title = "A.Run", Language = "C#", Folder = "Proj", SourceRoot = @"D:\OtherPC\Proj", SourceFile = @"Services/A.cs", SourceSymbol = "Proj.A.Run()", Code = "v2" }
        };

        var plan = ImportMerger.Plan(existing, incoming, ImportMode.Merge);

        Assert.Empty(plan.ToAdd);
        var upd = Assert.Single(plan.ToUpdate);
        Assert.Equal(1, upd.Id);
        Assert.Equal("v2", upd.Code);
    }

    [Fact]
    public void Merge_SameTitleDifferentFilesNotDuplicates()
    {
        var existing = new List<Snippet>
        {
            new() { Id = 1, Title = "Run", Language = "C#", Folder = "Proj", SourceFile = "A.cs", SourceSymbol = "A.Run()", Code = "c1" }
        };
        var incoming = new List<Snippet>
        {
            new() { Id = 2, Title = "Run", Language = "C#", Folder = "Proj", SourceFile = "B.cs", SourceSymbol = "B.Run()", Code = "c2" }
        };

        var plan = ImportMerger.Plan(existing, incoming, ImportMode.Merge);

        Assert.Single(plan.ToAdd);
        Assert.Empty(plan.ToUpdate);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Merge_SourceAndManualNeverMatch()
    {
        var existing = new List<Snippet>
        {
            new() { Id = 1, Title = "Helper", Language = "C#", Folder = "Proj", Code = "manual" }
        };
        var incoming = new List<Snippet>
        {
            new() { Id = 2, Title = "Helper", Language = "C#", Folder = "Proj", SourceFile = "H.cs", SourceSymbol = "Helper()", Code = "scanned" }
        };

        var plan = ImportMerger.Plan(existing, incoming, ImportMode.Merge);

        Assert.Single(plan.ToAdd);
        Assert.Empty(plan.ToUpdate);
    }

    [Fact]
    public void Merge_KeepsLocalSourceRoot()
    {
        var existing = new List<Snippet>
        {
            new() { Id = 1, Title = "A.Run", Language = "C#", Folder = "Proj", SourceRoot = @"C:\MyMachine\Proj", SourceFile = "A.cs", SourceSymbol = "A.Run()", Code = "v1" }
        };
        var incoming = new List<Snippet>
        {
            new() { Id = 5, Title = "A.Run", Language = "C#", Folder = "Proj", SourceRoot = @"E:\OtherMachine\Proj", SourceFile = "A.cs", SourceSymbol = "A.Run()", Code = "v2" }
        };

        var plan = ImportMerger.Plan(existing, incoming, ImportMode.Merge);

        var upd = Assert.Single(plan.ToUpdate);
        Assert.Equal(@"C:\MyMachine\Proj", upd.SourceRoot);
    }
}
