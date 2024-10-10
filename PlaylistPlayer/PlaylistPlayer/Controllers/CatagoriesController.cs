using PlaylistPlayer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PlaylistPlayer.Controllers;

public class CategoriesController(MusicDbContext dbContext) : Controller
{
    [HttpGet("/api/categories2/{categoryId}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetCategory(int categoryId)
    {
        var category = await dbContext.Categories.FindAsync(categoryId);
        if (category == null)
            return NotFound();

        return Ok(category.ToDto());
    }
}