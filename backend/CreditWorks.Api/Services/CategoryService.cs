using CreditWorks.Api.Data;
using CreditWorks.Api.Dtos;
using CreditWorks.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CreditWorks.Api.Services;

public interface ICategoryService
{
    Task<List<CategoryResponse>> GetAllAsync();
    Task<CategoryResponse> CreateAsync(CategoryRequest request);
    Task<CategoryResponse> UpdateAsync(int id, CategoryRequest request);
    Task DeleteAsync(int id);
}

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
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var nameTaken = await _db.VehicleCategories.AnyAsync(c => c.Name == request.Name.Trim());
        if (nameTaken)
        {
            throw new ValidationApiException($"A category named '{request.Name}' already exists.");
        }

        var proposed = new VehicleCategory
        {
            Name = request.Name.Trim(),
            IconKey = request.IconKey.Trim(),
            MinWeightKg = request.MinWeightKg,
            MaxWeightKg = request.MaxWeightKg
        };

        var existing = await _db.VehicleCategories.ToListAsync();
        var proposedSet = existing.Concat(new[] { proposed });

        var validation = _validator.Validate(proposedSet);
        if (!validation.IsValid)
        {
            throw new ValidationApiException(validation.Errors);
        }

        _db.VehicleCategories.Add(proposed);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return ToResponse(proposed);
    }

    public async Task<CategoryResponse> UpdateAsync(int id, CategoryRequest request)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

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
        await transaction.CommitAsync();

        return ToResponse(category);
    }

    public async Task DeleteAsync(int id)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var category = await _db.VehicleCategories.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundApiException(nameof(VehicleCategory), id);

        var remaining = await _db.VehicleCategories.Where(c => c.Id != id).ToListAsync();

        var validation = _validator.Validate(remaining);
        if (!validation.IsValid)
        {
            // Wrap with a deletion-specific message but keep the specific reason.
            throw new ConflictApiException(
                $"Cannot delete category '{category.Name}': {validation.Errors[0]}");
        }

        _db.VehicleCategories.Remove(category);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
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
