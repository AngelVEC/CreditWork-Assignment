namespace CreditWorks.Api.Models;

/// <summary>
/// A vehicle. Deliberately has NO stored category and NO FK to
/// VehicleCategory: category is always computed on read from WeightKg
/// against the *current* set of VehicleCategory ranges (see
/// Services/CategoryResolver.cs). This is what guarantees a vehicle's
/// displayed category can never go stale relative to the current category
/// configuration (assignment section 6).
/// </summary>
public class Vehicle
{
    public int Id { get; set; }

    public string OwnerName { get; set; } = string.Empty;

    public int ManufacturerId { get; set; }

    public Manufacturer? Manufacturer { get; set; }

    public int YearOfManufacture { get; set; }

    public decimal WeightKg { get; set; }
}
