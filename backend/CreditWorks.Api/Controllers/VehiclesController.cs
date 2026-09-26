using CreditWorks.Api.Dtos;
using CreditWorks.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditWorks.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    /// <summary>
    /// Public — no login required. Each vehicle is returned with its
    /// computed category already attached, so the frontend's main list page
    /// never needs to call /categories directly.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<VehicleResponse>>> GetAll(
        [FromQuery] string sortBy = "ownerName",
        [FromQuery] string sortDir = "asc")
    {
        var parsedSortBy = sortBy.ToLowerInvariant() switch
        {
            "manufacturer" => VehicleSortBy.Manufacturer,
            "year" => VehicleSortBy.Year,
            "weight" => VehicleSortBy.Weight,
            _ => VehicleSortBy.OwnerName
        };

        var parsedSortDir = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Desc
            : SortDirection.Asc;

        return Ok(await _vehicleService.GetAllAsync(parsedSortBy, parsedSortDir));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<VehicleResponse>> GetById(int id)
    {
        return Ok(await _vehicleService.GetByIdAsync(id));
    }

    /// <summary>
    /// Admin only. Registering a vehicle is a write to shared data (and,
    /// via the inline "add manufacturer" flow, potentially to the
    /// manufacturer list too), so it's gated the same way category
    /// administration is — see AuthController/Program.cs for how the
    /// httpOnly-cookie JWT is validated.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VehicleResponse>> Create([FromBody] VehicleRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var created = await _vehicleService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<VehicleResponse>> Update(int id, [FromBody] VehicleRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        return Ok(await _vehicleService.UpdateAsync(id, request));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _vehicleService.DeleteAsync(id);
        return NoContent();
    }
}
