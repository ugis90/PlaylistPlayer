using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using PlaylistPlayer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using PlaylistPlayer.Auth.Model;

namespace PlaylistPlayer.Data;

public class MusicDbContext(IConfiguration configuration) : IdentityDbContext<MusicUser>
{
    public DbSet<Category> Categories { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<Song> Songs { get; set; }
    public DbSet<Session> Sessions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("PostgreSQL"));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder
            .Entity<Category>()
            .HasMany(c => c.Playlists)
            .WithOne(p => p.Category)
            .HasForeignKey(p => p.CategoryId);

        builder
            .Entity<Playlist>()
            .HasMany(p => p.Songs)
            .WithOne(s => s.Playlist)
            .HasForeignKey(s => s.PlaylistId);
    }
}
