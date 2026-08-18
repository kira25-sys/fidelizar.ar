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

    /// <summary>
    /// Login is a password endpoint exposed to the internet — a global 100/60s limit is too
    /// generous to be a real brute-force defence (ARCHITECTURE §8, F1-03). Tighter than
    /// <see cref="PermitLimit"/> by default, and stacks with it: a login request is checked
    /// against both the global limiter and this named policy.
    /// </summary>
    public LoginRateLimitSettings Login { get; set; } = new();
}

/// <summary>See <see cref="RateLimitSettings.Login"/>.</summary>
public sealed class LoginRateLimitSettings
{
    public int PermitLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;
    public int QueueLimit { get; set; }
}
