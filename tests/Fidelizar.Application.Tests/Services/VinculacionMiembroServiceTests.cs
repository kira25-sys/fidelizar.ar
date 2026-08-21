using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Texto;

namespace Fidelizar.Application.Tests.Services;

/// <summary>
/// F1-14 "Socios sin vincular" (ROADMAP; ARCHITECTURE §13 R1 — an unidentified member produces
/// no data at all). Runs with no database (ARCHITECTURE §11). Every member here is invented
/// (CLAUDE.md).
/// </summary>
public class VinculacionMiembroServiceTests
{
    private const int NegocioId = 7;
    private const int OtroNegocioId = 9;
    private const int UsuarioId = 3;

    private static (VinculacionMiembroService Servicio,
        FakeMiembroRepository Miembros,
        FakeRegistroAuditoriaRepository Auditoria) Crear()
    {
        var miembros = new FakeMiembroRepository();
        var auditoria = new FakeRegistroAuditoriaRepository();
        var servicio = new VinculacionMiembroService(miembros, auditoria, new FakeUnitOfWork());
        return (servicio, miembros, auditoria);
    }

    private static Miembro Sembrar(
        FakeMiembroRepository miembros,
        int id,
        string nombre,
        string? clienteExternoId = null,
        int negocioId = NegocioId,
        DateOnly? fechaAlta = null)
    {
        var miembro = new Miembro
        {
            Id = id,
            NegocioId = negocioId,
            ClienteExternoId = clienteExternoId,
            Nombre = nombre,
            NombreNormalizado = VipNombres.Normalizar(nombre),
            FechaAlta = fechaAlta ?? new DateOnly(2026, 1, 1),
        };

        miembros.Sembrar(miembro);
        return miembro;
    }

    [Fact]
    public async Task ListarSinVincular_solo_devuelve_los_que_no_tienen_ClienteExternoId()
    {
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia Sin Vincular Uno");
        Sembrar(miembros, 2, "Socio Ya Vinculado", clienteExternoId: "POS-100");
        Sembrar(miembros, 3, "Socia Sin Vincular Dos");

        var resultado = await servicio.ListarSinVincularAsync(NegocioId, CancellationToken.None);

        Assert.Equal([1, 3], resultado.Select(m => m.Id));
    }

    [Fact]
    public async Task ListarSinVincular_filtra_por_NegocioId()
    {
        // I8: another business's unlinked members are not this business's work queue.
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia De Este Negocio");
        Sembrar(miembros, 2, "Socia De Otro Negocio", negocioId: OtroNegocioId);

        var resultado = await servicio.ListarSinVincularAsync(NegocioId, CancellationToken.None);

        Assert.Equal([1], resultado.Select(m => m.Id));
    }

    [Fact]
    public async Task ListarSinVincular_ordena_por_FechaAlta_ascendente()
    {
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia Reciente", fechaAlta: new DateOnly(2026, 8, 1));
        Sembrar(miembros, 2, "Socia Antigua", fechaAlta: new DateOnly(2026, 2, 1));

        var resultado = await servicio.ListarSinVincularAsync(NegocioId, CancellationToken.None);

        Assert.Equal([2, 1], resultado.Select(m => m.Id));
    }

    [Fact]
    public async Task Vincular_escribe_el_ClienteExternoId_en_el_miembro()
    {
        var (servicio, miembros, _) = Crear();
        var miembro = Sembrar(miembros, 1, "Socia De Mostrador");

        var resultado = await servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-100", UsuarioId), CancellationToken.None);

        Assert.Equal("POS-100", resultado.ClienteExternoId);
        Assert.Equal("POS-100", miembro.ClienteExternoId);
    }

