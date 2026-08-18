using System.Text;
using Fidelizar.Api.Options;
using Fidelizar.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Fidelizar.Api.Configurations;

/// <summary>
/// JWT-in-cookie authentication and the role→policy mapping (ARCHITECTURE §8). Two things are
/// non-negotiable here and both are enforced in this one place:
///
/// <list type="bullet">
/// <item>The signing key comes from configuration (environment or user secrets) and is validated
/// before the host is allowed to start — missing or under
/// <see cref="MinimumSigningKeyBytes"/> bytes throws immediately, with a message naming the
/// environment variable, rather than failing on the first login attempt.</item>
/// <item><c>JwtBearer</c> reads the token from <see cref="AuthCookie.Name"/> via
/// <c>OnMessageReceived</c> instead of the <c>Authorization</c> header — everything below
/// (<c>[Authorize]</c>, policies, roles) is unchanged from the framework default.</item>
/// </list>
/// </summary>
public static class AuthenticationConfigurationExtensions
{
    // RFC 7518 §3.2: HS256 requires a key of at least 256 bits.
    private const int MinimumSigningKeyBytes = 32;

    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        var keyBytes = Encoding.UTF8.GetBytes(settings.SigningKey);

        if (keyBytes.Length < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Falta o es demasiado corta 'Jwt:SigningKey' (ARCHITECTURE §8, {keyBytes.Length} " +
                $"de {MinimumSigningKeyBytes} bytes mínimos para HS256). No se inventa una clave " +
                "de desarrollo: configurarla con 'dotnet user-secrets' o la variable de entorno " +
                "Jwt__SigningKey. En los tests, generarla al vuelo dentro del propio test — nunca " +
                "escribirla en un archivo de configuración.");
        }

        services.AddSingleton(settings);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };

                options.Events = new JwtBearerEvents
                {
                    // ARCHITECTURE §8: the token travels in an HttpOnly cookie, never in the
                    // Authorization header set by JavaScript — this is the one line that makes
                    // JwtBearer read it from there instead of its framework default.
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Cookies.TryGetValue(AuthCookie.Name, out var token) &&
                            !string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        // ARCHITECTURE §8 role table: Encargada can do everything a Cajero can, Dueno everything
        // Encargada can. Soporte's "audited technical access" is a separate lane, deliberately not
        // part of this ladder — Security.Policies' own comment already scopes it this way.
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CajeroOrAbove, policy =>
                policy.RequireRole(Roles.Cajero, Roles.Encargada, Roles.Dueno))
            .AddPolicy(Policies.EncargadaOrAbove, policy =>
                policy.RequireRole(Roles.Encargada, Roles.Dueno))
            .AddPolicy(Policies.DuenoOnly, policy =>
                policy.RequireRole(Roles.Dueno));

        return services;
    }
}
