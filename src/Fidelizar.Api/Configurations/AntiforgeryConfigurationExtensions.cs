namespace Fidelizar.Api.Configurations;

/// <summary>
/// Registers antiforgery services for <c>Security.AntiforgeryTokenRequiredAttribute</c>
/// (ARCHITECTURE §8). The cookie stays <c>HttpOnly</c> — the piece the client must echo back is
/// the token value <c>AuthController.ObtenerTokenAntifalsificacion</c> hands out in the response
/// body, not something read out of the cookie by script.
/// </summary>
public static class AntiforgeryConfigurationExtensions
{
    public const string HeaderName = "X-CSRF-TOKEN";
    private const string CookieName = "fidelizar_csrf";

    public static IServiceCollection AddAppAntiforgery(this IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;

            // SameAsRequest, not Always: ASP.NET Core's antiforgery system refuses to even
            // validate a request — throwing InvalidOperationException, not a clean rejection —
            // when SecurePolicy is Always but HttpContext.Request.IsHttps is false for that
            // request. Kestrel sees Caddy's proxied connection as plain HTTP unless forwarded
            // headers are configured to trust X-Forwarded-Proto (ARCHITECTURE §14's Caddy setup
            // does not do that yet), so Always would 500 every antiforgery-protected call in
            // production, not just in local http:// development. SameAsRequest mirrors whatever
            // scheme Kestrel actually sees instead of hard-failing on a mismatch it cannot verify.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        return services;
    }
}
