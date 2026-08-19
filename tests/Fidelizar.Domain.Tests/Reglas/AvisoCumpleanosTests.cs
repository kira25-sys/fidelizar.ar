using Fidelizar.Domain.Reglas;

namespace Fidelizar.Domain.Tests.Reglas;

/// <summary>RN-11 — birthday notice starts 2 days before; only day and month matter, the year is
/// ignored even when present.</summary>
public class AvisoCumpleanosTests
{
    [Fact]
    public void Sin_fecha_de_nacimiento_no_avisa()
    {
        Assert.False(AvisoCumpleanos.DebeAvisar(null, new DateOnly(2026, 9, 4)));
    }

    [Fact]
    public void El_dia_del_cumpleanos_avisa()
    {
        var nacimiento = new DateOnly(1990, 9, 4);
        Assert.True(AvisoCumpleanos.DebeAvisar(nacimiento, new DateOnly(2026, 9, 4)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Hasta_2_dias_antes_avisa(int diasAntes)
    {
        var nacimiento = new DateOnly(1990, 9, 4);
        var hoy = new DateOnly(2026, 9, 4).AddDays(-diasAntes);

        Assert.True(AvisoCumpleanos.DebeAvisar(nacimiento, hoy));
    }

    [Fact]
    public void Tres_dias_antes_todavia_no_avisa()
    {
        var nacimiento = new DateOnly(1990, 9, 4);
        Assert.False(AvisoCumpleanos.DebeAvisar(nacimiento, new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void Un_dia_despues_ya_no_avisa()
    {
        var nacimiento = new DateOnly(1990, 9, 4);
        Assert.False(AvisoCumpleanos.DebeAvisar(nacimiento, new DateOnly(2026, 9, 5)));
    }

    [Fact]
    public void El_anio_de_nacimiento_se_ignora()
    {
        // Nacida en 1950, el aviso igual dispara para un "hoy" de 2026 — solo día y mes importan.
        var nacimiento = new DateOnly(1950, 9, 4);
        Assert.True(AvisoCumpleanos.DebeAvisar(nacimiento, new DateOnly(2026, 9, 3)));
    }

    [Fact]
    public void Cumpleanos_a_fin_de_anio_cruza_el_31_de_diciembre()
    {
        var nacimiento = new DateOnly(1990, 1, 1);
        Assert.True(AvisoCumpleanos.DebeAvisar(nacimiento, new DateOnly(2026, 12, 30)));
        Assert.True(AvisoCumpleanos.DebeAvisar(nacimiento, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Nacido_un_29_de_febrero_avisa_en_un_anio_no_bisiesto_el_28()
    {
        var nacimiento = new DateOnly(1996, 2, 29);

        // 2026 no es bisiesto: la ocurrencia cae el 28/2.
        Assert.True(AvisoCumpleanos.DebeAvisar(nacimiento, new DateOnly(2026, 2, 28)));
        Assert.False(AvisoCumpleanos.DebeAvisar(nacimiento, new DateOnly(2026, 3, 1)));
    }
}
