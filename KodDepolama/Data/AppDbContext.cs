using KodDepolama;
using Microsoft.EntityFrameworkCore;

namespace DevSnippetManager.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Snippet> Snippets { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=localhost;Database=DevVaultDb;Trusted_Connection=True;TrustServerCertificate=True;");
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
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
