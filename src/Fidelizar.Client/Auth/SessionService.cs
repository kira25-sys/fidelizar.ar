using Fidelizar.Client.Api;
using Fidelizar.Shared.Auth;

namespace Fidelizar.Client.Auth;

/// <summary>See ISessionService. Registered scoped in Program.cs.</summary>
public sealed class SessionService : ISessionService, IDisposable
{
    private readonly IApiClient _apiClient;

    public SessionService(IApiClient apiClient)
    {
        _apiClient = apiClient;
        _apiClient.SessionExpired += HandleSessionExpired;
    }

    public SesionResponse? Current { get; private set; }
    public bool IsAuthenticated => Current is not null;
    public event Action? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<SesionResponse>("api/auth/me", cancellationToken);
        SetCurrent(result.Success ? result.Value : null);
    }

    public async Task<ApiResult<SesionResponse>> LoginAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.PostAsync<LoginRequest, SesionResponse>(
            "api/auth/login", new LoginRequest(email, password), cancellationToken);

        if (result.Success)
        {
            SetCurrent(result.Value);
        }

        return result;
    }

    public async Task<ApiResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        // Only clears client state on success — a failed logout leaves the cookie standing server-side.
        var result = await _apiClient.PostAsync("api/auth/logout", cancellationToken);
        if (result.Success)
        {
            SetCurrent(null);
        }

        return result;
    }

    private void HandleSessionExpired() => SetCurrent(null);

    private void SetCurrent(SesionResponse? session)
    {
        Current = session;
        Changed?.Invoke();
    }

    public void Dispose() => _apiClient.SessionExpired -= HandleSessionExpired;
}
