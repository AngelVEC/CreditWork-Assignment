using System.Net;
using CreditWorks.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CreditWorks.Api.Middleware;

/// <summary>
/// Maps our custom ApiException hierarchy to RFC 7807 ProblemDetails
/// responses, and ensures any *unexpected* exception is logged server-side
/// but never leaks a stack trace or implementation detail to the client.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            NotFoundApiException => (HttpStatusCode.NotFound, "Not found"),
            ValidationApiException => (HttpStatusCode.BadRequest, "Validation failed"),
            ConflictApiException => (HttpStatusCode.Conflict, "Conflict"),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            // Full details logged server-side only.
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Type = $"https://tools.ietf.org/html/rfc7231#section-6.{(status == HttpStatusCode.NotFound ? "5.4" : status == HttpStatusCode.Conflict ? "5.8" : "5.1")}",
        };

        if (ex is ValidationApiException validationEx)
        {
            problem.Extensions["errors"] = validationEx.Errors;
        }
        else
        {
            problem.Detail = status == HttpStatusCode.InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : ex.Message;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;
        await context.Response.WriteAsJsonAsync(problem);
    }
}
