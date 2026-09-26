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

    [Fact]
    public async Task DeleteAsync_MiddleCategory_CascadesIntoLowerNeighbor()
    {
        // Deleting Medium (500-2500) should extend Light (0-500) up to
        // 2500, absorbing Medium's range, rather than being rejected for
        // leaving a gap.
        var medium = await _db.VehicleCategories.FirstAsync(c => c.Name == "Medium");

        await _service.DeleteAsync(medium.Id);

        var remaining = await _db.VehicleCategories.ToListAsync();
        remaining.Should().HaveCount(2);

        var light = remaining.Single(c => c.Name == "Light");
        light.MaxWeightKg.Should().Be(2500m);

        var heavy = remaining.Single(c => c.Name == "Heavy");
        heavy.MinWeightKg.Should().Be(2500m);

        new CategoryRangeValidator().Validate(remaining).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_BottomCategory_CascadesIntoUpperNeighbor_DownToZero()
    {
        // Deleting Light (0-500), which has no lower neighbor, should push
        // Medium's minimum down to 0 instead.
        var light = await _db.VehicleCategories.FirstAsync(c => c.Name == "Light");

        await _service.DeleteAsync(light.Id);

        var medium = await _db.VehicleCategories.FirstAsync(c => c.Name == "Medium");
        medium.MinWeightKg.Should().Be(0m);

        var remaining = await _db.VehicleCategories.ToListAsync();
        new CategoryRangeValidator().Validate(remaining).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_NewBottomCategory_CascadesIntoLightsLowerBound()
    {
        // Adding a new 0-200 category should shrink Light (0-500) down to
        // start at 200, rather than being rejected as overlapping.
        var result = await _service.CreateAsync(new CategoryRequest
        {
            Name = "Ultra-Light",
            IconKey = "Feather",
            MinWeightKg = 0m,
            MaxWeightKg = 200m
        });

        result.MaxWeightKg.Should().Be(200m);

        var light = await _db.VehicleCategories.FirstAsync(c => c.Name == "Light");
        light.MinWeightKg.Should().Be(200m);

        var all = await _db.VehicleCategories.ToListAsync();
        new CategoryRangeValidator().Validate(all).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_NewUnboundedTopCategory_CascadesIntoHeavysUpperBound()
    {
        // Adding a new unbounded 5000+ category should bound Heavy at
        // 5000 instead of leaving it unbounded (two unbounded categories
        // would otherwise be invalid).
        var result = await _service.CreateAsync(new CategoryRequest
        {
            Name = "Super-Heavy",
            IconKey = "Container",
            MinWeightKg = 5000m,
            MaxWeightKg = null
        });

        result.MaxWeightKg.Should().BeNull();

        var heavy = await _db.VehicleCategories.FirstAsync(c => c.Name == "Heavy");
        heavy.MaxWeightKg.Should().Be(5000m);

        var all = await _db.VehicleCategories.ToListAsync();
        new CategoryRangeValidator().Validate(all).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_SpanningTwoExistingCategories_CascadesBothNeighbors()
    {
        // A new 300-1000 category spans across the Light/Medium boundary:
        // it should shrink Light's top (500->300) and Medium's bottom
        // (500->1000) simultaneously.
        var result = await _service.CreateAsync(new CategoryRequest
        {
            Name = "Mid",
            IconKey = "Bike",
            MinWeightKg = 300m,
            MaxWeightKg = 1000m
        });

        result.MinWeightKg.Should().Be(300m);
        result.MaxWeightKg.Should().Be(1000m);

        var light = await _db.VehicleCategories.FirstAsync(c => c.Name == "Light");
        light.MaxWeightKg.Should().Be(300m);

        var medium = await _db.VehicleCategories.FirstAsync(c => c.Name == "Medium");
        medium.MinWeightKg.Should().Be(1000m);

        var all = await _db.VehicleCategories.ToListAsync();
        new CategoryRangeValidator().Validate(all).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_RangeStrictlyInsideAnExistingCategory_SplitsItThreeWays()
    {
        // 800-1200 sits entirely inside Medium (500-2500) without touching
        // either of its edges. Medium keeps its name for the lower
        // remainder (500-800); a new auto-named category covers the upper
        // remainder (1200-2500).
        var result = await _service.CreateAsync(new CategoryRequest
        {
            Name = "Inner",
            IconKey = "Bike",
            MinWeightKg = 800m,
            MaxWeightKg = 1200m
        });

        result.MinWeightKg.Should().Be(800m);
        result.MaxWeightKg.Should().Be(1200m);

        var all = await _db.VehicleCategories.ToListAsync();
        all.Should().HaveCount(5); // Light, Medium, Inner, Medium (2), Heavy

        var medium = all.Single(c => c.Name == "Medium");
        medium.MinWeightKg.Should().Be(500m);
        medium.MaxWeightKg.Should().Be(800m);

        var remainder = all.Single(c => c.Name == "Medium (2)");
        remainder.MinWeightKg.Should().Be(1200m);
        remainder.MaxWeightKg.Should().Be(2500m);
        remainder.IconKey.Should().Be(medium.IconKey);

        new CategoryRangeValidator().Validate(all).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_SplitInsideUnboundedTopCategory_UpperRemainderStaysUnbounded()
    {
        // 3000-4000 sits inside Heavy (2500-null) without touching either
        // "edge" (Heavy has no upper edge to touch). Heavy's lower
        // remainder becomes 2500-3000; the new upper remainder stays
        // unbounded (4000-null).
        await _service.CreateAsync(new CategoryRequest
        {
            Name = "Special",
            IconKey = "Truck",
            MinWeightKg = 3000m,
            MaxWeightKg = 4000m
        });

        var all = await _db.VehicleCategories.ToListAsync();

        var heavy = all.Single(c => c.Name == "Heavy");
        heavy.MinWeightKg.Should().Be(2500m);
        heavy.MaxWeightKg.Should().Be(3000m);

        var remainder = all.Single(c => c.Name == "Heavy (2)");
        remainder.MinWeightKg.Should().Be(4000m);
        remainder.MaxWeightKg.Should().BeNull();

        new CategoryRangeValidator().Validate(all).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_FullyEnglufingAnExistingCategory_IsStillRejected()
    {
        // A new range that exactly (or more than) covers Medium's entire
        // span isn't a "split" — it would mean deleting Medium outright,
        // which isn't auto-resolved.
        var act = () => _service.CreateAsync(new CategoryRequest
        {
            Name = "Engulfing",
            IconKey = "Bike",
            MinWeightKg = 500m,
            MaxWeightKg = 2500m
        });

        await act.Should().ThrowAsync<ValidationApiException>();
    }
}
