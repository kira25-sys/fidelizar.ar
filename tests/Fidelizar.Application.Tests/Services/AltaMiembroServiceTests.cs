using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Persistence;
using Fidelizar.Domain.Repositories;

namespace Fidelizar.Application.Tests.Services;

/// <summary>
/// S5 Alta de socio (FUNCTIONAL-SPEC §7, I10). Invented fixtures throughout — no real member data
/// (CLAUDE.md).
/// </summary>
public class AltaMiembroServiceTests
{
    private const int NegocioId = 1;

    /// <summary>Un segundo negocio, solo para probar que el alta nunca lo cruza (I8).</summary>
    private const int OtroNegocioId = 2;
    private static readonly DateOnly Hoy = new(2026, 8, 19);

    private static AltaMiembroService CrearServicio(
        out FakeMiembroRepository miembroRepositorio,
        out FakeConsentimientoRepository consentimientoRepositorio,
        out FakeSucursalRepository sucursalRepositorio)
    {
        miembroRepositorio = new FakeMiembroRepository();
        consentimientoRepositorio = new FakeConsentimientoRepository();
        sucursalRepositorio = new FakeSucursalRepository();
        var consentimientoService = new ConsentimientoService(consentimientoRepositorio);

        return new AltaMiembroService(
            miembroRepositorio, sucursalRepositorio, consentimientoService, new FakeUnitOfWork());
    }

    private static AltaMiembroSolicitud Solicitud(
        bool consentimientoDatosPersonales = true,
        bool consentimientoDatosSensibles = false,
        string? clienteExternoId = null,
        int? sucursalId = null,
        int? dia = null,
        int? mes = null,
        int negocioId = NegocioId) =>
        new(
            negocioId, "Ana Gómez", clienteExternoId, "11-5555-5555", "30111222", dia, mes,
            sucursalId, consentimientoDatosPersonales, consentimientoDatosSensibles, UsuarioId: 3, Hoy);

