// FleetManager/Auth/AuthSeeder.cs
using FleetManager.Auth.Model;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System.Linq; // Add Linq
using System; // Add Console

namespace FleetManager.Auth;

public class AuthSeeder(UserManager<FleetUser> userManager, RoleManager<IdentityRole> roleManager)
{
    public async Task SeedAsync()
    {
        await AddDefaultRolesAsync();
        await AddAdminUserAsync();
    }

    private async Task AddAdminUserAsync()
    {
        var newAdminUser = new FleetUser { UserName = "admin", Email = "admin@admin.com" };

        var existingAdminUser = await userManager.FindByNameAsync(newAdminUser.UserName);
        if (existingAdminUser == null)
        {
            var createAdminUserResult = await userManager.CreateAsync(
                newAdminUser,
                "VerySafeP@ssw0rd1"
            );
            if (createAdminUserResult.Succeeded)
            {
                Console.WriteLine("Admin user created successfully.");
                // Add all roles (ensure they exist first from AddDefaultRolesAsync)
                var addRolesResult = await userManager.AddToRolesAsync(
                    newAdminUser,
                    FleetRoles.All
                );
                Console.WriteLine(
                    !addRolesResult.Succeeded
                        ? $"Failed to add roles to admin: {string.Join(", ", addRolesResult.Errors.Select(e => e.Description))}"
                        : "Assigned all roles to admin."
                );
            }
            else
            {
                Console.WriteLine(
                    $"Admin user creation failed: {string.Join(", ", createAdminUserResult.Errors.Select(e => e.Description))}"
                );
            }
        }
        else
        {
            Console.WriteLine("Admin user already exists.");
            // Ensure existing admin has all roles
            var currentRoles = await userManager.GetRolesAsync(existingAdminUser);
            var rolesToAdd = FleetRoles.All
                .Except(currentRoles.Select(r => r.ToUpperInvariant()))
                .ToArray(); // Compare uppercase
            if (rolesToAdd.Length > 0)
            {
                var addRolesResult = await userManager.AddToRolesAsync(
                    existingAdminUser,
                    rolesToAdd
                );
                Console.WriteLine(
                    !addRolesResult.Succeeded
                        ? $"Failed to add missing roles to existing admin: {string.Join(", ", addRolesResult.Errors.Select(e => e.Description))}"
                        : $"Added missing roles ({string.Join(", ", rolesToAdd)}) to existing admin."
                );
            }
        }
    }

    private async Task AddDefaultRolesAsync()
    {
        foreach (string roleNameUpper in FleetRoles.All) // Constants are already uppercase
        {
            var roleExists = await roleManager.RoleExistsAsync(roleNameUpper);
            if (!roleExists)
            {
                Console.WriteLine($"Creating role: {roleNameUpper}");
                // Create with uppercase name, Identity handles normalization
                await roleManager.CreateAsync(new IdentityRole(roleNameUpper));
            }
            else
            {
                // *** FIX: Ensure existing role name and normalized name are uppercase ***
                var role = await roleManager.FindByNameAsync(roleNameUpper);
                if (
                    role == null
                    || (role.Name == roleNameUpper && role.NormalizedName == roleNameUpper)
                )
                    continue;
                Console.WriteLine($"Correcting case for role: {role.Name} -> {roleNameUpper}");
                role.Name = roleNameUpper;
                role.NormalizedName = roleNameUpper; // Explicitly set normalized name
                await roleManager.UpdateAsync(role);
            }
        }
    }
}
