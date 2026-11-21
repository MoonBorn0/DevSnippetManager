using System;

namespace KodDepolama
{
    public class SnippetVersion
    {
        public int Id { get; set; }
        public int SnippetId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
