using CreditWorks.Api.Models;
using CreditWorks.Api.Services;
using FluentAssertions;
using Xunit;

namespace CreditWorks.Api.Tests.Unit;

public class CategoryRangeValidatorTests
{
    private readonly CategoryRangeValidator _validator = new();

    private static VehicleCategory Cat(string name, decimal min, decimal? max) =>
        new() { Name = name, MinWeightKg = min, MaxWeightKg = max };

    [Fact]
    public void Validate_StandardThreeCategoryConfig_IsValid()
    {
        var categories = new[]
        {
            Cat("Light", 0, 500),
            Cat("Medium", 500, 2500),
            Cat("Heavy", 2500, null),
        };

        _validator.Validate(categories).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_GapBetweenRanges_IsRejected()
    {
        // Matches the assignment's own invalid example: Light 0-500, Medium 600-2500.
        // Heavy is included, unbounded, purely so the "highest category
        // must be unbounded" rule doesn't fire first and mask the gap
        // check this test is actually exercising.
        var categories = new[]
        {
            Cat("Light", 0, 500),
            Cat("Medium", 600, 2500),
            Cat("Heavy", 2500, null),
        };

        var result = _validator.Validate(categories);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Should().Contain("gap");
    }

    [Fact]
    public void Validate_OverlappingRanges_IsRejected()
    {
        // Matches the assignment's own invalid example: Light 0-600, Medium 500-2500.
        // Heavy is included, unbounded, for the same reason as above.
        var categories = new[]
        {
            Cat("Light", 0, 600),
            Cat("Medium", 500, 2500),
            Cat("Heavy", 2500, null),
        };

        var result = _validator.Validate(categories);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Should().Contain("overlap");
    }

    [Fact]
    public void Validate_LowestCategoryDoesNotStartAtZero_IsRejected()
    {
        var categories = new[]
        {
            Cat("Light", 10, 500),
            Cat("Heavy", 500, null),
        };

        _validator.Validate(categories).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_HighestCategoryIsBounded_IsRejected()
    {
        var categories = new[]
        {
            Cat("Light", 0, 500),
            Cat("Heavy", 500, 10000), // should be null/unbounded
        };

        _validator.Validate(categories).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptySet_IsRejected()
    {
        _validator.Validate(Array.Empty<VehicleCategory>()).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_SingleUnboundedCategoryStartingAtZero_IsValid()
    {
        var categories = new[] { Cat("Any", 0, null) };
        _validator.Validate(categories).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MaxNotGreaterThanMin_IsRejected()
    {
        var categories = new[]
        {
            Cat("Broken", 0, 0),
            Cat("Heavy", 0, null),
        };

        _validator.Validate(categories).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidReconfiguration_MovingBoundary_IsAccepted()
    {
        // The §6 example: Medium/Heavy boundary moves from 2500 to 2000.
        var categories = new[]
        {
            Cat("Light", 0, 500),
            Cat("Medium", 500, 2000),
            Cat("Heavy", 2000, null),
        };

        _validator.Validate(categories).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DeletingMiddleCategory_LeavesGap_IsRejected()
    {
        // Simulates what CategoryService passes when deleting "Medium":
        // the proposed set is just the remaining categories.
        var remainingAfterDeletingMedium = new[]
        {
            Cat("Light", 0, 500),
            Cat("Heavy", 2500, null),
        };

        _validator.Validate(remainingAfterDeletingMedium).IsValid.Should().BeFalse();
    }
}
