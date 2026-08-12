using System.Reflection;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Domain.Tests.Repositories;

/// <summary>
/// I1 says the ledger is append-only and ARCHITECTURE §3 forbids a generic repository precisely
/// because a generic <c>Delete&lt;T&gt;</c> would make that unenforceable. This asserts the
/// negative by reflection over every repository interface in <see cref="Fidelizar.Domain"/>, so
/// the guarantee survives even if a future repository is added and someone forgets this rule.
/// </summary>
public class RepositoryInterfacesTests
{
    private static readonly string[] ForbiddenNameFragments = ["Delete", "Remove", "Update", "Edit"];

    private static IEnumerable<Type> RepositoryInterfaces =>
        typeof(IMovimientoRepository).Assembly
            .GetTypes()
            .Where(t => t.IsInterface && t.Namespace == typeof(IMovimientoRepository).Namespace);

    [Fact]
    public void Hay_al_menos_un_repositorio_para_probar()
    {
        // Guards against this test silently passing "for free" if the namespace ever moves.
        Assert.NotEmpty(RepositoryInterfaces);
    }

    [Fact]
    public void Ningun_repositorio_expone_borrado_o_actualizacion()
    {
        var ofensores = RepositoryInterfaces
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => ForbiddenNameFragments.Any(fragmento =>
                m.Name.Contains(fragmento, StringComparison.OrdinalIgnoreCase)))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToArray();

        Assert.Empty(ofensores);
    }

    [Fact]
    public void IMovimientoRepository_no_tiene_metodo_de_borrado_ni_de_actualizacion()
    {
        // Belt-and-suspenders on the ledger specifically (F0-06's explicit requirement),
        // independent of the naming-fragment scan above.
        var nombres = typeof(IMovimientoRepository)
            .GetMethods()
            .Select(m => m.Name)
            .ToArray();

        Assert.DoesNotContain("Delete", nombres);
        Assert.DoesNotContain("Remove", nombres);
        Assert.DoesNotContain("Update", nombres);
        Assert.Contains("AppendAsync", nombres);
    }
}
