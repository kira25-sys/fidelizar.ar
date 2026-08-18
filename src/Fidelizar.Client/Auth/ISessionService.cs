using Fidelizar.Client.Api;
using Fidelizar.Shared.Auth;

namespace Fidelizar.Client.Auth;

/// <summary>Who is signed in, per GET /api/auth/me — the JWT cookie is HttpOnly, unreadable here (ARCHITECTURE §8).</summary>
public interface ISessionService
{
    SesionResponse? Current { get; }
    bool IsAuthenticated { get; }

    /// <summary>Fires on sign in, sign out, or a background 401.</summary>
    event Action? Changed;

    /// <summary>Anonymous is not an error — Current just stays null.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<SesionResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default);
}
