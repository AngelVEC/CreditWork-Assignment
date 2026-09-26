using System.ComponentModel.DataAnnotations;

namespace CreditWorks.Api.Dtos;

/// <summary>Shape returned to clients — includes the computed category.</summary>
public class VehicleResponse
{
    public int Id { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int ManufacturerId { get; set; }
    public string ManufacturerName { get; set; } = string.Empty;
    public int YearOfManufacture { get; set; }
    public decimal WeightKg { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryIconKey { get; set; } = string.Empty;
}

/// <summary>
/// Body for creating/editing a vehicle. Validation attributes give a first
/// line of defense; CategoryRangeValidator-style server rules (year bounds,
/// weight precision) are re-checked in the service layer too, since the
/// assignment explicitly requires validation not to rely on the client/
/// framework attributes alone.
/// </summary>
public record VehicleRequest
{
    [Required(ErrorMessage = "Owner's name is required.")]
    [StringLength(200, MinimumLength = 1)]
    public string OwnerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Manufacturer is required.")]
    public int ManufacturerId { get; set; }

    [Required(ErrorMessage = "Year of manufacture is required.")]
    public int YearOfManufacture { get; set; }

    [Required(ErrorMessage = "Weight is required.")]
    public decimal WeightKg { get; set; }
}
