namespace Fidelizar.Api.Options;

/// <summary>
/// Bound once from the "RateLimiting" configuration section at startup — not read ad hoc from
/// <c>IConfiguration</c> at request time (ARCHITECTURE §15: bind options once, validate at
/// startup). Defaults are deliberately generous; a business number does not belong hard-coded in
/// a rate limiter either, so every value is configurable per deployment.
/// </summary>
public sealed class RateLimitSettings
{
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; }
}
