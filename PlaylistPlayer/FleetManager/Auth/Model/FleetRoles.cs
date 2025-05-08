// FleetManager/Auth/Model/FleetRoles.cs
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using FleetManager.Data.Entities; // Add this

namespace FleetManager.Auth.Model;

public class FleetUser : IdentityUser
{
    public string? FamilyGroupId { get; set; }
    public virtual ICollection<Vehicle>? Vehicles { get; set; }
    public virtual ICollection<UserLocation>? Locations { get; set; }
}

public static class FleetRoles
{
    // *** FIX: Use Uppercase for consistency with Identity's normalization ***
    public const string Admin = "ADMIN";
    public const string FleetUser = "FLEETUSER";
    public const string Parent = "PARENT";
    public const string Teenager = "TEENAGER";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        Admin,
        FleetUser,
        Parent,
        Teenager
    };
}
