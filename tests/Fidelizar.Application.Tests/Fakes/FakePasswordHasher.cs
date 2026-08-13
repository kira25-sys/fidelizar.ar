using Fidelizar.Domain.Security;

namespace Fidelizar.Application.Tests.Fakes;

/// <summary>
/// In-memory stand-in for <see cref="IPasswordHasher"/> (ARCHITECTURE §11) — no real PBKDF2, just
/// enough behaviour for <c>AuthServiceTests</c> to tell a correct password from a wrong one. The
/// real algorithm choice is tested against <c>Fidelizar.Infrastructure.Security.IdentityPasswordHasher</c>
/// directly, not through this fake.
/// </summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string passwordHash, string providedPassword) => passwordHash == Hash(providedPassword);
}
