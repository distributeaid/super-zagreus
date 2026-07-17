using DA.NA.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DA.NA.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api")]
public class ReferenceDataController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReferenceDataController(AppDbContext db) => _db = db;

    /// <summary>Returns all categories with their item types. Drives the category/item picker in the UI.</summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.ItemTypes
            .Include(i => i.DefaultUnit)
            .GroupBy(i => i.Category)
            .Select(g => new
            {
                Category = g.Key,
                Items = g.Select(i => new
                {
                    i.Id,
                    i.Name,
                    i.StrapiId,
                    DefaultUnit = new { i.DefaultUnit.Id, i.DefaultUnit.Name, i.DefaultUnit.Dimension }
                }).OrderBy(i => i.Name).ToList()
            })
            .OrderBy(g => g.Category)
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>Returns all units with conversion factors. Drives the unit picker in the UI.</summary>
    [HttpGet("units")]
    public async Task<IActionResult> GetUnits()
    {
        var units = await _db.Units
            .Select(u => new { u.Id, u.Name, u.Dimension, u.ToBaseFactor })
            .OrderBy(u => u.Dimension).ThenBy(u => u.Name)
            .ToListAsync();

        return Ok(units);
    }
}
