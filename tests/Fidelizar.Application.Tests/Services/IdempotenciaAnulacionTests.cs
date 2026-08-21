using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>
/// README decisión #6, extendida a S8 el 2026-08-21: un reintento de anulación con la misma
/// <c>ClaveIdempotencia</c> nunca escribe un segundo <c>Ajuste</c>. El caso real que evita: la
/// encargada aprieta "Anular movimiento", se corta la conexión, no ve respuesta, y aprieta de
/// nuevo — sin la clave eso movía la plata dos veces (I1/I3).
/// </summary>
public class IdempotenciaAnulacionTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private const int UsuarioId = 9;
    private static readonly DateOnly Hoy = new(2026, 8, 21);

    private static AnulacionMovimientoService CrearServicio(out FakeMovimientoRepository repositorio)
    {
        repositorio = new FakeMovimientoRepository();
        return new AnulacionMovimientoService(repositorio);
    }

    private static Task<MovimientoCredito> SembrarCanjeAsync(FakeMovimientoRepository repositorio, decimal monto = -300m) =>
        repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.Canje, monto, Hoy, motivo: "Canje"));

    /// <summary>El caso central: el reintento devuelve el Ajuste original y el saldo se corrige
    /// una sola vez.</summary>
    [Fact]
    public async Task Reintento_con_la_misma_clave_no_escribe_un_segundo_Ajuste()
    {
        var servicio = CrearServicio(out var repositorio);
        var original = await SembrarCanjeAsync(repositorio);

        var request = new AnularMovimientoRequest(
            NegocioId, original.Id, "Canje registrado por error", UsuarioId, Hoy, "clave-anulacion-1");

        var primero = await servicio.AnularAsync(request);
        var segundo = await servicio.AnularAsync(request); // el reintento, mismos datos

        Assert.Equal(primero.Id, segundo.Id);
        Assert.Equal(primero.Monto, segundo.Monto);
        Assert.Single(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Ajuste);
        Assert.Equal(0m, await repositorio.GetSaldoAsync(NegocioId, MiembroId));
    }

    /// <summary>La clave viaja hasta la fila: es lo que el índice único parcial va a ver.</summary>
    [Fact]
    public async Task El_Ajuste_queda_guardado_con_la_clave_del_intento()
    {
        var servicio = CrearServicio(out var repositorio);
        var original = await SembrarCanjeAsync(repositorio);

        var ajuste = await servicio.AnularAsync(new AnularMovimientoRequest(
            NegocioId, original.Id, "Corrección", UsuarioId, Hoy, "clave-anulacion-2"));

        Assert.Equal("clave-anulacion-2", ajuste.ClaveIdempotencia);
    }

    /// <summary>Una clave repetida con otro motivo no es un reintento — es un error del cliente.</summary>
    [Fact]
    public async Task Clave_repetida_con_otro_motivo_se_rechaza()
    {
        var servicio = CrearServicio(out var repositorio);
        var original = await SembrarCanjeAsync(repositorio);

        var primero = new AnularMovimientoRequest(
            NegocioId, original.Id, "Canje registrado por error", UsuarioId, Hoy, "clave-reusada");
        await servicio.AnularAsync(primero);

        var segundoConOtroMotivo = primero with { Motivo = "Otro motivo distinto" };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => servicio.AnularAsync(segundoConOtroMotivo));

        Assert.Equal("CLAVE_IDEMPOTENCIA_REUTILIZADA", ex.ErrorCode);
        Assert.Single(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Ajuste);
    }

    /// <summary>Y una clave repetida sobre un movimiento de otro monto, tampoco.</summary>
    [Fact]
    public async Task Clave_repetida_sobre_otro_movimiento_se_rechaza()
    {
        var servicio = CrearServicio(out var repositorio);
        var primerCanje = await SembrarCanjeAsync(repositorio, -300m);
        var segundoCanje = await SembrarCanjeAsync(repositorio, -500m);

        var primero = new AnularMovimientoRequest(
            NegocioId, primerCanje.Id, "Corrección", UsuarioId, Hoy, "clave-reusada-otro-movimiento");
        await servicio.AnularAsync(primero);

        var segundo = primero with { MovimientoId = segundoCanje.Id };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => servicio.AnularAsync(segundo));

        Assert.Equal("CLAVE_IDEMPOTENCIA_REUTILIZADA", ex.ErrorCode);
        Assert.Single(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Ajuste);
    }

    /// <summary>Sin clave no hay garantía: se rechaza antes de escribir nada.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Clave_de_idempotencia_vacia_se_rechaza(string clave)
    {
        var servicio = CrearServicio(out var repositorio);
        var original = await SembrarCanjeAsync(repositorio);

        var request = new AnularMovimientoRequest(
            NegocioId, original.Id, "Corrección", UsuarioId, Hoy, clave);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.AnularAsync(request));

        Assert.Equal("CLAVE_IDEMPOTENCIA_REQUERIDA", ex.ErrorCode);
        Assert.DoesNotContain(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Ajuste);
    }

    /// <summary>
    /// El caso que cierra el índice único de la base, no el chequeo previo: dos anulaciones con la
    /// misma clave pasan a la vez por "¿ya existe?" y las dos llegan a <c>AppendAsync</c>.
    /// <see cref="CarreraSimuladaMovimientoRepository"/> fuerza esa ventana. El servicio se tiene
    /// que recuperar leyendo al ganador, nunca escribir un segundo <c>Ajuste</c>.
    /// </summary>
    [Fact]
    public async Task Dos_anulaciones_concurrentes_con_la_misma_clave_nunca_escriben_dos_Ajustes()
    {
        var repositorioBase = new FakeMovimientoRepository();
        var original = await SembrarCanjeAsync(repositorioBase);
        var servicio = new AnulacionMovimientoService(new CarreraSimuladaMovimientoRepository(repositorioBase));

        var ajuste = await servicio.AnularAsync(new AnularMovimientoRequest(
            NegocioId, original.Id, "Corrección", UsuarioId, Hoy, "clave-concurrente-anulacion"));

        Assert.Equal(300m, ajuste.Monto);
        Assert.Single(repositorioBase.Movimientos, m => m.Tipo == TipoMovimientoCredito.Ajuste);
        Assert.Equal(0m, await repositorioBase.GetSaldoAsync(NegocioId, MiembroId));
    }

    /// <summary>Una anulación normal, sin reintento, sigue funcionando igual que antes.</summary>
    [Fact]
    public async Task Anulacion_normal_sin_reintento_escribe_un_solo_Ajuste()
    {
        var servicio = CrearServicio(out var repositorio);
        var original = await SembrarCanjeAsync(repositorio);

        var ajuste = await servicio.AnularAsync(new AnularMovimientoRequest(
            NegocioId, original.Id, "Corrección", UsuarioId, Hoy, "clave-de-un-solo-uso"));

        Assert.Equal(TipoMovimientoCredito.Ajuste, ajuste.Tipo);
        Assert.Equal(300m, ajuste.Monto);
        Assert.Single(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Ajuste);
    }
}
