using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CreditWorks.Api.Data;
using CreditWorks.Api.Dtos;
using CreditWorks.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CreditWorks.Api.Services;

public interface IAuthService
{
    /// <summary>Verifies credentials. Returns (token, session) on success, or null on failure.</summary>
    Task<(string Token, SessionResponse Session)?> LoginAsync(LoginRequest request);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<(string Token, SessionResponse Session)?> LoginAsync(LoginRequest request)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Username == request.Username);
        if (admin is null)
        {
            // Deliberately identical failure path to "wrong password" below —
            // never reveal whether the username or password was the problem.
            return null;
        }

        var hasher = new PasswordHasher<Admin>();
        var result = hasher.VerifyHashedPassword(admin, admin.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        var signingKey = _config["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var issuer = _config["Jwt:Issuer"] ?? "CreditWorksVehicleApp";
        var audience = _config["Jwt:Audience"] ?? "CreditWorksVehicleApp";
        var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 90;

        var expires = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim("username", admin.Username),
            new Claim(ClaimTypes.Role, admin.Role),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var session = new SessionResponse
        {
            Username = admin.Username,
            Role = admin.Role,
            ExpiresAtUtc = expires
        };

        return (tokenString, session);
    }
}
