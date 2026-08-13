namespace Fidelizar.Domain.Exceptions;

/// <summary>
/// Thrown when an authenticated user lacks permission for the requested operation. Maps to HTTP
/// 403 in Fidelizar.Api. Not thrown anywhere yet — role checks arrive with F1-03.
/// </summary>
public class AuthorizationException : AppException
{
    public AuthorizationException(string message = "Permissions are required for the requested operation.")
        : base(message, "AUTHORIZATION_FAILED")
    {
    }
}
