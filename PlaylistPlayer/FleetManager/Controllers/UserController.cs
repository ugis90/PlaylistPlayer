using System.Security.Claims;
using FleetManager.Auth.Model;
using FleetManager.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using FleetManager.Data.Entities;

namespace FleetManager.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UserController(UserManager<FleetUser> userManager, FleetDbContext dbContext)
    : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = $"{FleetRoles.Admin},{FleetRoles.Parent}")]
    public async Task<IActionResult> GetFamilyMembers()
    {
        try
        {
            var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized("User ID not found.");

            var currentUser = await userManager.FindByIdAsync(currentUserId);
            if (currentUser == null)
                return Unauthorized("Current user not found.");

            bool isAdmin = User.IsInRole(FleetRoles.Admin);
            string? familyGroupId = currentUser.FamilyGroupId;

            if (!isAdmin && string.IsNullOrEmpty(familyGroupId))
            {
                Console.WriteLine(
                    $"User {currentUser.UserName} is Parent/Other but has no FamilyGroupId."
                );
                return Ok(new List<object>());
            }

            List<string> userIdsInScope;
            if (isAdmin)
            {
                userIdsInScope = await dbContext.Users.Select(u => u.Id).ToListAsync();
            }
            else
            {
                userIdsInScope = await dbContext.Users
                    .Where(u => u.FamilyGroupId == familyGroupId)
                    .Select(u => u.Id)
                    .ToListAsync();
            }

            if (!userIdsInScope.Any())
            {
                return Ok(new List<object>());
            }

            var usersInfo = await dbContext.Users
                .Where(u => userIdsInScope.Contains(u.Id))
                .Select(
                    u =>
                        new
                        {
                            u.Id,
                            u.UserName,
                            u.Email
                        }
                )
                .ToListAsync();

            var latestLocations = await dbContext.UserLocations
                .Where(l => userIdsInScope.Contains(l.UserId))
                .GroupBy(l => l.UserId)
                .Select(g => g.OrderByDescending(l => l.Timestamp).First())
                .ToDictionaryAsync(
                    l => l.UserId,
                    l =>
                        new
                        {
                            l.Latitude,
                            l.Longitude,
                            l.Timestamp,
                            l.Speed,
                            l.Heading
                        }
                );

            var familyMembersData = new List<object>();
            foreach (var userInfo in usersInfo)
            {
                var user = await userManager.FindByIdAsync(userInfo.Id);
                if (user == null)
                    continue;

                var roles = await userManager.GetRolesAsync(user);
                latestLocations.TryGetValue(userInfo.Id, out var lastLocation);

                familyMembersData.Add(
                    new
                    {
                        userInfo.Id,
                        userInfo.UserName,
                        userInfo.Email,
                        Roles = roles,
                        LastLocation = lastLocation
                    }
                );
            }

            return Ok(familyMembersData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetFamilyMembers: {ex.ToString()}");
            return StatusCode(
                500,
                "An internal server error occurred while fetching family members."
            );
        }
    }

    [HttpGet("locations")]
    [Authorize(Roles = $"{FleetRoles.Admin},{FleetRoles.Parent}")]
    public async Task<IActionResult> GetFamilyLocations()
    {
        try
        {
            var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized();

            var currentUser = await userManager.FindByIdAsync(currentUserId);
            if (currentUser == null)
                return Unauthorized();

            string? familyGroupId = null;
            bool isAdmin = User.IsInRole(FleetRoles.Admin);

            if (!isAdmin)
            {
                familyGroupId = currentUser.FamilyGroupId;
                if (string.IsNullOrEmpty(familyGroupId))
                {
                    return Ok(new List<object>());
                }
            }

            IQueryable<UserLocation> locationQuery = dbContext.UserLocations
                .Include(l => l.User)
                .OrderByDescending(l => l.Timestamp);

            if (!isAdmin)
            {
                locationQuery = locationQuery.Where(l => l.User.FamilyGroupId == familyGroupId);
            }

            var latestLocations = await locationQuery
                .GroupBy(l => l.UserId)
                .Select(g => g.First())
                .ToListAsync();

            return Ok(latestLocations);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetFamilyLocations: {ex}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost("invite")]
    [Authorize(Roles = $"{FleetRoles.Admin},{FleetRoles.Parent}")]
    public async Task<IActionResult> InviteUserToFamily([FromBody] InviteUserDto dto)
    {
        try
        {
            var inviterId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrEmpty(inviterId))
                return Unauthorized();

            var inviter = await userManager.FindByIdAsync(inviterId);
            if (inviter == null || string.IsNullOrEmpty(inviter.FamilyGroupId))
            {
                return BadRequest("Inviter must belong to a family group to invite others.");
            }

            var familyGroupId = inviter.FamilyGroupId;

            var userToInvite = await userManager.FindByEmailAsync(dto.Email);
            if (userToInvite == null)
            {
                return NotFound($"User with email {dto.Email} not found.");
            }

            userToInvite.FamilyGroupId = familyGroupId;
            var updateResult = await userManager.UpdateAsync(userToInvite);
            if (!updateResult.Succeeded)
            {
                Console.WriteLine(
                    $"Error updating user FamilyGroupId: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}"
                );
                return StatusCode(500, "Failed to add user to family group.");
            }

            if (string.IsNullOrEmpty(dto.Role))
                return Ok(
                    new { message = $"User {userToInvite.UserName} added to family successfully." }
                );
            if (!FleetRoles.All.Contains(dto.Role))
            {
                return BadRequest("Invalid role specified.");
            }
            var currentRoles = await userManager.GetRolesAsync(userToInvite);
            var removeResult = await userManager.RemoveFromRolesAsync(userToInvite, currentRoles);
            if (!removeResult.Succeeded)
            {
                Console.WriteLine($"Warn: Failed to remove old roles for {userToInvite.UserName}");
            }
            var addResult = await userManager.AddToRoleAsync(userToInvite, dto.Role);
            if (!addResult.Succeeded)
            {
                Console.WriteLine($"Error adding role {dto.Role} to {userToInvite.UserName}");
            }

            return Ok(
                new { message = $"User {userToInvite.UserName} added to family successfully." }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in InviteUserToFamily: {ex}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}

public class InviteUserDto
{
    public string Email { get; set; } = string.Empty;
    public string? Role { get; set; }
}
