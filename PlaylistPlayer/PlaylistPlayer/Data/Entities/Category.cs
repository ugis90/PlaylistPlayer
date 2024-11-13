using System.ComponentModel.DataAnnotations;
using PlaylistPlayer.Auth.Model;

namespace PlaylistPlayer.Data.Entities;

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public bool IsBlocked { get; set; } // Only can be set/seen by admin

    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();

    [Required]
    public required string UserId { get; set; }
    public MusicUser User { get; set; }

    public CategoryDto ToDto()
    {
        return new CategoryDto(Id, Name, Description, CreatedAt);
    }
}
