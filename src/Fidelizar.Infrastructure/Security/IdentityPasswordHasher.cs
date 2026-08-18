using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace Fidelizar.Infrastructure.Security;

/// <summary>
/// Wraps <see cref="PasswordHasher{TUser}"/> — DATA-MODEL §2's sanctioned algorithm for
/// <c>Usuario.PasswordHash</c> — behind <see cref="IPasswordHasher"/>, so
/// <c>Fidelizar.Application</c> depends on the interface only, never on ASP.NET Core Identity
/// directly (ARCHITECTURE §3). <see cref="Usuario"/> is passed as the generic user type purely to
/// satisfy <see cref="PasswordHasher{TUser}"/>'s signature — its default (V3) implementation is
/// salted PBKDF2-HMACSHA256 and never reads any property off that argument, so <c>null!</c> below
/// is the well-known, safe way to use it outside the full Identity user-store machinery this
/// product does not adopt (DATA-MODEL §2: <c>Usuario</c> is its own lean entity).
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string providedPassword) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, providedPassword) is
            PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
