namespace PlaylistPlayer.Data.Entities;

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    // Only can be set/seen by admin
    public bool IsBlocked { get; set; }

    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();

    public CategoryDto ToDto()
    {
        return new CategoryDto(Id, Name, Description, CreatedAt);
    }
}