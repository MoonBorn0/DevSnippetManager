using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DevSnippetManager.Services.ProjectScan;

public enum BlockKind
{
    Method,
    Function,
    Class,
    Interface,
    Record,
    Struct,
    Enum,
    Property,
    Constructor,
    Procedure
}

public static class BlockKindTags
{
    public const string ScanTag = "proje-tarama";

    public static string ToTag(BlockKind kind)
    {
        return kind switch
        {
            BlockKind.Method => "metot",
            BlockKind.Function => "fonksiyon",
            BlockKind.Class => "sınıf",
            BlockKind.Interface => "arayüz",
            BlockKind.Record => "kayıt",
            BlockKind.Struct => "yapı",
            BlockKind.Enum => "enum",
            BlockKind.Property => "özellik",
            BlockKind.Constructor => "kurucu",
            BlockKind.Procedure => "prosedür",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Bilinmeyen blok türü.")
        };
    }
}

public sealed record ExtractedBlock(
    string Symbol,
    string DisplayName,
    BlockKind Kind,
    string Code,
    string Comment,
    int StartLine);

public sealed record ExtractionResult(IReadOnlyList<ExtractedBlock> Blocks, bool HasErrors);

public interface IBlockExtractor
{
    bool CanHandle(string language);

    ExtractionResult Extract(string source, string language);
}

public sealed record ScannedFile(
    string RelativePath,
    string Language,
    ExtractionResult? Result,
    bool Exists);

public sealed record ScanApplyResult(
    int Added,
    int Updated,
    int MarkedMissing,
    int Restored,
    int Unchanged,
    int Relinked);

public static class CodeNormalizer
{
    public static string Dedent(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var normalized = code.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var commonIndent = lines
            .Where(line => line.Trim().Length > 0)
            .Select(GetIndentLength)
            .DefaultIfEmpty(0)
            .Min();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd();
            if (line.Length > 0 && commonIndent > 0)
                line = RemoveIndent(line, commonIndent);
            lines[index] = line;
        }

        return string.Join('\n', lines);
    }

    public static string ContentHash(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Dedent(code)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int GetIndentLength(string line)
    {
        var count = 0;
        while (count < line.Length && (line[count] == ' ' || line[count] == '\t'))
            count++;
        return count;
    }

    private static string RemoveIndent(string line, int count)
    {
        var index = 0;
        while (index < line.Length && index < count && (line[index] == ' ' || line[index] == '\t'))
            index++;
        return line[index..];
    }
}