    [Fact]
    public async Task Vincular_recorta_los_espacios_del_id_antes_de_escribirlo()
    {
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia De Mostrador");

        var resultado = await servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "  POS-100  ", UsuarioId), CancellationToken.None);

        Assert.Equal("POS-100", resultado.ClienteExternoId);
        Assert.Equal("POS-100", miembros.UltimoClienteExternoIdVinculado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Vincular_rechaza_un_ClienteExternoId_en_blanco(string? clienteExternoId)
    {
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia De Mostrador");

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, clienteExternoId, UsuarioId),
            CancellationToken.None));

        Assert.Equal("CLIENTE_EXTERNO_ID_REQUERIDO", ex.ErrorCode);
    }

    [Fact]
    public async Task Vincular_un_miembro_inexistente_da_404()
    {
        var (servicio, _, _) = Crear();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 999, "POS-100", UsuarioId), CancellationToken.None));
    }

    [Fact]
    public async Task Vincular_un_miembro_de_otro_negocio_responde_igual_que_uno_inexistente()
    {
        // I8: the answer never reveals that the id is real in another business.
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia De Otro Negocio", negocioId: OtroNegocioId);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-100", UsuarioId), CancellationToken.None));
    }

    [Fact]
    public async Task Vincular_un_miembro_de_otro_negocio_no_lo_toca()
    {
        var (servicio, miembros, _) = Crear();
        var ajeno = Sembrar(miembros, 1, "Socia De Otro Negocio", negocioId: OtroNegocioId);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-100", UsuarioId), CancellationToken.None));

        Assert.Null(ajeno.ClienteExternoId);
    }

    [Fact]
    public async Task Vincular_un_miembro_ya_vinculado_da_409()
    {
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia Ya Vinculada", clienteExternoId: "POS-100");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-200", UsuarioId), CancellationToken.None));

        Assert.Equal("MIEMBRO_YA_VINCULADO", ex.ErrorCode);
    }

    [Fact]
    public async Task Vincular_un_id_ya_usado_por_otro_socio_del_negocio_da_409()
    {
        // DATA-MODEL §3: the partial unique index on (NegocioId, ClienteExternoId) is what makes
        // this impossible at the database; the service reports it with a usable message first.
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia De Mostrador");
        Sembrar(miembros, 2, "Socio Ya Vinculado", clienteExternoId: "POS-100");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-100", UsuarioId), CancellationToken.None));

        Assert.Equal("CLIENTE_EXTERNO_ID_DUPLICADO", ex.ErrorCode);
    }

    [Fact]
    public async Task Vincular_un_id_usado_en_otro_negocio_no_bloquea_a_este()
    {
        // I8 again, from the other side: uniqueness is per business, not global.
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia De Este Negocio");
        Sembrar(miembros, 2, "Socia De Otro Negocio", clienteExternoId: "POS-100", negocioId: OtroNegocioId);

        var resultado = await servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-100", UsuarioId), CancellationToken.None);

        Assert.Equal("POS-100", resultado.ClienteExternoId);
    }

    [Fact]
    public async Task Vincular_que_pierde_la_carrera_contra_otro_pedido_da_409()
    {
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia De Mostrador");
        miembros.SimularVinculacionPerdida = true;

        var ex = await Assert.ThrowsAsync<ConflictException>(() => servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-100", UsuarioId), CancellationToken.None));

        Assert.Equal("MIEMBRO_YA_VINCULADO", ex.ErrorCode);
    }

    [Fact]
    public async Task Vincular_audita_quien_lo_hizo()
    {
        // DATA-MODEL §2: linking decides whose future purchases accrue, so the actor is recorded.
        var (servicio, miembros, auditoria) = Crear();
        Sembrar(miembros, 1, "Socia De Mostrador");

        await servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-100", UsuarioId), CancellationToken.None);

        var registro = Assert.Single(auditoria.Registros);
        Assert.Equal("VincularClienteExterno", registro.Accion);
        Assert.Equal(UsuarioId, registro.UsuarioId);
        Assert.Equal(NegocioId, registro.NegocioId);
        Assert.Equal(nameof(Miembro), registro.EntidadTipo);
        Assert.Equal(1, registro.EntidadId);
    }

    [Fact]
    public async Task Vincular_no_audita_cuando_el_pedido_es_rechazado()
    {
        var (servicio, miembros, auditoria) = Crear();
        Sembrar(miembros, 1, "Socia Ya Vinculada", clienteExternoId: "POS-100");

        await Assert.ThrowsAsync<ConflictException>(() => servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-200", UsuarioId), CancellationToken.None));

        Assert.Empty(auditoria.Registros);
    }

    [Fact]
    public async Task Un_miembro_vinculado_ya_no_aparece_en_la_lista()
    {
        var (servicio, miembros, _) = Crear();
        Sembrar(miembros, 1, "Socia De Mostrador");

        await servicio.VincularAsync(
            new VincularClienteExternoSolicitud(NegocioId, 1, "POS-100", UsuarioId), CancellationToken.None);

        Assert.Empty(await servicio.ListarSinVincularAsync(NegocioId, CancellationToken.None));
    }
}
