using System.Text;
using CreditWorks.Api.Controllers;
using CreditWorks.Api.Data;
using CreditWorks.Api.Middleware;
using CreditWorks.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ---------------------------------------------------
// All sensitive values (connection string, JWT key, seed admin creds) come
// from environment variables / user-secrets, never from appsettings.json
// itself — see appsettings.Example.json and README "Configuration".
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Set the " +
        "ConnectionStrings__DefaultConnection environment variable or a user-secret.");
}

var corsOrigin = builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173";

// ---- Services ----------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<ICategoryResolver, CategoryResolver>();
builder.Services.AddScoped<ICategoryRangeValidator, CategoryRangeValidator>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        // A wildcard origin cannot be combined with AllowCredentials(), so
        // the frontend origin must be explicit — see README "Configuration".
        policy.WithOrigins(corsOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var configuration = builder.Configuration;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "CreditWorksVehicleApp",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "CreditWorksVehicleApp",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"] ?? "dev-only-placeholder-key-not-for-production-use")),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // The JWT Bearer middleware reads from the Authorization header by
        // default. Our token instead travels as an httpOnly cookie, so we
        // pull it from there — this is the one non-obvious wiring step in
        // the whole cookie-based auth design (see build notes §3.4).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthController.CookieName, out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ---- Database creation + seeding ---------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await SeedData.InitializeAsync(db, app.Configuration, logger);
}

// ---- Middleware pipeline -------------------------------------------------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
