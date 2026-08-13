namespace Fidelizar.Domain.Exceptions;

/// <summary>
/// Base type for every exception the product throws on purpose (as opposed to a bug). Carries an
/// error code, a message and optional per-field detail — enough for Fidelizar.Api's
/// ExceptionHandlingMiddleware to map it to an HTTP status and to the wire-level
/// Fidelizar.Shared.Errors.ErrorResponse.
///
/// Domain deliberately does not know about ErrorResponse or about HTTP: ARCHITECTURE §3 says
/// Domain depends on nothing, so the translation to the wire contract happens in Api, which is
/// the only layer that references both Domain and Shared.
///
/// Ported from Dsw2026Tpi (ARCHITECTURE §15), adapted: no CrossCutting project, no
/// ResourceManager-backed error strings (Domain has no file I/O), lands directly in Domain.
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
