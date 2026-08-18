namespace Fidelizar.Domain.Exceptions;

/// <summary>
/// Base type for every exception the product throws on purpose (as opposed to a bug). Carries an
/// error code and optional per-field detail — enough for <c>Fidelizar.Api</c>'s exception
/// middleware to map it to an HTTP status and to <c>Fidelizar.Shared.Errors.ErrorResponse</c>.
/// Domain knows nothing about HTTP or that response type (ARCHITECTURE §3): the translation
/// happens in Api, the only layer referencing both.
/// </summary>
public abstract class AppException : Exception
{
    public string ErrorCode { get; }

    private readonly List<AppExceptionDetail> _details = [];

    public IReadOnlyCollection<AppExceptionDetail> Details => _details;

    protected AppException(string message, string errorCode, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public AppException WithDetail(string field, string issue)
    {
        _details.Add(new AppExceptionDetail(field, issue));
        return this;
    }

    public AppException WithDetails(IEnumerable<(string Field, string Issue)> details)
    {
        foreach (var (field, issue) in details)
        {
            WithDetail(field, issue);
        }

        return this;
    }
}

/// <summary>A single field-level complaint attached to an <see cref="AppException"/>.</summary>
public readonly record struct AppExceptionDetail(string Field, string Issue);
