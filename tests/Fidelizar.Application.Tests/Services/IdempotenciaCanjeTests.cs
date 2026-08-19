using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Services;

/// <summary>
/// README decisión #6 (2026-08-19): un reintento de canje con la misma <c>ClaveIdempotencia</c>
/// nunca produce un segundo <c>Canje</c>. El caso real que evita: la cajera aprieta "canjear",
/// se corta la conexión, no ve respuesta, y aprieta de nuevo con exactamente los mismos datos.
/// </summary>
public class IdempotenciaCanjeTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private static readonly DateOnly Hoy = new(2026, 8, 19);

    private static SaldoService CrearServicio(out FakeMovimientoRepository repositorio)
    {
        repositorio = new FakeMovimientoRepository();
        var miembroRepositorio = new FakeMiembroRepository();
        miembroRepositorio.SembrarNuevo(NegocioId, MiembroId);
        return new SaldoService(repositorio, miembroRepositorio);
    }

    /// <summary>El caso central: la cajera reintenta con la misma clave tras no ver respuesta.
    /// El segundo POST no debe escribir un segundo movimiento, y el saldo se descuenta una sola
    /// vez.</summary>
    [Fact]
    public async Task Reintento_con_la_misma_clave_no_duplica_el_movimiento_ni_el_descuento()
    {
        var servicio = CrearServicio(out var repositorio);
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 10_000m, Hoy));

        var request = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 5_000m, "Compra de mercadería", UsuarioId: 3, Hoy, Hoy,
            ClaveIdempotencia: "clave-reintento-1");

        var primero = await servicio.RegistrarCanjeAsync(request);
        var segundo = await servicio.RegistrarCanjeAsync(request); // el reintento, mismos datos

        Assert.Equal(primero.Id, segundo.Id);
        Assert.Equal(primero.SaldoResultante, segundo.SaldoResultante);
        Assert.Single(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Canje);
        Assert.Equal(5_000m, 10_000m - await servicio.ObtenerSaldoAsync(NegocioId, MiembroId));
    }

    /// <summary>Confirma que el reintento devuelve exactamente el mismo resultado que la cajera
    /// vio (o no vio) la primera vez — indistinguible de que salió bien de entrada.</summary>
    [Fact]
    public async Task Reintento_devuelve_el_mismo_CanjeResponse_logico_que_el_intento_original()
    {
        var servicio = CrearServicio(out var repositorio);
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 10_000m, Hoy));

        var request = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 5_000m, "Compra de mercadería", UsuarioId: 3, Hoy, Hoy,
            ClaveIdempotencia: "clave-reintento-2");

        var primero = await servicio.RegistrarCanjeAsync(request);
        var segundo = await servicio.RegistrarCanjeAsync(request);

        Assert.Equal(primero.Monto, segundo.Monto);
        Assert.Equal(primero.FechaEfectiva, segundo.FechaEfectiva);
        Assert.Equal(primero.SaldoResultante, segundo.SaldoResultante);
    }

    /// <summary>Una clave repetida con otro monto no es un reintento — es un error del cliente.</summary>
    [Fact]
    public async Task Clave_repetida_con_otro_monto_se_rechaza()
    {
        var servicio = CrearServicio(out var repositorio);
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 10_000m, Hoy));

        var primero = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 5_000m, "Compra de mercadería", UsuarioId: 3, Hoy, Hoy,
            ClaveIdempotencia: "clave-reusada");
        await servicio.RegistrarCanjeAsync(primero);

        var segundoConOtroMonto = primero with { Monto = 1_000m };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => servicio.RegistrarCanjeAsync(segundoConOtroMonto));

        Assert.Equal("CLAVE_IDEMPOTENCIA_REUTILIZADA", ex.ErrorCode);
        Assert.Single(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Canje);
    }

    /// <summary>Una clave repetida con otro socio tampoco es un reintento.</summary>
    [Fact]
    public async Task Clave_repetida_con_otro_miembro_se_rechaza()
    {
        var servicio = CrearServicio(out var repositorio);
        var miembroRepositorio = new FakeMiembroRepository();
        miembroRepositorio.SembrarNuevo(NegocioId, MiembroId);
        miembroRepositorio.SembrarNuevo(NegocioId, MiembroId + 1);
        servicio = new SaldoService(repositorio, miembroRepositorio);

        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 10_000m, Hoy));
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId + 1, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 10_000m, Hoy));

        var primero = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 5_000m, "Compra de mercadería", UsuarioId: 3, Hoy, Hoy,
            ClaveIdempotencia: "clave-reusada-otro-socio");
        await servicio.RegistrarCanjeAsync(primero);

        var segundoConOtroMiembro = primero with { MiembroId = MiembroId + 1 };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => servicio.RegistrarCanjeAsync(segundoConOtroMiembro));

        Assert.Equal("CLAVE_IDEMPOTENCIA_REUTILIZADA", ex.ErrorCode);
    }

    /// <summary>Una clave vacía o ausente no vale — la garantía existe solo cuando el cliente la
    /// manda.</summary>
    [Fact]
    public async Task Clave_de_idempotencia_vacia_se_rechaza()
    {
        var servicio = CrearServicio(out _);

        var request = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 5_000m, "Compra de mercadería", UsuarioId: 3, Hoy, Hoy,
            ClaveIdempotencia: "");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.RegistrarCanjeAsync(request));
        Assert.Equal("CLAVE_IDEMPOTENCIA_REQUERIDA", ex.ErrorCode);
    }

    /// <summary>
    /// El caso que el índice único de la base, no el código, tiene que cerrar: dos requests con la
    /// misma clave pasan el chequeo "¿ya existe?" a la vez — ninguno ve todavía el movimiento del
    /// otro — y ambos llegan a <c>AppendAsync</c>. <see cref="CarreraSimuladaMovimientoRepository"/>
    /// hace exactamente eso: cuando el servicio intenta insertar, un "competidor" ya insertó bajo
    /// la misma clave un instante antes, tal como haría el índice único parcial de Postgres al
    /// rechazar la segunda fila. El servicio tiene que recuperarse leyendo al ganador, nunca
    /// devolver un error ni escribir una segunda fila.
    /// </summary>
    [Fact]
    public async Task Dos_intentos_concurrentes_con_la_misma_clave_nunca_escriben_dos_Canjes()
    {
        var repositorioBase = new FakeMovimientoRepository();
        var repositorio = new CarreraSimuladaMovimientoRepository(repositorioBase);
        var miembroRepositorio = new FakeMiembroRepository();
        miembroRepositorio.SembrarNuevo(NegocioId, MiembroId);
        var servicio = new SaldoService(repositorio, miembroRepositorio);

        await repositorioBase.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 10_000m, Hoy));

        var request = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 5_000m, "Compra de mercadería", UsuarioId: 3, Hoy, Hoy,
            ClaveIdempotencia: "clave-concurrente");

        var resultado = await servicio.RegistrarCanjeAsync(request);

        Assert.Equal(-5_000m, resultado.Monto);
        Assert.Single(repositorioBase.Movimientos, m => m.Tipo == TipoMovimientoCredito.Canje);
        Assert.Equal(5_000m, 10_000m - await servicio.ObtenerSaldoAsync(NegocioId, MiembroId));
    }

    /// <summary>Un canje normal, sin reintento, sigue funcionando exactamente igual que antes.</summary>
    [Fact]
    public async Task Canje_normal_sin_reintento_se_registra_una_sola_vez()
    {
        var servicio = CrearServicio(out var repositorio);
        await repositorio.AppendAsync(MovimientoCredito.Crear(
            NegocioId, MiembroId, Hoy, DateTime.UtcNow, TipoMovimientoCredito.SaldoInicial, 1_000m, Hoy));

        var request = new RegistrarCanjeRequest(
            NegocioId, MiembroId, 300m, "Canje normal", UsuarioId: 7, Hoy, Hoy,
            ClaveIdempotencia: "clave-unica-de-un-solo-uso");

        var movimiento = await servicio.RegistrarCanjeAsync(request);

        Assert.Equal(-300m, movimiento.Monto);
        Assert.Equal("clave-unica-de-un-solo-uso", movimiento.ClaveIdempotencia);
        Assert.Equal(700m, await servicio.ObtenerSaldoAsync(NegocioId, MiembroId));
        Assert.Single(repositorio.Movimientos, m => m.Tipo == TipoMovimientoCredito.Canje);
    }
}

