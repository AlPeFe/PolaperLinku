using Microsoft.EntityFrameworkCore;

namespace PolaperLinku.Api.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<Folder> Folders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Favorite>()
            .HasOne(f => f.Folder)
            .WithMany(fo => fo.Favorites)
            .HasForeignKey(f => f.FolderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
