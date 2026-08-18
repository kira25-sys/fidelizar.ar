namespace Fidelizar.Domain.Security;

/// <summary>
/// Password hashing, abstracted so <c>Fidelizar.Application</c> can verify a login without
/// depending on ASP.NET Core (ARCHITECTURE §3). Implemented in <c>Fidelizar.Infrastructure</c>
/// with ASP.NET Core Identity's hasher, the algorithm DATA-MODEL §2 sanctions.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string providedPassword);
}
