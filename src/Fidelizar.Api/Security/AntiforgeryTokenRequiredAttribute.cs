using Fidelizar.Domain.Exceptions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fidelizar.Api.Security;

/// <summary>
/// Validates the antiforgery token on a state-changing endpoint (ARCHITECTURE §8: "every
/// state-changing endpoint additionally requires an antiforgery token" — SameSite=Strict alone is
/// a defect on its own). The client obtains the token from <c>GET /api/auth/csrf-token</c> and
/// echoes it back in the header <c>Configurations.AntiforgeryConfigurationExtensions.HeaderName</c>
/// names, on every unsafe request.
///
/// A thin custom filter rather than the framework's <c>[AutoValidateAntiforgeryToken]</c> so a
/// failure throws this product's own <see cref="ValidationException"/> and flows through
/// <c>ExceptionHandlingMiddleware</c>, producing the same <c>ErrorResponse</c> shape as every
/// other rejection instead of a different, ad hoc one. Named
/// <see cref="AntiforgeryTokenRequiredAttribute"/> rather than the more obvious
/// <c>RequireAntiforgeryTokenAttribute</c> because that exact name already exists in
/// <c>Microsoft.AspNetCore.Antiforgery</c> and the two would be ambiguous at every call site.
/// </summary>
public sealed class AntiforgeryTokenRequiredAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            throw new ValidationException(
                "Token antifalsificación inválido o ausente.", "ANTIFORGERY_TOKEN_INVALIDO");
        }
    }
}
