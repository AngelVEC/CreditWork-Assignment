using CreditWorks.Api.Data;
using CreditWorks.Api.Dtos;
using CreditWorks.Api.Models;
using CreditWorks.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CreditWorks.Api.Tests.Unit;

public class CategoryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);

        _db.VehicleCategories.AddRange(
            new VehicleCategory { Name = "Light", IconKey = "Feather", MinWeightKg = 0, MaxWeightKg = 500 },
            new VehicleCategory { Name = "Medium", IconKey = "Truck", MinWeightKg = 500, MaxWeightKg = 2500 },
            new VehicleCategory { Name = "Heavy", IconKey = "Container", MinWeightKg = 2500, MaxWeightKg = null }
        );
        _db.SaveChanges();

        _service = new CategoryService(_db, new CategoryRangeValidator());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task UpdateAsync_MovingLowerBoundaryOfHeavy_CascadesMediumsUpperBoundaryToMatch()
    {
        // Reproduces the reported bug: moving Heavy from 2500->3500 used to
        // fail with "gap between 2500kg and 3500kg" because Medium's max
        // (2500) never moved with it.
        var heavy = await _db.VehicleCategories.FirstAsync(c => c.Name == "Heavy");

        var result = await _service.UpdateAsync(heavy.Id, new CategoryRequest
        {
            Name = "Heavy",
            IconKey = "Container",
            MinWeightKg = 3500m,
            MaxWeightKg = null
        });

        result.MinWeightKg.Should().Be(3500m);

        var medium = await _db.VehicleCategories.FirstAsync(c => c.Name == "Medium");
        medium.MaxWeightKg.Should().Be(3500m);

        // Full config should still be internally valid afterwards.
        var all = await _db.VehicleCategories.ToListAsync();
        new CategoryRangeValidator().Validate(all).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_MovingUpperBoundaryOfMedium_CascadesHeavysLowerBoundaryToMatch()
    {
        // Reproduces the second reported bug: moving Medium's max from
        // 2500->2600 used to fail with "2600kg already covered by Heavy"
        // because Heavy's min (2500) never moved with it.
        var medium = await _db.VehicleCategories.FirstAsync(c => c.Name == "Medium");

        var result = await _service.UpdateAsync(medium.Id, new CategoryRequest
        {
            Name = "Medium",
            IconKey = "Truck",
            MinWeightKg = 500m,
            MaxWeightKg = 2600m
        });

        result.MaxWeightKg.Should().Be(2600m);

        var heavy = await _db.VehicleCategories.FirstAsync(c => c.Name == "Heavy");
        heavy.MinWeightKg.Should().Be(2600m);

        var all = await _db.VehicleCategories.ToListAsync();
        new CategoryRangeValidator().Validate(all).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_MovingBoundaryPastANonAdjacentCategory_IsStillRejected()
    {
        // Cascade only adjusts the immediately-touching neighbor. Trying to
        // move Medium's min down past 0 (Light's own min) isn't something a
        // single-neighbor cascade can fix, and must still be rejected.
        var medium = await _db.VehicleCategories.FirstAsync(c => c.Name == "Medium");

        var act = () => _service.UpdateAsync(medium.Id, new CategoryRequest
        {
            Name = "Medium",
            IconKey = "Truck",
            MinWeightKg = -100m,
            MaxWeightKg = 2500m
        });

        await act.Should().ThrowAsync<ValidationApiException>();
    }

    [Fact]
    public async Task DeleteAsync_LastRemainingCategory_ThrowsConflict()
    {
        var all = await _db.VehicleCategories.ToListAsync();
        foreach (var c in all.Skip(1))
        {
            await _service.DeleteAsync(c.Id);
        }

        var last = (await _db.VehicleCategories.ToListAsync()).Single();

        var act = () => _service.DeleteAsync(last.Id);

        await act.Should().ThrowAsync<ConflictApiException>();
    }
}
