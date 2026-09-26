using CreditWorks.Api.Models;

namespace CreditWorks.Api.Services;

public class CategoryValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();

    public static CategoryValidationResult Ok() => new();

    public static CategoryValidationResult Fail(string error)
    {
        var r = new CategoryValidationResult();
        r.Errors.Add(error);
        return r;
    }
}

public interface ICategoryRangeValidator
{
    /// <summary>
    /// Validates a *proposed* full set of categories (i.e. the caller is
    /// responsible for building the "what would the table look like after
    /// this change" list — including the edited/new/removed entries — and
    /// passing that whole set in). Checks, in order:
    ///   1. The set isn't empty (deleting the last category is always invalid).
    ///   2. Every category has Min &lt; Max (or Max == null).
    ///   3. Sorted by Min, the lowest category's Min == 0.
    ///   4. Sorted by Min, the highest category's Max == null.
    ///   5. For every consecutive pair, current.Max == next.Min (this single
    ///      check simultaneously rules out both gaps and overlaps).
    /// </summary>
    CategoryValidationResult Validate(IEnumerable<VehicleCategory> proposedCategories);
}

public class CategoryRangeValidator : ICategoryRangeValidator
{
    public CategoryValidationResult Validate(IEnumerable<VehicleCategory> proposedCategories)
    {
        var categories = proposedCategories.OrderBy(c => c.MinWeightKg).ToList();

        if (categories.Count == 0)
        {
            return CategoryValidationResult.Fail(
                "At least one category must exist — deleting the last remaining category would leave every vehicle weight without a category.");
        }

        foreach (var c in categories)
        {
            if (c.MinWeightKg < 0)
            {
                return CategoryValidationResult.Fail(
                    $"Category '{c.Name}' has a negative minimum weight ({c.MinWeightKg}kg); weights are never negative.");
            }

            if (c.MaxWeightKg.HasValue && c.MaxWeightKg.Value <= c.MinWeightKg)
            {
                return CategoryValidationResult.Fail(
                    $"Category '{c.Name}' has a maximum weight ({c.MaxWeightKg}kg) that is not greater than its minimum weight ({c.MinWeightKg}kg).");
            }
        }

        var lowest = categories[0];
        if (lowest.MinWeightKg != 0)
        {
            return CategoryValidationResult.Fail(
                $"The lowest category ('{lowest.Name}') must start at 0kg so every valid vehicle weight has a category, but it starts at {lowest.MinWeightKg}kg.");
        }

        var highest = categories[^1];
        if (highest.MaxWeightKg != null)
        {
            return CategoryValidationResult.Fail(
                $"The highest category ('{highest.Name}') must be unbounded (no maximum weight) so every valid vehicle weight has a category, but it caps at {highest.MaxWeightKg}kg.");
        }

        for (var i = 0; i < categories.Count - 1; i++)
        {
            var current = categories[i];
            var next = categories[i + 1];

            if (current.MaxWeightKg != next.MinWeightKg)
            {
                if (current.MaxWeightKg < next.MinWeightKg)
                {
                    return CategoryValidationResult.Fail(
                        $"This configuration would leave a gap between {current.MaxWeightKg}kg ('{current.Name}') and {next.MinWeightKg}kg ('{next.Name}') — no category covers that range.");
                }

                return CategoryValidationResult.Fail(
                    $"This configuration would overlap: '{current.Name}' extends to {current.MaxWeightKg}kg but '{next.Name}' already starts at {next.MinWeightKg}kg.");
            }
        }

        return CategoryValidationResult.Ok();
    }
}
