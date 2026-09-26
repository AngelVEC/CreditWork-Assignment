namespace CreditWorks.Api.Models;

/// <summary>
/// An admin account, used solely to gate the Category Administration
/// screen/endpoints. Not related to any other table.
/// </summary>
public class Admin
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    /// <summary>Hashed via ASP.NET Core's PasswordHasher&lt;T&gt; (PBKDF2). Never plaintext.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "Admin";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
