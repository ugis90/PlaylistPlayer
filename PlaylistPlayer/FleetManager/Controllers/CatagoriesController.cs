using FleetManager.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Controllers;

public class VehiclesController(FleetDbContext dbContext) : Controller
{
    [HttpGet("/api/categories2/{categoryId}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetCategory(int categoryId)
    {
        var category = await dbContext.Vehicles.FindAsync(categoryId);
        if (category == null)
            return NotFound();

        return Ok(category.ToDto());
    }
}
