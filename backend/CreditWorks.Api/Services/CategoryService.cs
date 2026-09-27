using CreditWorks.Api.Data;
using CreditWorks.Api.Dtos;
using CreditWorks.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Services;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetAllAsync();
    Task<CategoryResponse> CreateAsync(CategoryRequest request);
    Task<CategoryResponse> UpdateAsync(int id, CategoryRequest request);
    Task DeleteAsync(int id);
}

/// <summary>
/// Note on atomicity: each method here does exactly one
/// <c>SaveChangesAsync()</c> call, even though it may touch several
/// categories (cascades, splits). A single SaveChangesAsync call is
/// already wrapped in an implicit transaction by EF Core on a relational
/// database, so an explicit <c>BeginTransaction</c>/<c>Commit</c> around it
/// would be redundant — and would also break the EF Core InMemory provider
/// used by the unit tests, which doesn't support transactions at all. If a
/// method here ever needs to span more than one SaveChangesAsync call, an
/// explicit transaction would become necessary again at that point.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly ICategoryRangeValidator _validator;

    public CategoryService(AppDbContext db, ICategoryRangeValidator validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<List<CategoryResponse>> GetAllAsync()
    {
        var categories = await _db.VehicleCategories.OrderBy(c => c.MinWeightKg).ToListAsync();
        return categories.Select(ToResponse).ToList();
    }

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request)
    {
        var nameTaken = await _db.VehicleCategories.AnyAsync(c => c.Name == request.Name.Trim());
        if (nameTaken)
        {
            throw new ValidationApiException($"A category named '{request.Name}' already exists.");
        }

        var newMin = request.MinWeightKg;
        var newMax = request.MaxWeightKg;

        // Tracked entities — mutating them here and SaveChangesAsync below persists the cascade too.
        var existing = await _db.VehicleCategories.ToListAsync();
        var newRemainders = new List<VehicleCategory>();

        // Cascade: if the new category's range eats into an existing
        // category, adjust that category to make room instead of rejecting
        // the request as an overlap. For each existing category `other`
        // that the new range overlaps at all, classify the overlap:
        //   - touches `other`'s bottom AND reaches at/past its top  -> new
        //     range fully engulfs `other`. Not auto-resolved (ambiguous —
        //     would mean deleting `other` outright); left for the final
        //     validation below to reject with a clear error.
        //   - touches `other`'s bottom only                        -> Rule 2:
        //     `other` shrinks from below (its Min moves up to newMax).
        //   - reaches `other`'s top only                           -> Rule 1:
        //     `other` shrinks from above (its Max moves down to newMin).
        //   - touches neither edge (new sits strictly inside `other`) ->
        //     three-way split: `other` becomes the *lower* remainder
        //     (keeps its name, ends at newMin), and a new category is
        //     created for the *upper* remainder (auto-named, e.g.
        //     "Medium (2)"), covering newMax through `other`'s old max.
        foreach (var other in existing)
        {
            var otherReachesPastNewMin = other.MaxWeightKg is null || other.MaxWeightKg.Value > newMin;
            var newReachesPastOtherMin = newMax is null || newMax.Value > other.MinWeightKg;
            var overlaps = otherReachesPastNewMin && newReachesPastOtherMin;
            if (!overlaps) continue;

            var touchesBottom = newMin <= other.MinWeightKg;
            var reachesTop = newMax is null || (other.MaxWeightKg is not null && newMax.Value >= other.MaxWeightKg.Value);

            if (touchesBottom && reachesTop)
            {
                continue; // full engulf — unsupported, let validation below reject it clearly
            }

            if (touchesBottom)
            {
                other.MinWeightKg = newMax!.Value; // Rule 2
            }
            else if (reachesTop)
            {
                other.MaxWeightKg = newMin; // Rule 1
            }
            else
            {
                // Three-way split.
                var originalMax = other.MaxWeightKg;
                other.MaxWeightKg = newMin;

                var takenNames = existing.Select(c => c.Name).Append(request.Name.Trim());
                var remainderName = GenerateRemainderName(other.Name, takenNames);

                newRemainders.Add(new VehicleCategory
                {
                    Name = remainderName,
                    IconKey = other.IconKey,
                    MinWeightKg = newMax!.Value,
                    MaxWeightKg = originalMax
                });
            }
        }

        var proposed = new VehicleCategory
        {
            Name = request.Name.Trim(),
            IconKey = request.IconKey.Trim(),
            MinWeightKg = newMin,
            MaxWeightKg = newMax
        };

        var validation = _validator.Validate(existing.Concat(newRemainders).Append(proposed));
        if (!validation.IsValid)
        {
            throw new ValidationApiException(validation.Errors);
        }

        _db.VehicleCategories.Add(proposed);
        _db.VehicleCategories.AddRange(newRemainders);
        await _db.SaveChangesAsync();

        return ToResponse(proposed);
    }

    public async Task<CategoryResponse> UpdateAsync(int id, CategoryRequest request)
    {
        var category = await _db.VehicleCategories.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundApiException(nameof(VehicleCategory), id);

        var nameTaken = await _db.VehicleCategories.AnyAsync(c => c.Name == request.Name.Trim() && c.Id != id);
        if (nameTaken)
        {
            throw new ValidationApiException($"A category named '{request.Name}' already exists.");
        }

        var oldMin = category.MinWeightKg;
        var oldMax = category.MaxWeightKg;
        var newMin = request.MinWeightKg;
        var newMax = request.MaxWeightKg;

        // These are tracked entities (no AsNoTracking), so mutating them here
        // and calling SaveChangesAsync below persists the cascade too.
        var others = await _db.VehicleCategories.Where(c => c.Id != id).ToListAsync();

        // Cascade: a category's min/max boundary is shared with whichever
        // neighbor currently touches it. Moving *only* this category's side
        // of that boundary — without telling its neighbor — is exactly what
        // used to produce "there's a gap"/"that's already covered" errors
        // even though the intent (move the shared line) was perfectly
        // valid. So: if the lower boundary moved, find the neighbor whose
        // Max used to equal it and move that neighbor's Max to match; same
        // for the upper boundary against a neighbor's Min. The full-set
        // validation below still runs afterwards and is still authoritative
        // — this cascade only removes the need to make the same edit twice.
        if (newMin != oldMin)
        {
            var lowerNeighbor = others.FirstOrDefault(c => c.MaxWeightKg == oldMin);
            if (lowerNeighbor is not null) lowerNeighbor.MaxWeightKg = newMin;
        }

        if (newMax != oldMax)
        {
            var upperNeighbor = others.FirstOrDefault(c => c.MinWeightKg == oldMax);
            if (upperNeighbor is not null) upperNeighbor.MinWeightKg = newMax.Value;
        }

        var proposedSelf = new VehicleCategory
        {
            Id = id,
            Name = request.Name.Trim(),
            IconKey = request.IconKey.Trim(),
            MinWeightKg = newMin,
            MaxWeightKg = newMax
        };

        var validation = _validator.Validate(others.Concat(new[] { proposedSelf }));
        if (!validation.IsValid)
        {
            throw new ValidationApiException(validation.Errors);
        }

        category.Name = proposedSelf.Name;
        category.IconKey = proposedSelf.IconKey;
        category.MinWeightKg = proposedSelf.MinWeightKg;
        category.MaxWeightKg = proposedSelf.MaxWeightKg;

        await _db.SaveChangesAsync();

        return ToResponse(category);
    }

    public async Task DeleteAsync(int id)
    {
        var category = await _db.VehicleCategories.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundApiException(nameof(VehicleCategory), id);

        var remaining = await _db.VehicleCategories.Where(c => c.Id != id).ToListAsync();

        // Cascade: absorb the deleted category's range into whichever
        // neighbor touches it, rather than just rejecting the deletion
        // outright because it would otherwise leave a gap. Prefer
        // extending the neighbor *below* (it grows upward to cover the
        // deleted range); if there is no lower neighbor — i.e. the very
        // bottom (0kg) category is being deleted — extend the neighbor
        // *above* back down to 0 instead.
        var lowerNeighbor = remaining.FirstOrDefault(c => c.MaxWeightKg == category.MinWeightKg);
        var upperNeighbor = remaining.FirstOrDefault(c => c.MinWeightKg == category.MaxWeightKg);

        if (lowerNeighbor is not null)
        {
            lowerNeighbor.MaxWeightKg = category.MaxWeightKg;
        }
        else if (upperNeighbor is not null)
        {
            upperNeighbor.MinWeightKg = category.MinWeightKg;
        }

        var validation = _validator.Validate(remaining);
        if (!validation.IsValid)
        {
            // Wrap with a deletion-specific message but keep the specific reason.
            throw new ConflictApiException(
                $"Cannot delete category '{category.Name}': {validation.Errors[0]}");
        }

        _db.VehicleCategories.Remove(category);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Produces a unique name for a split-off remainder category, deriving
    /// from the original category's name (e.g. "Medium" -&gt; "Medium (2)",
    /// or "Medium (3)" if "(2)" is somehow already taken).
    /// </summary>
    private static string GenerateRemainderName(string baseName, IEnumerable<string> takenNames)
    {
        var taken = new HashSet<string>(takenNames, StringComparer.OrdinalIgnoreCase);
        var counter = 2;
        string candidate;
        do
        {
            candidate = $"{baseName} ({counter})";
            counter++;
        } while (taken.Contains(candidate));

        return candidate;
    }

    private static CategoryResponse ToResponse(VehicleCategory c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        IconKey = c.IconKey,
        MinWeightKg = c.MinWeightKg,
        MaxWeightKg = c.MaxWeightKg
    };
}
