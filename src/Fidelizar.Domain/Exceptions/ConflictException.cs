namespace Fidelizar.Domain.Exceptions;

/// <summary>
/// Thrown when a request conflicts with the current state of a resource (for example, a
/// duplicate). Maps to HTTP 409 in Fidelizar.Api.
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message, string errorCode = "CONFLICT")
        : base(message, errorCode)
    {
    }
}
