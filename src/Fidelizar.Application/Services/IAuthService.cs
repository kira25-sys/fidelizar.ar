using Fidelizar.Domain.Entities;

namespace Fidelizar.Application.Services;

/// <summary>
/// Session authentication (ARCHITECTURE §8). Verifies credentials and hands back the
/// <see cref="Usuario"/> on success — minting the JWT and setting the cookie are HTTP concerns and
/// stay in <c>Fidelizar.Api</c> (ARCHITECTURE §3).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Throws <see cref="Fidelizar.Domain.Exceptions.AuthenticationException"/> when the email is
    /// unknown, the password is wrong, or the account is deactivated — the same message and error
    /// code in every case, so a failed login never tells a caller which of the three it was.
    /// </summary>
    Task<Usuario> AutenticarAsync(string email, string password, CancellationToken cancellationToken = default);
}
