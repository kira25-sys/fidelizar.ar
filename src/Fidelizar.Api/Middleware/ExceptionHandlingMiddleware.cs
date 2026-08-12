using System.Net;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Shared.Errors;

namespace Fidelizar.Api.Middleware;

/// <summary>
/// Single place that maps an exception to an HTTP status and to the
/// <see cref="Fidelizar.Shared.Errors.ErrorResponse"/> wire contract. Ported from Dsw2026Tpi
/// (ARCHITECTURE §15): a controller never writes its own error response, it just throws.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error while processing the request");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        HttpStatusCode status;
        ErrorResponse error;

        if (ex is AppException appException)
        {
            status = StatusForAppException(appException);
            error = new ErrorResponse(appException.ErrorCode, appException.Message);
            error.AddDetails(appException.Details.Select(d => (d.Field, d.Issue)));
        }
        else
        {
            status = HttpStatusCode.InternalServerError;
            error = new ErrorResponse("UNHANDLED_ERROR", "An unhandled error occurred.");
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;
        await context.Response.WriteAsJsonAsync(error);
    }

    private static HttpStatusCode StatusForAppException(AppException ex) => ex switch
    {
        ValidationException => HttpStatusCode.BadRequest,
        EntityNotFoundException => HttpStatusCode.NotFound,
        ConflictException => HttpStatusCode.Conflict,
        AuthenticationException => HttpStatusCode.Unauthorized,
        AuthorizationException => HttpStatusCode.Forbidden,
        _ => HttpStatusCode.InternalServerError,
    };
}
