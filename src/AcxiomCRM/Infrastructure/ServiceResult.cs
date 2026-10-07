namespace AcxiomCRM.Infrastructure;

public enum ServiceErrorKind
{
    None,
    Validation,
    NotFound,
    Conflict
}

/// <summary>
/// Outcome of a business operation. Errors are keyed by field name so MVC controllers can push
/// them into ModelState and API controllers can turn them into ProblemDetails.
/// </summary>
public class ServiceResult
{
    public ServiceErrorKind Kind { get; protected set; }
    public Dictionary<string, string> Errors { get; } = new();
    public bool Succeeded => Kind == ServiceErrorKind.None;

    public static ServiceResult Ok() => new();
    public static ServiceResult NotFound() => new() { Kind = ServiceErrorKind.NotFound };

    public static ServiceResult Invalid(string field, string message) => new ServiceResult().AddError(field, message);

    public static ServiceResult Conflict(string field, string message) =>
        new ServiceResult { Kind = ServiceErrorKind.Conflict }.AddErrorKeepKind(field, message);

    public ServiceResult AddError(string field, string message)
    {
        if (Kind == ServiceErrorKind.None) Kind = ServiceErrorKind.Validation;
        Errors[field] = message;
        return this;
    }

    private ServiceResult AddErrorKeepKind(string field, string message)
    {
        Errors[field] = message;
        return this;
    }
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Value { get; private init; }

    public static ServiceResult<T> Ok(T value) => new() { Value = value };

    public static ServiceResult<T> From(ServiceResult failure)
    {
        var r = new ServiceResult<T> { Kind = failure.Kind };
        foreach (var (k, v) in failure.Errors) r.Errors[k] = v;
        return r;
    }
}
