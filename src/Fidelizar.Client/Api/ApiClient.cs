using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fidelizar.Shared.Errors;

namespace Fidelizar.Client.Api;

/// <summary>See IApiClient. Registered scoped in Program.cs.</summary>
public sealed class ApiClient(HttpClient httpClient, CsrfTokenProvider csrfTokenProvider) : IApiClient
{
    /// <summary>Mirrors AntiforgeryConfigurationExtensions.HeaderName in Fidelizar.Api — Client can't reference Api.</summary>
    private const string CsrfHeaderName = "X-CSRF-TOKEN";

    /// <summary>Mirrors AntiforgeryTokenRequiredAttribute's ErrorCode.</summary>
    private const string AntiforgeryErrorCode = "ANTIFORGERY_TOKEN_INVALIDO";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public event Action? SessionExpired;

    public Task<ApiResult<TResponse>> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Get, requestUri), attachCsrf: false, cancellationToken);

    public Task<ApiResult<TResponse>> PostAsync<TRequest, TResponse>(
        string requestUri, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => BuildJsonPost(requestUri, body), attachCsrf: true, cancellationToken);

    public async Task<ApiResult> PostAsync<TRequest>(
        string requestUri, TRequest body, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<object?>(
            () => BuildJsonPost(requestUri, body), attachCsrf: true, cancellationToken, expectBody: false);
        return result.Success ? ApiResult.Ok() : ApiResult.Fail(result.Problem!);
    }

    public async Task<ApiResult> PostAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        var result = await SendAsync<object?>(
            () => new HttpRequestMessage(HttpMethod.Post, requestUri), attachCsrf: true, cancellationToken, expectBody: false);
        return result.Success ? ApiResult.Ok() : ApiResult.Fail(result.Problem!);
    }

    private static HttpRequestMessage BuildJsonPost<TRequest>(string requestUri, TRequest body) =>
        new(HttpMethod.Post, requestUri) { Content = JsonContent.Create(body, options: JsonOptions) };

    /// <summary>Attaches CSRF, retries once on a stale token, maps every failure to ApiProblem.</summary>
    private async Task<ApiResult<TResponse>> SendAsync<TResponse>(
        Func<HttpRequestMessage> requestFactory, bool attachCsrf, CancellationToken cancellationToken, bool expectBody = true)
    {
        var alreadyRetriedCsrf = false;
        while (true)
        {
            using var request = requestFactory();

            if (attachCsrf)
            {
                string csrfToken;
                try
                {
                    csrfToken = await csrfTokenProvider.GetTokenAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    return ApiResult<TResponse>.Fail(OfflineProblem());
                }

                request.Headers.Add(CsrfHeaderName, csrfToken);
            }

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // caller cancelled — not a failure to report
            }
            catch (OperationCanceledException)
            {
                // HttpClient.Timeout fired, not the caller's token.
                return ApiResult<TResponse>.Fail(new ApiProblem(
                    Message: "La conexión está tardando demasiado. Probá de nuevo.",
                    StatusCode: null, ErrorCode: ApiErrorCodes.Timeout, IsOffline: true));
            }
            catch (HttpRequestException)
            {
                return ApiResult<TResponse>.Fail(OfflineProblem());
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    if (!expectBody || response.StatusCode == HttpStatusCode.NoContent)
                    {
                        return ApiResult<TResponse>.Ok(default!);
                    }

                    try
                    {
                        var value = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
                        return ApiResult<TResponse>.Ok(value!);
                    }
                    catch (JsonException)
                    {
                        return ApiResult<TResponse>.Fail(OfflineProblem());
                    }
                }

                var problem = await ToProblemAsync(response, cancellationToken);

                if (attachCsrf && !alreadyRetriedCsrf && problem.ErrorCode == AntiforgeryErrorCode)
                {
                    alreadyRetriedCsrf = true;
                    csrfTokenProvider.Invalidate();
                    continue; // safe — nothing was mutated (filter runs before the action)
                }

                if (problem.IsUnauthorized)
                {
                    SessionExpired?.Invoke();
                }

                return ApiResult<TResponse>.Fail(problem);
            }
        }
    }

    private static async Task<ApiProblem> ToProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ErrorResponse? body = null;
        try
        {
            body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // A proxy/framework error page (e.g. a raw 502) is not JSON — fall back below.
        }

        var problem = new ApiProblem(
            body?.Message ?? FallbackMessageFor(response.StatusCode), response.StatusCode, body?.ErrorCode);

        return body is null || body.Details.Count == 0 ? problem : problem with { Details = [.. body.Details] };
    }

    private static string FallbackMessageFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "Tu sesión venció. Iniciá sesión de nuevo.",
        HttpStatusCode.Forbidden => "No tenés permiso para hacer esto.",
        HttpStatusCode.TooManyRequests => "Demasiados intentos. Esperá un momento y volvé a intentar.",
        _ => "Ocurrió un error inesperado. Intentá de nuevo en unos segundos.",
    };

    private static ApiProblem OfflineProblem() => new(
        Message: "No pudimos conectar con el servidor. Revisá tu conexión e intentá de nuevo.",
        StatusCode: null, ErrorCode: ApiErrorCodes.Offline, IsOffline: true);
}
