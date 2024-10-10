namespace PlaylistPlayer.Data.Entities;

public class Song
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Artist { get; set; }
    public required int Duration { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public int PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = null!;

    public SongDto ToDto()
    {
        return new SongDto(Id, Title, Artist, Duration, CreatedAt, PlaylistId);
    }
}