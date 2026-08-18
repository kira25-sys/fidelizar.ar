namespace Fidelizar.Client.Api;

/// <summary>Typed wrapper over the REST contract (docs/REST-CONTRACT-F1.md). Never throws — every call returns an ApiResult.</summary>
public interface IApiClient
{
    /// <summary>Raised on a 401 — SessionService clears itself on this.</summary>
    event Action? SessionExpired;

    Task<ApiResult<TResponse>> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default);

    Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string requestUri, TRequest body, CancellationToken cancellationToken = default);

    Task<ApiResult> PostAsync<TRequest>(string requestUri, TRequest body, CancellationToken cancellationToken = default);

    Task<ApiResult> PostAsync(string requestUri, CancellationToken cancellationToken = default);
}