    /// <summary>El caso central: el alta escribe socio y consentimiento juntos (I10).</summary>
    [Fact]
    public async Task El_alta_escribe_el_socio_y_el_consentimiento_de_datos_personales_juntos()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var consentimientoRepositorio, out _);

        var miembro = await servicio.DarDeAltaAsync(Solicitud());

        Assert.Single(miembroRepositorio.Miembros);
        Assert.Equal("Ana Gómez", miembro.Nombre);
        Assert.True(miembro.Activo);

        var consentimiento = Assert.Single(consentimientoRepositorio.Consentimientos);
        Assert.Equal(miembro.Id, consentimiento.MiembroId);
        Assert.Equal(TipoConsentimiento.DatosPersonales, consentimiento.Tipo);
        Assert.True(consentimiento.Otorgado);
        Assert.Equal(TextosConsentimiento.DatosPersonalesVersion, consentimiento.VersionTexto);
        Assert.Equal(CanalConsentimiento.Mostrador, consentimiento.Canal);
    }

    /// <summary>Si falla la escritura del consentimiento, no debe quedar el socio huérfano (I10)
    /// — ambas escrituras viven en la misma transacción.</summary>
    [Fact]
    public async Task Si_falla_la_escritura_del_consentimiento_no_queda_el_socio_huerfano()
    {
        var miembroRepositorio = new FakeMiembroRepository();
        var consentimientoRepositorioQueFalla = new ConsentimientoRepositoryQueFalla();
        var sucursalRepositorio = new FakeSucursalRepository();
        var consentimientoService = new ConsentimientoService(consentimientoRepositorioQueFalla);
        var unitOfWork = new UnitOfWorkQueDeshaceElMiembroSiFalla(miembroRepositorio);

        var servicio = new AltaMiembroService(miembroRepositorio, sucursalRepositorio, consentimientoService, unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.DarDeAltaAsync(Solicitud()));

        // La transacción tiene que haber deshecho el alta del socio — nada queda escrito.
        Assert.Empty(miembroRepositorio.Miembros);
    }

    /// <summary>El alta sin el checkbox obligatorio se rechaza, y nada llega a escribirse — ni
    /// siquiera el Miembro (I10).</summary>
    [Fact]
    public async Task El_alta_sin_consentimiento_de_datos_personales_se_rechaza_y_no_escribe_nada()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var consentimientoRepositorio, out _);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => servicio.DarDeAltaAsync(Solicitud(consentimientoDatosPersonales: false)));

        Assert.Equal("CONSENTIMIENTO_DATOS_PERSONALES_REQUERIDO", ex.ErrorCode);
        Assert.Empty(miembroRepositorio.Miembros);
        Assert.Empty(consentimientoRepositorio.Consentimientos);
    }

    /// <summary>El de DatosSensibles es opcional — el texto aprobado dice explícitamente que se
    /// puede ser socio sin darlo (decisión del dueño, 2026-08-19). El alta se acepta igual, y no
    /// se escribe ninguna fila de consentimiento de ese tipo (no es lo mismo "no preguntado" que
    /// "rechazado").</summary>
    [Fact]
    public async Task El_alta_sin_consentimiento_de_datos_sensibles_se_acepta()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var consentimientoRepositorio, out _);

        var miembro = await servicio.DarDeAltaAsync(Solicitud(consentimientoDatosSensibles: false));

        Assert.Single(miembroRepositorio.Miembros);
        Assert.DoesNotContain(consentimientoRepositorio.Consentimientos, c => c.Tipo == TipoConsentimiento.DatosSensibles);
    }

    /// <summary>Cuando sí se ofrece, el consentimiento de DatosSensibles se escribe con su propia
    /// versión de texto.</summary>
    [Fact]
    public async Task El_alta_con_consentimiento_de_datos_sensibles_lo_escribe_tambien()
    {
        var servicio = CrearServicio(out _, out var consentimientoRepositorio, out _);

        var miembro = await servicio.DarDeAltaAsync(Solicitud(consentimientoDatosSensibles: true));

        var consentimientoSensible = Assert.Single(
            consentimientoRepositorio.Consentimientos, c => c.Tipo == TipoConsentimiento.DatosSensibles);
        Assert.True(consentimientoSensible.Otorgado);
        Assert.Equal(TextosConsentimiento.DatosSensiblesVersion, consentimientoSensible.VersionTexto);
        Assert.Equal(miembro.Id, consentimientoSensible.MiembroId);
    }

    /// <summary>Revocar el consentimiento de DatosSensibles tiene que funcionar sin tocar la
    /// cuenta ni el saldo (decisión del dueño, 2026-08-19: el texto dice explícitamente que se
    /// puede revocar sin que afecte la cuenta ni los puntos). RevocarAsync solo toca
    /// IConsentimientoRepository — nunca IMiembroRepository ni el ledger.</summary>
    [Fact]
    public async Task Revocar_el_consentimiento_de_datos_sensibles_no_toca_el_miembro_ni_el_saldo()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var consentimientoRepositorio, out _);
        var consentimientoService = new ConsentimientoService(consentimientoRepositorio);
        var miembro = await servicio.DarDeAltaAsync(Solicitud(consentimientoDatosSensibles: true));
        var miembrosAntes = miembroRepositorio.Miembros.ToList();

        await consentimientoService.RevocarAsync(new RevocarConsentimientoRequest(
            NegocioId, miembro.Id, TipoConsentimiento.DatosSensibles, TextosConsentimiento.DatosSensiblesVersion,
            CanalConsentimiento.Mostrador, DateTime.UtcNow, RegistradoPorUsuarioId: 3));

        // El Miembro no cambió en absoluto — la revocación no lo tocó.
        Assert.Equal(miembrosAntes, miembroRepositorio.Miembros);
        Assert.True(miembro.Activo);

        var vigente = await consentimientoService.ObtenerVigenteAsync(NegocioId, miembro.Id, TipoConsentimiento.DatosSensibles);
        Assert.False(vigente!.Otorgado);
    }

    [Fact]
    public async Task El_alta_con_SucursalId_inexistente_se_rechaza()
    {
        var servicio = CrearServicio(out _, out _, out _);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => servicio.DarDeAltaAsync(Solicitud(sucursalId: 999)));

        Assert.Equal("SUCURSAL_INEXISTENTE", ex.ErrorCode);
    }

    [Fact]
    public async Task El_alta_con_ClienteExternoId_ya_vinculado_a_otro_socio_se_rechaza()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _, out _);
        miembroRepositorio.Sembrar(new Miembro
        {
            NegocioId = NegocioId,
            ClienteExternoId = "POS-001",
            Nombre = "Otro Socio",
            NombreNormalizado = "otro socio",
            FechaAlta = Hoy,
        });

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => servicio.DarDeAltaAsync(Solicitud(clienteExternoId: "POS-001")));

        Assert.Equal("CLIENTE_EXTERNO_ID_DUPLICADO", ex.ErrorCode);
    }

    [Fact]
    public async Task El_alta_con_dia_de_nacimiento_sin_mes_se_rechaza()
    {
        var servicio = CrearServicio(out _, out _, out _);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => servicio.DarDeAltaAsync(Solicitud(dia: 15, mes: null)));

        Assert.Equal("FECHA_NACIMIENTO_INCOMPLETA", ex.ErrorCode);
    }

    /// <summary>RN-11: solo importan día y mes — un cumpleaños 29 de febrero tiene que poder
    /// cargarse sin año real (el año que se usa internamente es un placeholder bisiesto).</summary>
    [Fact]
    public async Task El_alta_con_dia_y_mes_de_nacimiento_29_de_febrero_se_acepta()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _, out _);

        var miembro = await servicio.DarDeAltaAsync(Solicitud(dia: 29, mes: 2));

        Assert.Equal(2, miembro.FechaNacimiento!.Value.Month);
        Assert.Equal(29, miembro.FechaNacimiento.Value.Day);
    }

    [Fact]
    public async Task El_alta_con_SucursalId_existente_se_acepta_y_queda_registrada()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _, out var sucursalRepositorio);
        var sucursal = sucursalRepositorio.Sembrar(NegocioId);

        var miembro = await servicio.DarDeAltaAsync(Solicitud(sucursalId: sucursal.Id));

        Assert.Equal(sucursal.Id, miembro.SucursalId);
    }

    /// <summary>
    /// I8: el <c>NegocioId</c> de la solicitud queda estampado en el socio y en cada fila de
    /// consentimiento. No es una convención que alguien tenga que acordarse de respetar — es el
    /// único valor que el alta escribe, y viene del token, nunca del cuerpo del request.
    /// </summary>
    [Fact]
    public async Task El_alta_estampa_el_NegocioId_de_la_solicitud_en_el_socio_y_en_los_consentimientos()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var consentimientoRepositorio, out _);

        var miembro = await servicio.DarDeAltaAsync(
            Solicitud(consentimientoDatosSensibles: true, negocioId: OtroNegocioId));

        Assert.Equal(OtroNegocioId, miembro.NegocioId);
        Assert.Equal(2, consentimientoRepositorio.Consentimientos.Count);
        Assert.All(consentimientoRepositorio.Consentimientos, c => Assert.Equal(OtroNegocioId, c.NegocioId));
        Assert.All(consentimientoRepositorio.Consentimientos, c => Assert.Equal(miembro.Id, c.MiembroId));
        Assert.All(miembroRepositorio.Miembros, m => Assert.Equal(OtroNegocioId, m.NegocioId));
    }

    /// <summary>
    /// I8: una sucursal de otro negocio no existe para este alta. Vale la pena probarlo aparte de
    /// <c>SUCURSAL_INEXISTENTE</c> a secas: el id existe en la tabla, y lo único que lo hace
    /// inválido es el filtro por <c>NegocioId</c> — justo el <c>WHERE</c> que ARCHITECTURE §5 dice
    /// que no puede olvidarse.
    /// </summary>
    [Fact]
    public async Task El_alta_no_acepta_una_sucursal_de_otro_negocio()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _, out var sucursalRepositorio);
        var sucursalAjena = sucursalRepositorio.Sembrar(OtroNegocioId);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => servicio.DarDeAltaAsync(Solicitud(sucursalId: sucursalAjena.Id)));

        Assert.Equal("SUCURSAL_INEXISTENTE", ex.ErrorCode);
        Assert.Empty(miembroRepositorio.Miembros);
    }

    /// <summary>
    /// I8, el otro lado del mismo filtro: el mismo <c>ClienteExternoId</c> en otro negocio no es
    /// un choque. El índice único de DATA-MODEL §3 es <c>(NegocioId, ClienteExternoId)</c>, no
    /// <c>ClienteExternoId</c> solo — si esta prueba fallara, un socio de otro negocio estaría
    /// bloqueando un alta acá.
    /// </summary>
    [Fact]
    public async Task El_alta_no_choca_con_el_ClienteExternoId_de_otro_negocio()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var consentimientoRepositorio, out _);
        miembroRepositorio.Sembrar(new Miembro
        {
            Id = 90,
            NegocioId = OtroNegocioId,
            ClienteExternoId = "POS-001",
            Nombre = "Socia De Otro Negocio",
            NombreNormalizado = "socia de otro negocio",
            FechaAlta = Hoy,
        });

        var miembro = await servicio.DarDeAltaAsync(Solicitud(clienteExternoId: "POS-001"));

        Assert.Equal(NegocioId, miembro.NegocioId);
        Assert.Equal("POS-001", miembro.ClienteExternoId);
        var consentimiento = Assert.Single(consentimientoRepositorio.Consentimientos);
        Assert.Equal(NegocioId, consentimiento.NegocioId);
        Assert.Equal(miembro.Id, consentimiento.MiembroId);
    }

    /// <summary>Fake que simula una falla real de la base al escribir el consentimiento —
    /// exactamente el punto en el que una escritura a mitad de camino tendría que revertirse.
    /// </summary>
    private sealed class ConsentimientoRepositoryQueFalla : IConsentimientoRepository
    {
        public Task<Consentimiento?> GetVigenteAsync(
            int negocioId, int miembroId, TipoConsentimiento tipo, CancellationToken cancellationToken = default) =>
            Task.FromResult<Consentimiento?>(null);

        public Task<IReadOnlyList<Consentimiento>> GetHistorialAsync(
            int negocioId, int miembroId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Consentimiento>>([]);

        public Task<Consentimiento> AppendAsync(Consentimiento consentimiento, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Falla simulada de la base al escribir el consentimiento.");
    }

    /// <summary>
    /// Espeja lo que <c>Fidelizar.Infrastructure.Persistence.UnitOfWork</c> hace de verdad con una
    /// transacción real: si <paramref name="operacion"/> falla, deshace lo que
    /// <see cref="FakeMiembroRepository.AddAsync"/> escribió durante el intento — sin esto no hay
    /// forma de probar la atomicidad sin una base de datos real (ARCHITECTURE §11).
    /// </summary>
    private sealed class UnitOfWorkQueDeshaceElMiembroSiFalla(FakeMiembroRepository miembroRepositorio) : IUnitOfWork
    {
        public async Task EjecutarEnTransaccionAsync(
            Func<CancellationToken, Task> operacion, CancellationToken cancellationToken = default)
        {
            var cantidadAntes = miembroRepositorio.Miembros.Count;
            try
            {
                await operacion(cancellationToken);
            }
            catch
            {
                while (miembroRepositorio.Miembros.Count > cantidadAntes)
                {
                    miembroRepositorio.Quitar(miembroRepositorio.Miembros[^1]);
                }

                throw;
            }
        }
    }
}
