namespace Fidelizar.Domain.Exceptions;

/// <summary>
/// Thrown when an authenticated user lacks permission for the requested operation. Maps to HTTP
/// 403 in <c>Fidelizar.Api</c>, where authorization policies decide (ARCHITECTURE §8).
/// </summary>
public class AuthorizationException : AppException
{
    public AuthorizationException(string message = "Permissions are required for the requested operation.")
        : base(message, "AUTHORIZATION_FAILED")
    {
    }
}
