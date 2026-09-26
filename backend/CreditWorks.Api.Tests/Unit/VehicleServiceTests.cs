using CreditWorks.Api.Data;
using CreditWorks.Api.Dtos;
using CreditWorks.Api.Models;
using CreditWorks.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CreditWorks.Api.Tests.Unit;

public class VehicleServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly VehicleService _service;

    public VehicleServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        _db.Manufacturers.Add(new Manufacturer { Id = 1, Name = "Mazda" });
        _db.VehicleCategories.AddRange(
            new VehicleCategory { Id = 1, Name = "Light", IconKey = "Feather", MinWeightKg = 0, MaxWeightKg = 500 },
            new VehicleCategory { Id = 2, Name = "Medium", IconKey = "Truck", MinWeightKg = 500, MaxWeightKg = 2500 },
            new VehicleCategory { Id = 3, Name = "Heavy", IconKey = "Container", MinWeightKg = 2500, MaxWeightKg = null }
        );
        _db.SaveChanges();

        _service = new VehicleService(_db, new CategoryResolver());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateAsync_ValidVehicle_IsPersistedWithCorrectCategory()
    {
        var request = new VehicleRequest
        {
            OwnerName = "John Smith",
            ManufacturerId = 1,
            YearOfManufacture = 2019,
            WeightKg = 480.50m
        };

        var result = await _service.CreateAsync(request);

        result.CategoryName.Should().Be("Light");
        result.ManufacturerName.Should().Be("Mazda");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateAsync_NonPositiveWeight_ThrowsValidationException(decimal weight)
    {
        var request = ValidRequest() with { };
        request.WeightKg = weight;

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationApiException>();
    }

    [Fact]
    public async Task CreateAsync_WeightWithMoreThanTwoDecimalPlaces_ThrowsValidationException()
    {
        var request = ValidRequest();
        request.WeightKg = 1850.755m;

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationApiException>();
    }

    [Fact]
    public async Task CreateAsync_MissingOwnerName_ThrowsValidationException()
    {
        var request = ValidRequest();
        request.OwnerName = "   ";

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationApiException>();
    }

    [Fact]
    public async Task CreateAsync_UnknownManufacturer_ThrowsValidationException()
    {
        var request = ValidRequest();
        request.ManufacturerId = 999;

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationApiException>();
    }

    [Theory]
    [InlineData(1885)]
    [InlineData(3000)]
    public async Task CreateAsync_YearOutOfRange_ThrowsValidationException(int year)
    {
        var request = ValidRequest();
        request.YearOfManufacture = year;

        var act = () => _service.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationApiException>();
    }

    [Fact]
    public async Task GetAllAsync_SortByWeightDescending_ReturnsCorrectOrder()
    {
        await _service.CreateAsync(ValidRequest() with { OwnerName = "A", WeightKg = 100m });
        await _service.CreateAsync(ValidRequest() with { OwnerName = "B", WeightKg = 3000m });
        await _service.CreateAsync(ValidRequest() with { OwnerName = "C", WeightKg = 1000m });

        var result = await _service.GetAllAsync(VehicleSortBy.Weight, SortDirection.Desc);

        result.Select(v => v.OwnerName).Should().ContainInOrder("B", "C", "A");
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ThrowsNotFoundException()
    {
        var act = () => _service.GetByIdAsync(999);
        await act.Should().ThrowAsync<NotFoundApiException>();
    }

    [Fact]
    public async Task CategoryChange_ReflectsImmediatelyOnExistingVehicle_WithoutDataMigration()
    {
        var created = await _service.CreateAsync(ValidRequest() with { WeightKg = 2200m });
        created.CategoryName.Should().Be("Medium");

        // Simulate an admin changing the Medium/Heavy boundary from 2500 to 2000.
        var medium = await _db.VehicleCategories.FirstAsync(c => c.Name == "Medium");
        var heavy = await _db.VehicleCategories.FirstAsync(c => c.Name == "Heavy");
        medium.MaxWeightKg = 2000m;
        heavy.MinWeightKg = 2000m;
        await _db.SaveChangesAsync();

        var refetched = await _service.GetByIdAsync(created.Id);
        refetched.CategoryName.Should().Be("Heavy");
    }

    private static VehicleRequest ValidRequest() => new()
    {
        OwnerName = "Jane Turei",
        ManufacturerId = 1,
        YearOfManufacture = 2015,
        WeightKg = 1850.75m
    };
}
