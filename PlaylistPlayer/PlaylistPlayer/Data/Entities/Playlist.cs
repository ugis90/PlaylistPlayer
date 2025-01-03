﻿using PlaylistPlayer.Auth.Model;
using System.ComponentModel.DataAnnotations;

namespace PlaylistPlayer.Data.Entities;

public class Playlist
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public ICollection<Song> Songs { get; set; } = new List<Song>();

    [Required]
    public required string UserId { get; set; }
    public MusicUser User { get; set; }

    public PlaylistDto ToDto()
    {
        return new PlaylistDto(Id, Name, Description, CreatedAt, CategoryId);
    }
}
