using System.Net;
using System.Net.Http.Json;
using CreditWorks.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace CreditWorks.Api.Tests.Integration;

public class AuthFlowTests : IDisposable
{
    private readonly CreditWorksApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient NewClient() => _factory.CreateClient(new()
    {
        HandleCookies = true
    });

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200AndSetsHttpOnlyCookie_NoTokenInBody()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        var cookie = cookies!.First();
        cookie.Should().Contain("HttpOnly");
        cookie.Should().Contain("SameSite=Strict");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("eyJ"); // no raw JWT (base64 JWTs start with "eyJ") in the response body
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithGenericMessage()
    {
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = "definitely-wrong"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_Returns401WithSameGenericMessageAsWrongPassword()
    {
        var client = NewClient();

        var wrongUserResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "not-a-real-user",
            Password = "whatever"
        });
        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = "whatever-wrong"
        });

        var wrongUserBody = await wrongUserResponse.Content.ReadAsStringAsync();
        var wrongPasswordBody = await wrongPasswordResponse.Content.ReadAsStringAsync();

        wrongUserResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPasswordResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongUserBody.Should().Be(wrongPasswordBody); // identical — doesn't reveal which field was wrong
    }

    [Fact]
    public async Task Session_WithNoCookie_Returns401()
    {
        var client = NewClient();

        var response = await client.GetAsync("/api/auth/session");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Session_AfterLogin_Returns200WithUsernameAndRole()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });

        var response = await client.GetAsync("/api/auth/session");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        session!.Username.Should().Be("admin");
        session.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Logout_ThenSession_Returns401()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });

        var logoutResponse = await client.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessionResponse = await client.GetAsync("/api/auth/session");
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CategoriesEndpoint_WithNoAuthCookie_Returns401()
    {
        var client = NewClient();

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CategoriesEndpoint_AfterLogin_Returns200()
    {
        var client = NewClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
