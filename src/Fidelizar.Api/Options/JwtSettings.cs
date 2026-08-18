namespace Fidelizar.Api.Options;

/// <summary>
/// Bound once from the "Jwt" configuration section at startup, then validated before the
/// application is allowed to start (ARCHITECTURE §8: "The key is validated at startup: a missing
/// or short key stops the application from starting, rather than throwing on the first login
/// attempt"). See <c>Configurations.AuthenticationConfigurationExtensions</c> for that check.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>
    /// Comes from the environment or user secrets — never a literal here, never a default that
    /// lets a command "just run" (ARCHITECTURE §8, CLAUDE.md). Env var: <c>Jwt__SigningKey</c>.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "Fidelizar";

    public string Audience { get; set; } = "Fidelizar";

    /// <summary>
    /// Short on purpose (ARCHITECTURE §8: "Token lifetime is short and renewal is silent") — the
    /// "quien soy" endpoint reissues the cookie on every authenticated call, so this only bounds
    /// how long a genuinely abandoned session stays valid, not how long an active shift lasts.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;
}
