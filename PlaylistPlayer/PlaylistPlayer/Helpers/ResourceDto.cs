namespace PlaylistPlayer.Helpers;

public record ResourceDto<T>(T resource, IReadOnlyCollection<LinkDto> Links);