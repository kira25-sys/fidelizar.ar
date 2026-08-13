using System.Globalization;
using System.Threading.RateLimiting;
using Fidelizar.Api.Options;
using Fidelizar.Shared.Errors;
using Microsoft.AspNetCore.RateLimiting;

namespace Fidelizar.Api.Configurations;

/// <summary>
/// Ported from Dsw2026Tpi (ARCHITECTURE §15, §8), reduced to a single global, IP-based limiter:
/// this wave has no login endpoint yet to give its own named policy — that arrives with F1-03,
/// which can add named policies here the same way the university project scoped one per login
/// type. Registering this in DI is not enough by itself: <c>Program.cs</c> must also call
/// <c>app.UseRateLimiter()</c> in the request pipeline, or nothing is actually enforced
/// (ARCHITECTURE §8 calls this out explicitly).
/// </summary>
public static class RateLimitingConfigurationExtensions
{
    private const string LoggerCategory = "Fidelizar.Api.RateLimiting";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("RateLimiting").Get<RateLimitSettings>() ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = settings.QueueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true,
                    }));

            options.OnRejected = HandleRejectionAsync;
        });

        return services;
    }

    private static string GetPartitionKey(HttpContext context) => $"ip:{GetIp(context)}";

    private static string GetIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static async ValueTask HandleRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;

        var logger = httpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(LoggerCategory);

        logger.LogWarning(
            "Rate limit exceeded. Path: {Path}, IP: {Ip}",
            httpContext.Request.Path.Value,
            GetIp(httpContext));

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        var error = new ErrorResponse("RATE_LIMIT_EXCEEDED", "Too many requests. Try again later.");

        httpContext.Response.ContentType = "application/json";
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
    }
}
