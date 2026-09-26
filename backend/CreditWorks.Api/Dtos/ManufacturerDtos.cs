using System.ComponentModel.DataAnnotations;

namespace CreditWorks.Api.Dtos;

public class ManufacturerResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ManufacturerRequest
{
    [Required(ErrorMessage = "Manufacturer name is required.")]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}
