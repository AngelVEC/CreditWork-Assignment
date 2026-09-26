using CreditWorks.Api.Data;
using CreditWorks.Api.Dtos;
using CreditWorks.Api.Models;
using CreditWorks.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Controllers;

[ApiController]
[Route("api/manufacturers")]
public class ManufacturersController : ControllerBase
{
    private readonly AppDbContext _db;

    public ManufacturersController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Public. Used to populate the "Add Vehicle" manufacturer dropdown.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<ManufacturerResponse>>> GetAll()
    {
        var manufacturers = await _db.Manufacturers
            .OrderBy(m => m.Name)
            .Select(m => new ManufacturerResponse { Id = m.Id, Name = m.Name })
            .ToListAsync();

        return Ok(manufacturers);
    }

    /// <summary>
    /// Admin only — same reasoning as vehicle creation (VehiclesController):
    /// registering vehicles, and by extension adding a manufacturer from
    /// that same form, is a write to shared data. Manufacturers are still
    /// modeled as their own table (not embedded throughout the app) so
    /// this list can grow without a code change; it's just that only an
    /// admin can grow it now.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ManufacturerResponse>> Create([FromBody] ManufacturerRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var trimmedName = request.Name.Trim();

        var nameTaken = await _db.Manufacturers.AnyAsync(m => m.Name.ToLower() == trimmedName.ToLower());
        if (nameTaken)
        {
            throw new ValidationApiException($"A manufacturer named '{trimmedName}' already exists.");
        }

        var manufacturer = new Manufacturer { Name = trimmedName };
        _db.Manufacturers.Add(manufacturer);
        await _db.SaveChangesAsync();

        var response = new ManufacturerResponse { Id = manufacturer.Id, Name = manufacturer.Name };
        return CreatedAtAction(nameof(GetAll), response);
    }
}
