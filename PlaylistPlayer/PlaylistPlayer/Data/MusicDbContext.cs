using PlaylistPlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace PlaylistPlayer.Data;

public class MusicDbContext(IConfiguration configuration) : DbContext
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<Song> Songs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("PostgreSQL"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Category>()
            .HasMany(c => c.Playlists)
            .WithOne(p => p.Category)
            .HasForeignKey(p => p.CategoryId);

        modelBuilder
            .Entity<Playlist>()
            .HasMany(p => p.Songs)
            .WithOne(s => s.Playlist)
            .HasForeignKey(s => s.PlaylistId);
    }
}