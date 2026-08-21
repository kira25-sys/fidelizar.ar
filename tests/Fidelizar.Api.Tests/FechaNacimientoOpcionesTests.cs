using Fidelizar.Client.Formatting;

namespace Fidelizar.Api.Tests;

/// <summary>
/// S5's birthday pair (RN-11). Lives here for the same reason
/// <see cref="MontoTecleadoParserTests"/> does: this project already references
/// <c>Fidelizar.Client</c>, and there is no client test project (ARCHITECTURE §3).
/// </summary>
public class FechaNacimientoOpcionesTests
{
    [Fact]
    public void Meses_TieneLosDoceEnOrden()
    {
        Assert.Equal(12, FechaNacimientoOpciones.Meses.Count);
        Assert.Equal(Enumerable.Range(1, 12), FechaNacimientoOpciones.Meses.Select(m => m.Numero));
        Assert.All(FechaNacimientoOpciones.Meses, m => Assert.False(string.IsNullOrWhiteSpace(m.Nombre)));
    }

    [Theory]
    [InlineData(1, 31)]
    [InlineData(3, 31)]
    [InlineData(4, 30)]
    [InlineData(6, 30)]
    [InlineData(9, 30)]
    [InlineData(11, 30)]
    [InlineData(12, 31)]
    public void DiasDelMes_DevuelveLosDiasDelMesComun(int mes, int esperado)
    {
        Assert.Equal(esperado, FechaNacimientoOpciones.DiasDelMes(mes));
    }

    /// <summary>
    /// AltaMiembroService stores the pair against the year 2000, which is bisiesto on purpose, so
    /// 29/02 is a valid birthday there and has to be offerable here.
    /// </summary>
    [Fact]
    public void DiasDelMes_FebreroLlegaA29()
    {
        Assert.Equal(29, FechaNacimientoOpciones.DiasDelMes(2));
    }

    /// <summary>With no month chosen the day may still be picked first.</summary>
    [Fact]
    public void DiasDelMes_SinMesOfrece31()
    {
        Assert.Equal(31, FechaNacimientoOpciones.DiasDelMes(null));
    }

    [Theory]
    [InlineData(31, 2, false)]
    [InlineData(30, 2, false)]
    [InlineData(29, 2, true)]
    [InlineData(31, 4, false)]
    [InlineData(30, 4, true)]
    [InlineData(31, 1, true)]
    public void DiaCabeEnMes_RechazaSoloLoQueElServidorRechazaria(int dia, int mes, bool esperado)
    {
        Assert.Equal(esperado, FechaNacimientoOpciones.DiaCabeEnMes(dia, mes));
    }

    [Fact]
    public void DiaCabeEnMes_SinDiaSiempreCabe()
    {
        Assert.True(FechaNacimientoOpciones.DiaCabeEnMes(null, 2));
        Assert.True(FechaNacimientoOpciones.DiaCabeEnMes(null, null));
    }

    /// <summary>
    /// The whole point of the pair: every day/month combination the select can produce is one
    /// <c>AltaMiembroService.ResolverFechaNacimiento</c> accepts, so FECHA_NACIMIENTO_INVALIDA is
    /// unreachable from this screen.
    /// </summary>
    [Fact]
    public void CadaCombinacionOfrecidaEsUnaFechaReal()
    {
        foreach (var mes in FechaNacimientoOpciones.Meses)
        {
            for (var dia = 1; dia <= FechaNacimientoOpciones.DiasDelMes(mes.Numero); dia++)
            {
                var fecha = new DateOnly(2000, mes.Numero, dia);
                Assert.Equal(dia, fecha.Day);
                Assert.Equal(mes.Numero, fecha.Month);
            }
        }
    }
}
