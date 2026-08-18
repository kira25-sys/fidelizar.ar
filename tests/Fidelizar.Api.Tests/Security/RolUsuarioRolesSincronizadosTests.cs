using System.Reflection;
using Fidelizar.Api.Security;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// <see cref="RolUsuario"/> (Domain) and <see cref="Roles"/> (Api) are two parallel lists that
/// nothing else keeps in sync — a role added to the enum and forgotten in <c>Roles.cs</c> would
/// go unnoticed until a live token carries a role no policy recognises. Walks
/// <c>Enum.GetNames&lt;RolUsuario&gt;()</c> and checks every one has a matching constant here,
/// with the one deliberate exception: <see cref="RolUsuario.Sistema"/> is never granted a policy
/// and never presented in a token (<c>AuthService</c> refuses to authenticate it), so it must stay
/// out of <see cref="Roles"/> and out of <see cref="Policies"/> on purpose.
/// </summary>
public class RolUsuarioRolesSincronizadosTests
{
    private static string[] ConstantesDeRoles() =>
        typeof(Roles)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

    [Fact]
    public void Cada_RolUsuario_de_persona_tiene_su_constante_en_Roles()
    {
        var constantes = ConstantesDeRoles();

        var rolesDePersona = Enum.GetNames<RolUsuario>()
            .Where(nombre => nombre != nameof(RolUsuario.Sistema));

        Assert.All(rolesDePersona, nombre => Assert.Contains(nombre, constantes));
    }

    [Fact]
    public void Sistema_no_aparece_en_Roles_ni_es_una_persona_con_politica()
    {
        var constantes = ConstantesDeRoles();

        Assert.DoesNotContain(nameof(RolUsuario.Sistema), constantes);
    }

    [Fact]
    public void Roles_no_tiene_constantes_de_mas_que_RolUsuario_no_conozca()
    {
        var constantes = ConstantesDeRoles();
        var nombresDelEnum = Enum.GetNames<RolUsuario>();

        Assert.All(constantes, constante => Assert.Contains(constante, nombresDelEnum));
    }
}
