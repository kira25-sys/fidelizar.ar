using Microsoft.AspNetCore.Mvc;

namespace Fidelizar.Api.Tests.Security.Fakes;

/// <summary>
/// Test-only controller with no <c>[Authorize]</c> and no <c>[AllowAnonymous]</c> at all — the
/// exact shape of "somebody forgot the attribute". Wired into the real pipeline via
/// <c>FallbackPolicyPipelineTests</c>' <see cref="Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart"/>,
/// never shipped in <c>Fidelizar.Api</c> itself.
/// </summary>
[ApiController]
[Route("api/test/sin-atributo")]
public sealed class SinAtributoTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping() => Ok();
}
