using Fidelizar.Infrastructure.Security;

namespace Fidelizar.Infrastructure.Tests.Security;

/// <summary>
/// El ida y vuelta de la implementación real. Hasta el 2026-08-21 no existía: todo el resto de la
/// suite usa <c>FakePasswordHasher</c>, así que la única pieza que decide si una cajera puede
/// entrar nunca se había ejercitado. Las contraseñas de acá son inventadas (CLAUDE.md).
/// </summary>
public class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Theory]
    [InlineData("Fidelizar2026")]
    [InlineData("una contraseña con espacios")]
    [InlineData("acentos-ñÁÉÍ")]
    [InlineData("x")]
    public void Verifica_la_contrasena_que_acaba_de_hashear(string contrasena)
    {
        var hash = _hasher.Hash(contrasena);

        Assert.True(_hasher.Verify(hash, contrasena));
    }

    [Fact]
    public void Rechaza_una_contrasena_distinta()
    {
        var hash = _hasher.Hash("Fidelizar2026");

        Assert.False(_hasher.Verify(hash, "Fidelizar2027"));
        Assert.False(_hasher.Verify(hash, "fidelizar2026"));
        Assert.False(_hasher.Verify(hash, string.Empty));
    }

    /// <summary>
    /// El seeder hashea cada cuenta por separado, así que tres filas con la misma contraseña
    /// llevan tres hashes distintos — y las tres tienen que verificar igual.
    /// </summary>
    [Fact]
    public void Dos_hashes_de_la_misma_contrasena_son_distintos_y_ambos_verifican()
    {
        var primero = _hasher.Hash("Fidelizar2026");
        var segundo = _hasher.Hash("Fidelizar2026");

        Assert.NotEqual(primero, segundo);
        Assert.True(_hasher.Verify(primero, "Fidelizar2026"));
        Assert.True(_hasher.Verify(segundo, "Fidelizar2026"));
    }

    /// <summary>
    /// Un hash producido por otra instancia verifica igual: el seeder crea la suya con <c>new</c>
    /// y la API recibe la del contenedor de dependencias. Si esto fallara, ninguna cuenta sembrada
    /// podría entrar jamás.
    /// </summary>
    [Fact]
    public void Un_hash_de_otra_instancia_verifica_igual()
    {
        var delSeeder = new IdentityPasswordHasher().Hash("Fidelizar2026");

        Assert.True(new IdentityPasswordHasher().Verify(delSeeder, "Fidelizar2026"));
    }
}
