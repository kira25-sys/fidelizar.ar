namespace Fidelizar.Domain.Security;

/// <summary>
/// Abstraction over password hashing so <c>Fidelizar.Application</c> can verify a login without
/// depending on ASP.NET Core (ARCHITECTURE §3: "Application knows nothing about HTTP or EF
/// either"). <c>Fidelizar.Infrastructure</c> implements this using ASP.NET Core Identity's
/// hasher — DATA-MODEL §2 sanctions that exact algorithm for <see cref="Entities.Usuario.PasswordHash"/>.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string providedPassword);
}
