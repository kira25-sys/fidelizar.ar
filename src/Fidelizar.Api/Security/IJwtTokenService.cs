using System.Security.Claims;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Security;

/// <summary>Mints the JWT that <see cref="AuthCookie"/> carries. HTTP/token concern, deliberately
/// kept out of <c>Fidelizar.Application</c> (ARCHITECTURE §3).</summary>
public interface IJwtTokenService
{
    /// <summary>A fresh token for a user who just authenticated.</summary>
    (string Token, DateTime ExpiresAtUtc) CrearToken(Usuario usuario);

    /// <summary>
    /// A fresh token carrying the same identity claims an already-validated principal has —
    /// silent renewal (ARCHITECTURE §8). No database round trip: the JWT is the only session
    /// state ("no server-side session store"), so renewing it re-signs what is already trusted
    /// rather than re-reading it.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) RenovarToken(ClaimsPrincipal principal);
}
