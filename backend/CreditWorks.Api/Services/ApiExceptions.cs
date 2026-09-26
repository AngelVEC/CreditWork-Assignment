namespace CreditWorks.Api.Services;

/// <summary>Base for exceptions the middleware knows how to turn into a ProblemDetails response.</summary>
public abstract class ApiException : Exception
{
    protected ApiException(string message) : base(message) { }
}

public class NotFoundApiException : ApiException
{
    public NotFoundApiException(string entityName, object id)
        : base($"{entityName} with id '{id}' was not found.") { }
}

public class ValidationApiException : ApiException
{
    public IReadOnlyList<string> Errors { get; }

    public ValidationApiException(string message) : base(message)
    {
        Errors = new[] { message };
    }

    public ValidationApiException(IReadOnlyList<string> errors)
        : base(errors.Count > 0 ? errors[0] : "Validation failed.")
    {
        Errors = errors;
    }
}

public class ConflictApiException : ApiException
{
    public ConflictApiException(string message) : base(message) { }
}
