using System.Net;
using Fidelizar.Shared.Errors;

namespace Fidelizar.Client.Api;

/// <summary>The one Spanish error/offline shape every failed call produces (ROADMAP F1-04c).</summary>
public sealed record ApiProblem(string Message, HttpStatusCode? StatusCode, string? ErrorCode = null, bool IsOffline = false)
{
    public IReadOnlyCollection<ErrorDetail> Details { get; init; } = [];

    /// <summary>By status code, not by matching a server string — a bare 401 may carry no body.</summary>
    public bool IsUnauthorized => StatusCode == HttpStatusCode.Unauthorized;
}

/// <summary>Client-only fallback codes for failures with no server <c>ErrorResponse</c>.</summary>
public static class ApiErrorCodes
{
    public const string Offline = "CLIENTE_SIN_CONEXION";
    public const string Timeout = "CLIENTE_TIEMPO_AGOTADO";
    public const string Unexpected = "CLIENTE_ERROR_INESPERADO";
}
