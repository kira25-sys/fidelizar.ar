using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>S3 Ficha del socio, la vista completa de mostrador (FUNCTIONAL-SPEC §5).</summary>
public class FichaMostradorServiceTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private static readonly DateOnly Hoy = new(2026, 8, 19);

    private static FichaMostradorService CrearServicio(
        out FakeMiembroRepository miembroRepositorio,
        out FakeMovimientoRepository movimientoRepositorio,
        out FakeCorteRepository corteRepositorio)
    {
        miembroRepositorio = new FakeMiembroRepository();
        movimientoRepositorio = new FakeMovimientoRepository();
        corteRepositorio = new FakeCorteRepository();
        corteRepositorio.DeclararAsync(Corte.Declarar(NegocioId, new DateOnly(2026, 8, 15), 1, DateTime.UtcNow));

        return new FichaMostradorService(miembroRepositorio, movimientoRepositorio, new CorteService(corteRepositorio));
    }

    [Fact]
    public async Task Miembro_inexistente_lanza_EntityNotFoundException()
    {
        var servicio = CrearServicio(out _, out _, out _);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => servicio.ObtenerAsync(NegocioId, MiembroId, Hoy));
    }

    [Fact]
    public async Task Devuelve_nombre_numero_de_socio_saldo_y_corte()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var movimientoRepositorio, out _);
        miembroRepositorio.Sembrar(new Miembro
        {
            Id = MiembroId,
            NegocioId = NegocioId,
            Nombre = "Ana Gómez",
            NombreNormalizado = "ana gomez",
            NumeroSocio = "0142",
            FechaAlta = new DateOnly(2020, 1, 1),
        });
        await movimientoRepositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 12_400m, Hoy));

        var ficha = await servicio.ObtenerAsync(NegocioId, MiembroId, Hoy);

        Assert.Equal("Ana Gómez", ficha.Nombre);
        Assert.Equal("0142", ficha.NumeroSocio);
        Assert.Equal(12_400m, ficha.Saldo);
        Assert.Equal(new DateOnly(2026, 8, 15), ficha.CorteFecha);
    }

    /// <summary>RN-11: aviso desde 2 días antes.</summary>
    [Fact]
    public async Task Con_cumpleanos_dentro_de_2_dias_agrega_la_alerta()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _, out _);
        miembroRepositorio.Sembrar(new Miembro
        {
            Id = MiembroId,
            NegocioId = NegocioId,
            Nombre = "Ana Gómez",
            NombreNormalizado = "ana gomez",
            FechaNacimiento = new DateOnly(1990, 8, 20),
            FechaAlta = new DateOnly(2020, 1, 1),
        });

        var ficha = await servicio.ObtenerAsync(NegocioId, MiembroId, Hoy);

        var alerta = Assert.Single(ficha.Alertas);
        Assert.Equal(TipoAlertaMiembro.Cumpleanos, alerta.Tipo);
        Assert.Equal("Cumple el 20/8", alerta.Texto);
    }

    [Fact]
    public async Task Sin_cumpleanos_cercano_no_hay_alertas()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _, out _);
        miembroRepositorio.Sembrar(new Miembro
        {
            Id = MiembroId,
            NegocioId = NegocioId,
            Nombre = "Ana Gómez",
            NombreNormalizado = "ana gomez",
            FechaNacimiento = new DateOnly(1990, 1, 1),
            FechaAlta = new DateOnly(2020, 1, 1),
        });

        var ficha = await servicio.ObtenerAsync(NegocioId, MiembroId, Hoy);

        Assert.Empty(ficha.Alertas);
    }
}
