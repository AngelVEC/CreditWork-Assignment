using CreditWorks.Api.Data;
using CreditWorks.Api.Dtos;
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
    public async Task<ActionResult<List<ManufacturerResponse>>> GetAll()
    {
        var manufacturers = await _db.Manufacturers
            .OrderBy(m => m.Name)
            .Select(m => new ManufacturerResponse { Id = m.Id, Name = m.Name })
            .ToListAsync();

        return Ok(manufacturers);
    }
}
