namespace PlaylistPlayer.Auth.Model;

public class MusicRoles
{
    public const string Admin = nameof(Admin);
    public const string MusicUser = nameof(MusicUser);

    public static readonly IReadOnlyCollection<string> All = [Admin, MusicUser];
}
