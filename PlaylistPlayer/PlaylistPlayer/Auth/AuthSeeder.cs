using Microsoft.AspNetCore.Identity;
using PlaylistPlayer.Auth.Model;

namespace PlaylistPlayer.Auth;

public class AuthSeeder(UserManager<MusicUser> userManager, RoleManager<IdentityRole> roleManager)
{
    public async Task SeedAsync()
    {
        await AddDefaultRolesAsync();
        await AddAdminUserAsync();
    }

    private async Task AddAdminUserAsync()
    {
        var newAdminUser = new MusicUser { UserName = "admin", Email = "admin@admin.com" };

        var existingAdminUser = await userManager.FindByNameAsync(newAdminUser.UserName);
        if (existingAdminUser is null)
        {
            var createAdminUserResult = await userManager.CreateAsync(
                newAdminUser,
                "VerySafePassword1!"
            );
            if (createAdminUserResult.Succeeded)
                await userManager.AddToRolesAsync(newAdminUser, MusicRoles.All);
        }
    }

    private async Task AddDefaultRolesAsync()
    {
        foreach (string role in MusicRoles.All)
        {
            var roleExists = await roleManager.RoleExistsAsync(role);
            if (!roleExists)
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}
