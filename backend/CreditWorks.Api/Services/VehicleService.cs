using CreditWorks.Api.Data;
using CreditWorks.Api.Dtos;
using CreditWorks.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Services;

public enum VehicleSortBy { OwnerName, Manufacturer, Year, Weight }
public enum SortDirection { Asc, Desc }

public interface IVehicleService
{
    Task<List<VehicleResponse>> GetAllAsync(VehicleSortBy sortBy, SortDirection sortDir);
    Task<VehicleResponse> GetByIdAsync(int id);
    Task<VehicleResponse> CreateAsync(VehicleRequest request);
    Task<VehicleResponse> UpdateAsync(int id, VehicleRequest request);
    Task DeleteAsync(int id);
}

public class VehicleService : IVehicleService
{
    // Assumption (documented, §2.1 of the build brief): the first
    // automobile was built in 1886, so that's the earliest sensible year of
    // manufacture. Upper bound allows next-model-year vehicles.
    private const int EarliestValidYear = 1886;

    private readonly AppDbContext _db;
    private readonly ICategoryResolver _categoryResolver;

    public VehicleService(AppDbContext db, ICategoryResolver categoryResolver)
    {
        _db = db;
        _categoryResolver = categoryResolver;
    }

    public async Task<List<VehicleResponse>> GetAllAsync(VehicleSortBy sortBy, SortDirection sortDir)
    {
        var vehicles = await _db.Vehicles.Include(v => v.Manufacturer).ToListAsync();
        var categories = await _db.VehicleCategories.ToListAsync();

        var results = vehicles.Select(v => ToResponse(v, categories)).ToList();

        Func<VehicleResponse, object> keySelector = sortBy switch
        {
            VehicleSortBy.OwnerName => v => v.OwnerName,
            VehicleSortBy.Manufacturer => v => v.ManufacturerName,
            VehicleSortBy.Year => v => v.YearOfManufacture,
            VehicleSortBy.Weight => v => v.WeightKg,
            _ => v => v.OwnerName
        };

        results = sortDir == SortDirection.Desc
            ? results.OrderByDescending(keySelector).ToList()
            : results.OrderBy(keySelector).ToList();

        return results;
    }

    public async Task<VehicleResponse> GetByIdAsync(int id)
    {
        var vehicle = await _db.Vehicles.Include(v => v.Manufacturer).FirstOrDefaultAsync(v => v.Id == id)
            ?? throw new NotFoundApiException(nameof(Vehicle), id);

        var categories = await _db.VehicleCategories.ToListAsync();
        return ToResponse(vehicle, categories);
    }

    public async Task<VehicleResponse> CreateAsync(VehicleRequest request)
    {
        await ValidateAsync(request);

        var vehicle = new Vehicle
        {
            OwnerName = request.OwnerName.Trim(),
            ManufacturerId = request.ManufacturerId,
            YearOfManufacture = request.YearOfManufacture,
            WeightKg = request.WeightKg
        };

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();

        return await GetByIdAsync(vehicle.Id);
    }

    public async Task<VehicleResponse> UpdateAsync(int id, VehicleRequest request)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id)
            ?? throw new NotFoundApiException(nameof(Vehicle), id);

        await ValidateAsync(request);

        vehicle.OwnerName = request.OwnerName.Trim();
        vehicle.ManufacturerId = request.ManufacturerId;
        vehicle.YearOfManufacture = request.YearOfManufacture;
        vehicle.WeightKg = request.WeightKg;

        await _db.SaveChangesAsync();

        return await GetByIdAsync(vehicle.Id);
    }

    public async Task DeleteAsync(int id)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id)
            ?? throw new NotFoundApiException(nameof(Vehicle), id);

        _db.Vehicles.Remove(vehicle);
        await _db.SaveChangesAsync();
    }

    private async Task ValidateAsync(VehicleRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.OwnerName))
        {
            errors.Add("Owner's name is required.");
        }

        var manufacturerExists = await _db.Manufacturers.AnyAsync(m => m.Id == request.ManufacturerId);
        if (!manufacturerExists)
        {
            errors.Add($"Manufacturer with id '{request.ManufacturerId}' does not exist.");
        }

        var maxValidYear = DateTime.UtcNow.Year + 1;
        if (request.YearOfManufacture < EarliestValidYear || request.YearOfManufacture > maxValidYear)
        {
            errors.Add($"Year of manufacture must be between {EarliestValidYear} and {maxValidYear}.");
        }

        if (request.WeightKg <= 0)
        {
            errors.Add("Weight must be a positive number.");
        }
        else if (decimal.Round(request.WeightKg, 2) != request.WeightKg)
        {
            errors.Add("Weight must have at most two decimal places.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationApiException(errors);
        }
    }

    private VehicleResponse ToResponse(Vehicle vehicle, List<VehicleCategory> categories)
    {
        var category = _categoryResolver.Resolve(vehicle.WeightKg, categories);

        return new VehicleResponse
        {
            Id = vehicle.Id,
            OwnerName = vehicle.OwnerName,
            ManufacturerId = vehicle.ManufacturerId,
            ManufacturerName = vehicle.Manufacturer?.Name ?? string.Empty,
            YearOfManufacture = vehicle.YearOfManufacture,
            WeightKg = vehicle.WeightKg,
            // If category is null the configuration is currently invalid —
            // CategoryRangeValidator should prevent this from ever
            // happening, but we surface it clearly rather than crash or
            // silently mislabel the vehicle.
            CategoryName = category?.Name ?? "Uncategorised",
            CategoryIconKey = category?.IconKey ?? "AlertTriangle"
        };
    }
}
