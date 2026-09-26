using System.ComponentModel.DataAnnotations;

namespace CreditWorks.Api.Dtos;

public class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Returned by both POST /auth/login (on success) and GET /auth/session.
/// Never contains the raw token — the JWT only ever exists as an httpOnly
/// cookie, never in a response body or in JS-readable form.
/// </summary>
public class SessionResponse
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
