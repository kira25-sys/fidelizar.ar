namespace Fidelizar.Domain.Exceptions;

/// <summary>
/// Thrown when authentication fails (wrong credentials, missing or invalid token). Maps to HTTP
/// 401 in Fidelizar.Api. Not thrown anywhere yet — authentication itself is F1-03.
/// </summary>
public class AuthenticationException : AppException
{
    public AuthenticationException(string message = "Authentication failed.")
        : base(message, "AUTHENTICATION_FAILED")
    {
    }
}
