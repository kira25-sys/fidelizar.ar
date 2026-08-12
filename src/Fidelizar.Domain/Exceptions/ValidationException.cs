namespace Fidelizar.Domain.Exceptions;

/// <summary>Thrown when input data fails validation. Maps to HTTP 400 in Fidelizar.Api.</summary>
public class ValidationException : AppException
{
    public ValidationException(
        string message = "One or more validation errors occurred.",
        string errorCode = "VALIDATION_ERROR")
        : base(message, errorCode)
    {
    }
}