/// <summary>
/// Decorates a real <see cref="FakeMovimientoRepository"/> to force the exact race window a real
/// unique-index violation closes: on the first <see cref="AppendAsync"/> call for a given
/// <c>ClaveIdempotencia</c>, a "competitor" inserts under that same key first — as if another
/// request's transaction had committed a moment earlier — so this call's own insert collides,
/// exactly like Postgres' unique partial index would reject it. <c>FakeMovimientoRepository</c>
/// already throws <see cref="ConflictException"/> in that situation (it mirrors
/// <c>MovimientoRepository.AppendAsync</c>'s contract), so this decorator only needs to create the
/// collision, not simulate the exception itself.
/// </summary>
internal sealed class CarreraSimuladaMovimientoRepository(FakeMovimientoRepository interno) : IMovimientoRepository
{
    private bool _yaSimuloLaCarrera;

    public Task<decimal> GetSaldoAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        interno.GetSaldoAsync(negocioId, miembroId, cancellationToken);

    public Task<IReadOnlyList<MovimientoCredito>> GetPorMiembroAsync(
        int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        interno.GetPorMiembroAsync(negocioId, miembroId, cancellationToken);

    public Task<IReadOnlyList<MovimientoCredito>> GetPorPeriodoAsync(
        int negocioId, string periodo, CancellationToken cancellationToken = default) =>
        interno.GetPorPeriodoAsync(negocioId, periodo, cancellationToken);

    public Task<bool> TieneMovimientosAsync(int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
        interno.TieneMovimientosAsync(negocioId, miembroId, cancellationToken);

    public Task<MovimientoCredito?> GetByIdAsync(int negocioId, long id, CancellationToken cancellationToken = default) =>
        interno.GetByIdAsync(negocioId, id, cancellationToken);

    public Task<MovimientoCredito?> GetPorClaveIdempotenciaAsync(
        int negocioId, string claveIdempotencia, CancellationToken cancellationToken = default) =>
        interno.GetPorClaveIdempotenciaAsync(negocioId, claveIdempotencia, cancellationToken);

    public Task<IReadOnlyList<MovimientoCredito>> GetPorFechaEfectivaYTipoAsync(
        int negocioId, DateOnly fechaEfectiva, TipoMovimientoCredito tipo, CancellationToken cancellationToken = default) =>
        interno.GetPorFechaEfectivaYTipoAsync(negocioId, fechaEfectiva, tipo, cancellationToken);

    public async Task<MovimientoCredito> AppendAsync(
        MovimientoCredito movimiento, CancellationToken cancellationToken = default)
    {
        if (!_yaSimuloLaCarrera && movimiento.ClaveIdempotencia is not null)
        {
            _yaSimuloLaCarrera = true;

            // El "competidor" gana la carrera: inserta primero, con los mismos datos lógicos que
            // este intento (mismo socio, monto, fecha, motivo) — exactamente lo que pasaría si dos
            // POST con la misma clave llegaran a la base casi al mismo tiempo.
            var competidor = MovimientoCredito.Crear(
                movimiento.NegocioId, movimiento.MiembroId, movimiento.FechaEfectiva, DateTime.UtcNow,
                movimiento.Tipo, movimiento.Monto, movimiento.FechaEfectiva, movimiento.UsuarioId,
                movimiento.Motivo, claveIdempotencia: movimiento.ClaveIdempotencia);
            await interno.AppendAsync(competidor, cancellationToken);
        }

        // Esta llamada choca contra el competidor que ya insertó bajo la misma clave — la misma
        // ConflictException que lanzaría MovimientoRepository ante la violación del índice único.
        return await interno.AppendAsync(movimiento, cancellationToken);
    }
}
