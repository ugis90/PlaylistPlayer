using Microsoft.AspNetCore.Identity;
using FleetManager.Data.Entities;

namespace FleetManager.Auth.Model;

public class FleetUser : IdentityUser
{
    public string? FamilyGroupId { get; set; }
    public virtual ICollection<Vehicle>? Vehicles { get; set; }
    public virtual ICollection<UserLocation>? Locations { get; set; }
}

public static class FleetRoles
{
    public const string Admin = "ADMIN";
    public const string FleetUser = "FLEETUSER";
    public const string Parent = "PARENT";
    public const string YoungDriver = "YOUNGDRIVER";

    public static readonly IReadOnlyCollection<string> All =
    [
        Admin,
        FleetUser,
        Parent,
        YoungDriver
    ];
}
