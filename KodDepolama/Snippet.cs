using System;

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

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Tags)
                ? $"[{Language}] {Title}"
                : $"[{Language}] {Title}  ({Tags})";
        }
    }
}
