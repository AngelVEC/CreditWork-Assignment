using System.ComponentModel.DataAnnotations;

namespace CreditWorks.Api.Dtos;

public class CategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public decimal MinWeightKg { get; set; }
    public decimal? MaxWeightKg { get; set; }
}

public class CategoryRequest
{
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(50, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category icon is required.")]
    [StringLength(50, MinimumLength = 1)]
    public string IconKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Minimum weight is required.")]
    public decimal MinWeightKg { get; set; }

    /// <summary>Null means "unbounded" — only exactly one category may have this.</summary>
    public decimal? MaxWeightKg { get; set; }
}
