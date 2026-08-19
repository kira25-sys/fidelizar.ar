using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>
/// <see cref="ConsentimientoService"/>: grant, withdraw, query, and I10's gate wired through a
/// repository (<c>FakeConsentimientoRepository</c>, no database — ARCHITECTURE §11).
/// </summary>
public class ConsentimientoServiceTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private static readonly DateTime Ahora = new(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc);

    private static ConsentimientoService CrearServicio(out FakeConsentimientoRepository repositorio)
    {
        repositorio = new FakeConsentimientoRepository();
        return new ConsentimientoService(repositorio);
    }

    private static OtorgarConsentimientoRequest Otorgar(TipoConsentimiento tipo, DateTime? ocurridoEn = null) =>
        new(NegocioId, MiembroId, tipo, "v1", CanalConsentimiento.Mostrador, ocurridoEn ?? Ahora, RegistradoPorUsuarioId: 7);

    private static RevocarConsentimientoRequest Revocar(TipoConsentimiento tipo, DateTime? ocurridoEn = null) =>
        new(NegocioId, MiembroId, tipo, "v1", CanalConsentimiento.Mostrador, ocurridoEn ?? Ahora, RegistradoPorUsuarioId: 7);

    [Fact]
    public async Task Otorgar_agrega_una_fila_con_Otorgado_true()
    {
        var servicio = CrearServicio(out var repositorio);

        var consentimiento = await servicio.OtorgarAsync(Otorgar(TipoConsentimiento.DatosSensibles));

        Assert.True(consentimiento.Otorgado);
        Assert.Single(repositorio.Consentimientos);
        Assert.True(await servicio.EstaVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles));
    }

    /// <summary>Append-only: revocar nunca edita la fila que otorgó, agrega una nueva.</summary>
    [Fact]
    public async Task Revocar_agrega_una_fila_nueva_y_no_toca_la_anterior()
    {
        var servicio = CrearServicio(out var repositorio);

        await servicio.OtorgarAsync(Otorgar(TipoConsentimiento.DatosSensibles, Ahora));
        await servicio.RevocarAsync(Revocar(TipoConsentimiento.DatosSensibles, Ahora.AddMinutes(1)));

        Assert.Equal(2, repositorio.Consentimientos.Count);
        Assert.True(repositorio.Consentimientos[0].Otorgado);
        Assert.False(repositorio.Consentimientos[1].Otorgado);
        Assert.False(await servicio.EstaVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles));
    }

    /// <summary>Un nuevo otorgamiento posterior a una revocación vuelve a habilitar la escritura —
    /// consistente con "la vigencia es siempre la fila más nueva".</summary>
    [Fact]
    public async Task Un_otorgamiento_posterior_a_una_revocacion_vuelve_a_habilitar_la_escritura()
    {
        var servicio = CrearServicio(out _);

        await servicio.OtorgarAsync(Otorgar(TipoConsentimiento.DatosSensibles, Ahora));
        await servicio.RevocarAsync(Revocar(TipoConsentimiento.DatosSensibles, Ahora.AddMinutes(1)));
        await servicio.OtorgarAsync(Otorgar(TipoConsentimiento.DatosSensibles, Ahora.AddMinutes(2)));

        Assert.True(await servicio.EstaVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles));
        await servicio.RequerirVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles);
    }

    /// <summary>I10 — negative case 1: sin ningún consentimiento registrado para este socio.</summary>
    [Fact]
    public async Task RequerirVigente_sin_consentimiento_rechaza()
    {
        var servicio = CrearServicio(out _);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            servicio.RequerirVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles));

        Assert.Equal("CONSENTIMIENTO_REQUERIDO", ex.ErrorCode);
    }

    /// <summary>I10 — negative case 2: el consentimiento vigente de ese tipo fue revocado.</summary>
    [Fact]
    public async Task RequerirVigente_con_consentimiento_revocado_rechaza()
    {
        var servicio = CrearServicio(out _);
        await servicio.OtorgarAsync(Otorgar(TipoConsentimiento.DatosSensibles, Ahora));
        await servicio.RevocarAsync(Revocar(TipoConsentimiento.DatosSensibles, Ahora.AddMinutes(1)));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            servicio.RequerirVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles));

        Assert.Equal("CONSENTIMIENTO_REQUERIDO", ex.ErrorCode);
    }

    /// <summary>I10 — negative case 3: hay consentimiento otorgado, pero de otro tipo (datos
    /// personales en vez de datos sensibles).</summary>
    [Fact]
    public async Task RequerirVigente_con_consentimiento_de_otro_tipo_rechaza()
    {
        var servicio = CrearServicio(out _);
        await servicio.OtorgarAsync(Otorgar(TipoConsentimiento.DatosPersonales));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            servicio.RequerirVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles));

        Assert.Equal("CONSENTIMIENTO_REQUERIDO", ex.ErrorCode);
    }

    /// <summary>Caso positivo: consentimiento otorgado del tipo correcto no lanza.</summary>
    [Fact]
    public async Task RequerirVigente_con_consentimiento_otorgado_del_tipo_correcto_no_lanza()
    {
        var servicio = CrearServicio(out _);
        await servicio.OtorgarAsync(Otorgar(TipoConsentimiento.DatosSensibles));

        await servicio.RequerirVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles);
    }

    /// <summary>I8 — un consentimiento de otro negocio nunca satisface a este, ni siquiera con el
    /// mismo MiembroId numérico.</summary>
    [Fact]
    public async Task RequerirVigente_filtra_por_NegocioId()
    {
        var servicio = CrearServicio(out var repositorio);
        await repositorio.AppendAsync(Consentimiento.Registrar(
            negocioId: 999, MiembroId, TipoConsentimiento.DatosSensibles, otorgado: true,
            "v1", CanalConsentimiento.Mostrador, Ahora));

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            servicio.RequerirVigenteAsync(NegocioId, MiembroId, TipoConsentimiento.DatosSensibles));

        Assert.Equal("CONSENTIMIENTO_REQUERIDO", ex.ErrorCode);
    }
}
