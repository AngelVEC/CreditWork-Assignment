using CreditWorks.Api.Models;

namespace CreditWorks.Api.Services;

public interface ICategoryResolver
{
    /// <summary>
    /// Returns the single category that <paramref name="weightKg"/> falls
    /// into, given the (assumed valid — i.e. gapless, non-overlapping,
    /// fully-covering) set of categories. Returns null only if the category
    /// configuration is currently invalid/empty, which CategoryRangeValidator
    /// is responsible for preventing.
    /// </summary>
    VehicleCategory? Resolve(decimal weightKg, IEnumerable<VehicleCategory> categories);
}

/// <summary>
/// Range convention: [Min, Max) — inclusive lower bound, exclusive upper
/// bound, with the top category's Max == null meaning "and above". This is
/// what makes a boundary weight (e.g. exactly 500.00kg) resolve to exactly
/// one category: it belongs to the category whose Min equals that value,
/// not the one whose Max equals it.
/// </summary>
public class CategoryResolver : ICategoryResolver
{
    public VehicleCategory? Resolve(decimal weightKg, IEnumerable<VehicleCategory> categories)
    {
        return categories.FirstOrDefault(c =>
            weightKg >= c.MinWeightKg &&
            (c.MaxWeightKg == null || weightKg < c.MaxWeightKg.Value));
    }
}
