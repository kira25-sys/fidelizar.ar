using System.Text.Json;
using Fidelizar.Api.Middleware;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fidelizar.Api.Tests;

/// <summary>
/// The exception type → HTTP status mapping lives in exactly one place (ARCHITECTURE §15). These
/// tests exercise the middleware directly, with no host or network involved.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    [Theory]
    [InlineData(typeof(ValidationExceptionThrower), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(EntityNotFoundExceptionThrower), StatusCodes.Status404NotFound)]
    [InlineData(typeof(ConflictExceptionThrower), StatusCodes.Status409Conflict)]
    [InlineData(typeof(AuthenticationExceptionThrower), StatusCodes.Status401Unauthorized)]
    [InlineData(typeof(AuthorizationExceptionThrower), StatusCodes.Status403Forbidden)]
    [InlineData(typeof(UnhandledExceptionThrower), StatusCodes.Status500InternalServerError)]
    public async Task Maps_exception_type_to_the_expected_status_code(Type throwerType, int expectedStatus)
    {
        var thrower = (IExceptionThrower)Activator.CreateInstance(throwerType)!;
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw thrower.Create(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task Serialises_the_AppException_error_code_message_and_details()
    {
        static Exception CreateValidationException() =>
            new ValidationException("Amount is required.", "AMOUNT_REQUIRED")
                .WithDetail("monto", "required");

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw CreateValidationException(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        body.Seek(0, SeekOrigin.Begin);
        var error = await JsonSerializer.DeserializeAsync<ErrorResponse>(
            body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(error);
        Assert.Equal("AMOUNT_REQUIRED", error!.ErrorCode);
        Assert.Equal("Amount is required.", error.Message);
        var detail = Assert.Single(error.Details);
        Assert.Equal("monto", detail.Field);
        Assert.Equal("required", detail.Issue);
    }

    private interface IExceptionThrower
    {
        Exception Create();
    }

    private sealed class ValidationExceptionThrower : IExceptionThrower
    {
        public Exception Create() => new ValidationException();
    }

    private sealed class EntityNotFoundExceptionThrower : IExceptionThrower
    {
        public Exception Create() => new EntityNotFoundException("Miembro");
    }

    private sealed class ConflictExceptionThrower : IExceptionThrower
    {
        public Exception Create() => new ConflictException("Already exists.");
    }

    private sealed class AuthenticationExceptionThrower : IExceptionThrower
    {
        public Exception Create() => new AuthenticationException();
    }

    private sealed class AuthorizationExceptionThrower : IExceptionThrower
    {
        public Exception Create() => new AuthorizationException();
    }

    private sealed class UnhandledExceptionThrower : IExceptionThrower
    {
        public Exception Create() => new InvalidOperationException("boom");
    }
}
