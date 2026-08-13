using Fidelizar.Domain.Texto;

namespace Fidelizar.Domain.Tests.Texto;

/// <summary>
/// Name normalisation has to survive the source system's format
/// (<c>"Apellido (VC X), Nombre"</c>) against a signup form's format
/// (<c>"Nombre Apellido"</c>). Ported from Octaviano's <c>VipNombresTests</c> (F0-05), full
/// coverage kept — see also <see cref="VipNombres"/>'s class remarks for why nothing here ever
/// decides two members are the same person (I7).
///
/// All names below are invented fixtures, never a real member's data (CLAUDE.md) — including the
/// original suite's own example, which its source comment flagged as taken from a real screenshot
/// and which is therefore replaced here rather than copied.
/// </summary>
public class VipNombresTests
{
    [Fact]
    public void Normalizar_SacaLaMarcaVipConSuLetraDeSucursal()
    {
        // Invented fixture, shaped like the source system's branch-letter mark.
        Assert.Equal("ficticio prueba", VipNombres.Normalizar("Ficticio (VC H), Prueba"));
    }

    [Theory]
    [InlineData("Prueba (VC), Ficticio", "prueba ficticio")]
    [InlineData("Prueba (VC RD), Ficticio", "prueba ficticio")]
    [InlineData("Prueba (vc h), Ficticio", "prueba ficticio")]
    public void Normalizar_ToleraLasVariantesDeLaMarca(string entrada, string esperado)
    {
        Assert.Equal(esperado, VipNombres.Normalizar(entrada));
    }

    [Fact]
    public void Normalizar_SacaAcentosYPasaAMinusculas()
    {
        Assert.Equal("maria ines ficticia", VipNombres.Normalizar("María Inés Ficticia"));
    }

    [Theory]
    [InlineData("Crédito", "credito")]
    [InlineData("CRÉDITO", "credito")]
    [InlineData("Teléfono", "telefono")]
    [InlineData("MUÑOZ", "munoz")]
    [InlineData("Ángel Peña", "angel pena")]
    public void Normalizar_SacaAcentosTambienEnMayusculas(string entrada, string esperado)
    {
        // Regression: the first version used String.Normalize(FormD), which under
        // InvariantGlobalization=true returns the string untouched. Tests still passed because
        // the test project does not carry that flag, while the real deployed binary failed to
        // recognise the "Crédito" column. The accent map is explicit now, so both modes agree.
        Assert.Equal(esperado, VipNombres.Normalizar(entrada));
    }

    [Fact]
    public void Normalizar_ColapsaEspaciosYPuntuacion()
    {
        Assert.Equal("juan perez", VipNombres.Normalizar("  Juan   Pérez.  "));
    }

    [Fact]
    public void Normalizar_TextoVacio_DevuelveVacio()
    {
        Assert.Equal(string.Empty, VipNombres.Normalizar(null));
        Assert.Equal(string.Empty, VipNombres.Normalizar("   "));
    }

    [Fact]
    public void ClaveComparable_IgnoraElOrdenApellidoNombre()
    {
        // This is the whole point: the source system stores "Apellido, Nombre" and a signup form
        // stores "Nombre Apellido". Comparing raw strings would never match them.
        var delSistema = VipNombres.ClaveComparable("Ficticia (VC H), Marta Inés");
        var delFormulario = VipNombres.ClaveComparable("Marta Inés Ficticia");

        Assert.Equal(delSistema, delFormulario);
    }

    [Fact]
    public void QuitarMarcaVip_SinMarca_DejaElNombreIntacto()
    {
        Assert.Equal("Juan Pérez", VipNombres.QuitarMarcaVip("Juan Pérez"));
    }

    // --- Additional cases not present in the original Octaviano suite (F0-05: cover the gaps) ---

    [Fact]
    public void ClaveComparable_TextoVacio_DevuelveVacio()
    {
        Assert.Equal(string.Empty, VipNombres.ClaveComparable(null));
    }

    [Fact]
    public void QuitarMarcaVip_ParentesisSinCerrar_CortaDesdeLaMarca()
    {
        // Malformed export row: the closing parenthesis is missing. Everything from "(VC" onward
        // is dropped rather than left dangling in the normalised name.
        Assert.Equal("Prueba ", VipNombres.QuitarMarcaVip("Prueba (VC H, Ficticio"));
    }

    [Fact]
    public void Normalizar_ConNumeros_LosConserva()
    {
        // A member name is never expected to carry digits, but the normaliser must not choke on
        // one if a data-entry mistake puts a number in the name column.
        Assert.Equal("juan 2", VipNombres.Normalizar("Juan 2"));
    }

    [Fact]
    public void ClaveComparable_MayusculasYMinusculasDanLaMismaClave()
    {
        Assert.Equal(VipNombres.ClaveComparable("JUAN PEREZ"), VipNombres.ClaveComparable("juan perez"));
    }
}
