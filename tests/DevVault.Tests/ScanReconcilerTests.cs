using DevSnippetManager.Services.ProjectScan;
using KodDepolama;
using System;
using System.Collections.Generic;
using Xunit;

namespace DevVault.Tests;

public class ScanReconcilerTests
{
    private const string Root = @"C:\Projects\Demo";
    private const string Folder = "Demo";
    private static readonly DateTime Now = new(2026, 11, 21, 23, 55, 0);

    private static ExtractedBlock MakeBlock(string symbol, string display, string code, string comment = "", BlockKind kind = BlockKind.Method)
        => new(symbol, display, kind, code, comment, 1);

    private static Snippet MakeExisting(int id, string file, string symbol, string code, string comment = "", string? description = null, bool missing = false)
        => new()
        {
            Id = id,
            Title = symbol,
            Language = "C#",
            Folder = Folder,
            Tags = "proje-tarama, metot, A.cs",
            Code = code,
            SourceComment = comment,
            Description = description ?? comment,
            SourceRoot = Root,
            SourceFile = file,
            SourceSymbol = symbol,
            IsSourceMissing = missing,
            CreatedAt = Now.AddDays(-1),
            UpdatedAt = Now.AddDays(-1)
        };

    [Fact]
    public void Reconcile_NewBlocksInserted()
    {
        var file = new ScannedFile(@"Services\A.cs", "C#", new ExtractionResult(new[]
        {
            MakeBlock("Demo.A.Run()", "A.Run", "public void Run()\n{\n    DoWork();\n}", "Çalıştırır")
        }, false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { file }, Array.Empty<Snippet>(), Now);

        var ins = Assert.Single(plan.ToInsert);
        Assert.Equal("A.Run", ins.Title);
        Assert.Equal("C#", ins.Language);
        Assert.Equal(Folder, ins.Folder);
        Assert.Equal("proje-tarama, metot, A.cs", ins.Tags);
        Assert.Equal("Çalıştırır", ins.Description);
        Assert.Equal("Çalıştırır", ins.SourceComment);
        Assert.Equal(Root, ins.SourceRoot);
        Assert.Equal(@"Services\A.cs", ins.SourceFile);
        Assert.Equal("Demo.A.Run()", ins.SourceSymbol);
        Assert.Equal(Now, ins.LastScannedAt);
    }

    [Fact]
    public void Reconcile_SecondScanIsNoOp()
    {
        var existing = MakeExisting(1, "A.cs", "A.Run()", "code1", "comment1");
        var file = new ScannedFile("A.cs", "C#", new ExtractionResult(new[]
        {
            MakeBlock("A.Run()", "A.Run", "code1", "comment1")
        }, false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { file }, new[] { existing }, Now);

        Assert.Empty(plan.ToInsert);
        Assert.Empty(plan.ToUpdate);
        Assert.Empty(plan.MarkMissing);
        Assert.Empty(plan.ClearMissing);
        Assert.Single(plan.Touch, 1);
        Assert.Equal(1, plan.Unchanged);
    }

    [Fact]
    public void Reconcile_ChangedCodeUpdated()
    {
        var existing = MakeExisting(1, "A.cs", "A.Run()", "code1");
        var file = new ScannedFile("A.cs", "C#", new ExtractionResult(new[]
        {
            MakeBlock("A.Run()", "A.Run", "code2")
        }, false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { file }, new[] { existing }, Now);

        var upd = Assert.Single(plan.ToUpdate);
        Assert.Equal(1, upd.Id);
        Assert.Equal("code2", upd.Code);
        Assert.NotSame(existing, upd);
    }

    [Fact]
    public void Reconcile_PreservesUserOwnedFields()
    {
        var existing = MakeExisting(1, "A.cs", "A.Run()", "code1");
        existing.Title = "Kullanıcı Başlığı";
        existing.Tags = "özel-etiket";
        existing.Folder = "ÖzelKlasör";
        existing.IsFavorite = true;
        existing.UsageCount = 7;

        var file = new ScannedFile("A.cs", "C#", new ExtractionResult(new[]
        {
            MakeBlock("A.Run()", "A.Run", "code2")
        }, false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { file }, new[] { existing }, Now);

        var upd = Assert.Single(plan.ToUpdate);
        Assert.Equal("Kullanıcı Başlığı", upd.Title);
        Assert.Equal("özel-etiket", upd.Tags);
        Assert.Equal("ÖzelKlasör", upd.Folder);
        Assert.True(upd.IsFavorite);
        Assert.Equal(7, upd.UsageCount);
    }

    [Fact]
    public void Reconcile_DescriptionUpdatedOnlyIfUntouched()
    {
        var untouched = MakeExisting(1, "A.cs", "A.M1()", "c1", comment: "old", description: "old");
        var editedByUser = MakeExisting(2, "A.cs", "A.M2()", "c2", comment: "old", description: "Benim notum");

        var file = new ScannedFile("A.cs", "C#", new ExtractionResult(new[]
        {
            MakeBlock("A.M1()", "A.M1", "c1", "new comment"),
            MakeBlock("A.M2()", "A.M2", "c2", "new comment")
        }, false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { file }, new[] { untouched, editedByUser }, Now);

        Assert.Equal(2, plan.ToUpdate.Count);
        Assert.Equal("new comment", plan.ToUpdate.Find(s => s.Id == 1)!.Description);
        Assert.Equal("Benim notum", plan.ToUpdate.Find(s => s.Id == 2)!.Description);
    }

    [Fact]
    public void Reconcile_MissingBlockMarked()
    {
        var existing = MakeExisting(1, "A.cs", "A.Gone()", "code1");
        var file = new ScannedFile("A.cs", "C#", new ExtractionResult(Array.Empty<ExtractedBlock>(), false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { file }, new[] { existing }, Now);

        Assert.Single(plan.MarkMissing, 1);
    }

    [Fact]
    public void Reconcile_ReappearingBlockClearsMissing()
    {
        var existing = MakeExisting(1, "A.cs", "A.Back()", "code1", missing: true);
        var file = new ScannedFile("A.cs", "C#", new ExtractionResult(new[]
        {
            MakeBlock("A.Back()", "A.Back", "code1")
        }, false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { file }, new[] { existing }, Now);

        Assert.Single(plan.ClearMissing, 1);
        Assert.Empty(plan.MarkMissing);
    }

    [Fact]
    public void Reconcile_RenamedWithSameCodeRelinked()
    {
        var existing = MakeExisting(1, "Old.cs", "A.OldName()", "public void X()\n{\n    Do();\n}", comment: "c", description: "Benim açıklamam");
        var fileOld = new ScannedFile("Old.cs", "C#", new ExtractionResult(Array.Empty<ExtractedBlock>(), false), true);
        var fileNew = new ScannedFile("New.cs", "C#", new ExtractionResult(new[]
        {
            MakeBlock("A.NewName()", "A.NewName", "public void X()\n{\n    Do();\n}")
        }, false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { fileOld, fileNew }, new[] { existing }, Now);

        Assert.Equal(1, plan.Relinked);
        Assert.Empty(plan.ToInsert);
        Assert.Empty(plan.MarkMissing);
        var upd = Assert.Single(plan.ToUpdate);
        Assert.Equal(1, upd.Id);
        Assert.Equal("New.cs", upd.SourceFile);
        Assert.Equal("A.NewName()", upd.SourceSymbol);
        Assert.Equal("Benim açıklamam", upd.Description);
    }

    [Fact]
    public void Reconcile_RenamedAndChangedNotRelinked()
    {
        var existing = MakeExisting(1, "A.cs", "A.OldName()", "code1");
        var file = new ScannedFile("A.cs", "C#", new ExtractionResult(new[]
        {
            MakeBlock("A.NewName()", "A.NewName", "code2")
        }, false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { file }, new[] { existing }, Now);

        Assert.Equal(0, plan.Relinked);
        Assert.Single(plan.ToInsert);
        Assert.Single(plan.MarkMissing, 1);
    }

    [Fact]
    public void Reconcile_SkippedOrErrorFileDoesNotMarkMissing()
    {
        var s1 = MakeExisting(1, "Skipped.cs", "A.M1()", "c1");
        var s2 = MakeExisting(2, "Broken.cs", "A.M2()", "c2");

        var skipped = new ScannedFile("Skipped.cs", "C#", null, Exists: true);
        var broken = new ScannedFile("Broken.cs", "C#", new ExtractionResult(Array.Empty<ExtractedBlock>(), HasErrors: true), Exists: true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { skipped, broken }, new[] { s1, s2 }, Now);

        Assert.Equal(1, plan.SkippedFiles);
        Assert.Empty(plan.MarkMissing);
    }

    [Fact]
    public void Reconcile_DeletedFileMarksMissing()
    {
        var existing = MakeExisting(1, "Deleted.cs", "A.M1()", "c1");
        var deleted = new ScannedFile("Deleted.cs", "C#", null, Exists: false);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { deleted }, new[] { existing }, Now);

        Assert.Equal(0, plan.SkippedFiles);
        Assert.Single(plan.MarkMissing, 1);
    }

    [Fact]
    public void Reconcile_FilesOutsideScanUntouched()
    {
        var outside = MakeExisting(1, "Other.cs", "A.M1()", "c1");
        var scanned = new ScannedFile("Scanned.cs", "C#", new ExtractionResult(Array.Empty<ExtractedBlock>(), false), true);

        var plan = ScanReconciler.Reconcile(Root, Folder, new[] { scanned }, new[] { outside }, Now);

        Assert.Empty(plan.MarkMissing);
        Assert.Empty(plan.ToUpdate);
        Assert.Empty(plan.ToInsert);
    }
}
