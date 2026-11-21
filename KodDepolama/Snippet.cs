using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;

namespace KodDepolama
{
    public class Snippet
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsFavorite { get; set; }
        public int UsageCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public string Folder { get; set; } = string.Empty;
        public string SourceRoot { get; set; } = string.Empty;
        public string SourceFile { get; set; } = string.Empty;
        public string SourceSymbol { get; set; } = string.Empty;
        public string SourceComment { get; set; } = string.Empty;
        public bool IsSourceMissing { get; set; }
        public DateTime? LastScannedAt { get; set; }

        [NotMapped]
        [JsonIgnore]
        public bool IsFromSource => !string.IsNullOrEmpty(SourceSymbol);

        [NotMapped]
        [JsonIgnore]
        public string CodePreview
        {
            get
            {
                var lines = (Code ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Take(3)
                    .ToList();
                if (lines.Count == 0) return string.Empty;

                var indent = lines.Select(line => line.TakeWhile(char.IsWhiteSpace).Count()).Min();
                return string.Join("\n", lines.Select(line =>
                {
                    var reduced = line.Length >= indent ? line[indent..].TrimEnd() : line.TrimEnd();
                    return reduced.Length > 80 ? reduced[..79] + "…" : reduced;
                }));
            }
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Tags)
                ? $"[{Language}] {Title}"
                : $"[{Language}] {Title}  ({Tags})";
        }
    }
}
