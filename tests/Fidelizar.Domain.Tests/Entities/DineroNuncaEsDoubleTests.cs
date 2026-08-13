using System.Reflection;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Tests.Entities;

/// <summary>
/// I4 — money is <c>decimal</c>. Never <c>double</c>, never <c>float</c>, on any entity. Verified
/// by reflection over every property of every type in <c>Fidelizar.Domain.Entities</c>, so a
/// future entity or a future property cannot silently opt out.
///
/// The other half of I4 — rounding happens in exactly one place, 2 decimals, AwayFromZero — is
/// already covered by <c>RedondeoTests</c> and is not duplicated here.
/// </summary>
public class DineroNuncaEsDoubleTests
{
    private static IEnumerable<Type> EntidadesDeDominio =>
        typeof(MovimientoCredito).Assembly
            .GetTypes()
            .Where(t => t.IsClass && t.Namespace == typeof(MovimientoCredito).Namespace);

    [Fact]
    public void Hay_al_menos_una_entidad_de_dominio_para_probar()
    {
        // Guards against this test silently passing "for free" if the namespace ever moves.
        Assert.NotEmpty(EntidadesDeDominio);
    }

    [Fact]
    public void Ninguna_propiedad_de_ninguna_entidad_de_dominio_es_double_ni_float()
    {
        var ofensores = EntidadesDeDominio
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => EsDoubleOFloat(p.PropertyType))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name} ({p.PropertyType.Name})")
            .ToArray();

        Assert.Empty(ofensores);
    }

    private static bool EsDoubleOFloat(Type tipo)
    {
        var tipoBase = Nullable.GetUnderlyingType(tipo) ?? tipo;
        return tipoBase == typeof(double) || tipoBase == typeof(float);
    }
}
