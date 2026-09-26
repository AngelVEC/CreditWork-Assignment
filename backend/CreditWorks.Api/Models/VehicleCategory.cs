namespace CreditWorks.Api.Models;

/// <summary>
/// A configurable weight category (e.g. Light / Medium / Heavy).
///
/// Range convention: [MinWeightKg, MaxWeightKg) — inclusive lower bound,
/// exclusive upper bound. MaxWeightKg == null means "unbounded" and must
/// only ever be true for exactly one category (the highest one). This is
/// what guarantees a vehicle weighing exactly on a boundary (e.g. 500.00kg)
/// belongs to one, and only one, category — see CategoryRangeValidator and
/// CategoryResolver for where this is enforced/used.
/// </summary>
public class VehicleCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Key into the frontend's fixed lucide-react icon set (e.g. "Feather",
    /// "Truck", "Container"). No image upload/storage — just a lookup key.
    /// </summary>
    public string IconKey { get; set; } = string.Empty;

    public decimal MinWeightKg { get; set; }

    public decimal? MaxWeightKg { get; set; }
}
