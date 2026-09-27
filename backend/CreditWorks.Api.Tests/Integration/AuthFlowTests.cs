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
        // ASP.NET Core writes Set-Cookie attributes in lowercase
        // ("httponly", "samesite=strict"), regardless of how CookieOptions
        // was written in code, so this check is case-insensitive.
        cookie.Should().ContainEquivalentOf("HttpOnly");
        cookie.Should().ContainEquivalentOf("SameSite=Strict");
        // The test server talks plain HTTP, so the cookie must NOT be
        // marked Secure here — a Secure cookie is silently dropped by the
        // client on the next request over a non-HTTPS connection, which is
        // exactly the bug this guards against (see AuthController.Login).
        cookie.Should().NotContainEquivalentOf("; secure");

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
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });
        // Fail loudly here, not three lines down, if login itself didn't
        // actually succeed (e.g. wrong seeded password) — otherwise a
        // login failure and a cookie-round-trip failure both just look
        // like "session came back 401" and are easy to conflate.
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "login itself must succeed before we can test the session it creates");

        var response = await client.GetAsync("/api/auth/session");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        session!.Username.Should().Be("admin");
        session.Role.Should().Be("Admin");
    }

    /// <summary>
    /// Diagnostic test: bypasses HttpClient's automatic CookieContainer
    /// entirely and manually copies the Set-Cookie value from the login
    /// response onto a raw Cookie header for the next request. If this
    /// passes, the server-side JWT/cookie validation is fine and the bug is
    /// specifically that the client isn't resending the cookie
    /// automatically. If this *also* fails, the bug is server-side (token
    /// validation, signing key, or claim mapping) rather than a client/
    /// cookie-jar issue.
    /// </summary>
    [Fact]
    public async Task Session_WithManuallyReattachedCookie_Returns200()
    {
        // Deliberately HandleCookies = false: we want to control the Cookie
        // header ourselves and prove nothing is happening automatically.
        var client = _factory.CreateClient(new() { HandleCookies = false });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        loginResponse.Headers.TryGetValues("Set-Cookie", out var setCookieValues).Should().BeTrue();
        var setCookie = setCookieValues!.First();
        // Set-Cookie looks like "cw_auth=<token>; expires=...; path=/; ...".
        // A request's Cookie header wants just the "name=value" part.
        var nameEqualsValue = setCookie.Split(';')[0];

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", nameEqualsValue);

        var response = await client.SendAsync(request);

        var debugReason = response.Headers.TryGetValues("X-Debug-Auth-Failure", out var reasonValues)
            ? reasonValues.First()
            : "(no X-Debug-Auth-Failure header — either it succeeded, or failed before/without OnAuthenticationFailed firing at all, e.g. no token found on the request)";

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "a manually reattached cookie proves whether the server accepts a cookie it's actually given, independent of whether HttpClient's cookie jar resends one automatically. " +
            $"Actual validation failure reason: {debugReason}. " +
            $"Cookie header sent: {(nameEqualsValue.Length > 60 ? nameEqualsValue[..60] + "..." : nameEqualsValue)}");
    }

    [Fact]
    public async Task Logout_ThenSession_Returns401()
    {
        var client = NewClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

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
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "admin",
            Password = CreditWorksApiFactory.SeedAdminPassword
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
