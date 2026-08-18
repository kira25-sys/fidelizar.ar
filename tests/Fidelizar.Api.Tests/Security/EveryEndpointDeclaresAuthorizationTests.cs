using Fidelizar.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// F1-04 (roadmap): "nothing is open by omission" is enforced structurally here, not by trusting
/// nobody forgets an attribute. <c>AddAppAuthentication</c>'s fallback policy already makes a
/// forgotten attribute require a session instead of nothing at all — this test additionally
/// fails the build the moment a new controller action ships without
/// an explicit <c>[Authorize]</c> or <c>[AllowAnonymous]</c>, so the gap is caught in review, not
/// discovered later by a role that should never have reached the endpoint (ARCHITECTURE §8).
///
/// Walks every public action on every <see cref="ControllerBase"/> in this assembly by
/// reflection, so a new controller is covered automatically — nobody has to remember to add it
/// here (the same reasoning as <c>RolUsuarioRolesSincronizadosTests</c>).
/// </summary>
public class EveryEndpointDeclaresAuthorizationTests
{
    private static IEnumerable<System.Reflection.MethodInfo> AccionesDeControladores() =>
        typeof(AuthController).Assembly.GetTypes()
            .Where(t => t.IsPublic && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName && m.DeclaringType is not null && typeof(ControllerBase).IsAssignableFrom(m.DeclaringType));

    private static bool DeclaraAutorizacion(System.Reflection.MethodInfo accion)
    {
        var enClase = accion.DeclaringType!.GetCustomAttributes(inherit: true);
        var enMetodo = accion.GetCustomAttributes(inherit: true);
        var todos = enClase.Concat(enMetodo);

        return todos.Any(a => a is AuthorizeAttribute) || todos.Any(a => a is AllowAnonymousAttribute);
    }

    public static IEnumerable<object[]> Acciones() =>
        AccionesDeControladores().Select(m => new object[] { m.DeclaringType!.Name, m.Name });

    [Theory]
    [MemberData(nameof(Acciones))]
    public void Cada_accion_declara_explicitamente_Authorize_o_AllowAnonymous(string controlador, string accion)
    {
        var metodo = AccionesDeControladores()
            .Single(m => m.DeclaringType!.Name == controlador && m.Name == accion);

        Assert.True(
            DeclaraAutorizacion(metodo),
            $"{controlador}.{accion} no declara [Authorize] ni [AllowAnonymous] — " +
            "por F1-04 (roadmap) ningún endpoint queda abierto por omisión.");
    }

    [Fact]
    public void Hay_al_menos_un_controlador_para_que_este_test_no_pase_en_falso()
    {
        Assert.NotEmpty(AccionesDeControladores());
    }
}
