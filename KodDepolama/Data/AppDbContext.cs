using KodDepolama;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace DevSnippetManager.Data
{
    public class AppDbContext : DbContext
    {
        private readonly string _databasePath;

        public DbSet<Snippet> Snippets { get; set; }

        public AppDbContext(string databasePath)
        {
            _databasePath = databasePath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var databaseFolder = Path.GetDirectoryName(Path.GetFullPath(_databasePath));
                if (!string.IsNullOrWhiteSpace(databaseFolder))
                    Directory.CreateDirectory(databaseFolder);

                optionsBuilder.UseSqlite($"Data Source={_databasePath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Snippet>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Language).HasMaxLength(50);
                entity.Property(e => e.Tags).HasMaxLength(200);
                entity.Property(e => e.Folder).HasMaxLength(100);
                entity.Ignore(e => e.CodePreview);
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
