using CreditWorks.Api.Models;
using CreditWorks.Api.Services;
using FluentAssertions;
using Xunit;

namespace CreditWorks.Api.Tests.Unit;

public class CategoryResolverTests
{
    private static List<VehicleCategory> StandardCategories() => new()
    {
        new VehicleCategory { Id = 1, Name = "Light", MinWeightKg = 0m, MaxWeightKg = 500m },
        new VehicleCategory { Id = 2, Name = "Medium", MinWeightKg = 500m, MaxWeightKg = 2500m },
        new VehicleCategory { Id = 3, Name = "Heavy", MinWeightKg = 2500m, MaxWeightKg = null },
    };

    private readonly CategoryResolver _resolver = new();

    [Theory]
    [InlineData(0.01, "Light")]
    [InlineData(499.99, "Light")]
    [InlineData(1850.75, "Medium")]
    [InlineData(999999.99, "Heavy")]
    public void Resolve_ReturnsExpectedCategory_ForOrdinaryWeights(decimal weight, string expected)
    {
        var result = _resolver.Resolve(weight, StandardCategories());
        result.Should().NotBeNull();
        result!.Name.Should().Be(expected);
    }

    [Fact]
    public void Resolve_WeightExactlyOnLowerBoundary_BelongsToUpperCategory()
    {
        // [Min, Max) convention: 500.00kg belongs to Medium (Min=500), not Light (Max=500).
        var result = _resolver.Resolve(500.00m, StandardCategories());
        result!.Name.Should().Be("Medium");
    }

    [Fact]
    public void Resolve_WeightExactlyOnSecondBoundary_BelongsToUpperCategory()
    {
        var result = _resolver.Resolve(2500.00m, StandardCategories());
        result!.Name.Should().Be("Heavy");
    }

    [Fact]
    public void Resolve_EveryWeight_ResolvesToExactlyOneCategory()
    {
        var categories = StandardCategories();
        var weightsToCheck = new[] { 0.01m, 100m, 499.99m, 500.00m, 500.01m, 2499.99m, 2500.00m, 2500.01m, 50000m };

        foreach (var weight in weightsToCheck)
        {
            var matches = categories.Count(c =>
                weight >= c.MinWeightKg && (c.MaxWeightKg == null || weight < c.MaxWeightKg.Value));

            matches.Should().Be(1, because: $"weight {weight}kg should resolve to exactly one category");
        }
    }

    [Fact]
    public void Resolve_ChangedCategoryRanges_ReclassifiesExistingVehicleWeight()
    {
        // Assignment §6 example: a 2200kg vehicle is Medium under the
        // original ranges, then Heavy once Medium/Heavy's boundary moves to 2000kg.
        var originalCategories = StandardCategories();
        _resolver.Resolve(2200m, originalCategories)!.Name.Should().Be("Medium");

        var revisedCategories = new List<VehicleCategory>
        {
            new() { Id = 1, Name = "Light", MinWeightKg = 0m, MaxWeightKg = 500m },
            new() { Id = 2, Name = "Medium", MinWeightKg = 500m, MaxWeightKg = 2000m },
            new() { Id = 3, Name = "Heavy", MinWeightKg = 2000m, MaxWeightKg = null },
        };

        _resolver.Resolve(2200m, revisedCategories)!.Name.Should().Be("Heavy");
    }

    [Fact]
    public void Resolve_EmptyCategorySet_ReturnsNull()
    {
        var result = _resolver.Resolve(1000m, new List<VehicleCategory>());
        result.Should().BeNull();
    }
}
