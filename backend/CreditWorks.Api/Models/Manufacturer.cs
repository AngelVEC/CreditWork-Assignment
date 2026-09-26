namespace CreditWorks.Api.Models;

/// <summary>
/// A vehicle manufacturer. Modeled as its own table (rather than a hardcoded
/// enum or embedded string) so new manufacturers can be added later without
/// a code change or redeploy — see the assignment's note about not embedding
/// manufacturers throughout the application.
/// </summary>
public class Manufacturer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
