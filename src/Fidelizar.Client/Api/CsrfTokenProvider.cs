using System.Net.Http.Json;

namespace Fidelizar.Client.Api;

/// <summary>Caches the antiforgery token from GET /api/auth/csrf-token. Used only by ApiClient.</summary>
public sealed class CsrfTokenProvider(HttpClient httpClient)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedToken;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is { } token)
        {
            return token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is { } cached)
            {
                return cached;
            }

            var response = await httpClient.GetFromJsonAsync<CsrfTokenResponse>(
                "api/auth/csrf-token", cancellationToken);
            _cachedToken = response?.Token
                ?? throw new InvalidOperationException("El servidor no devolvió un token antifalsificación.");
            return _cachedToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Safe to retry after: the antiforgery filter runs before the action, nothing mutated yet.</summary>
    public void Invalidate() => _cachedToken = null;

    private sealed record CsrfTokenResponse(string Token);
}
