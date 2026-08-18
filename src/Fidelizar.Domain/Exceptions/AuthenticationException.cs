namespace Fidelizar.Domain.Exceptions;

/// <summary>
/// Thrown when authentication fails (wrong credentials, missing or invalid token). Maps to HTTP
/// 401 in <c>Fidelizar.Api</c>. Thrown by <c>AuthService</c> on a failed login.
/// </summary>
public class AuthenticationException : AppException
{
    public AuthenticationException(string message = "Authentication failed.")
        : base(message, "AUTHENTICATION_FAILED")
    {
    }
}
