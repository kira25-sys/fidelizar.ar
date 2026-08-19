using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>S6 Ficha completa — Encargada/Dueño only (FUNCTIONAL-SPEC §8). Cada lectura queda
/// auditada (DATA-MODEL §2).</summary>
public class FichaCompletaServiceTests
{
    private const int NegocioId = 1;
    private const int MiembroId = 42;
    private const int UsuarioIdQueLee = 9;

    private static FichaCompletaService CrearServicio(
        out FakeMiembroRepository miembroRepositorio, out FakeRegistroAuditoriaRepository auditoriaRepositorio)
    {
        miembroRepositorio = new FakeMiembroRepository();
        auditoriaRepositorio = new FakeRegistroAuditoriaRepository();
        return new FichaCompletaService(miembroRepositorio, auditoriaRepositorio);
    }

    [Fact]
    public async Task Miembro_inexistente_lanza_EntityNotFoundException()
    {
        var servicio = CrearServicio(out _, out _);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => servicio.ObtenerAsync(NegocioId, MiembroId, UsuarioIdQueLee));
    }

    [Fact]
    public async Task Devuelve_telefono_y_dni()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out _);
        miembroRepositorio.Sembrar(new Miembro
        {
            Id = MiembroId,
            NegocioId = NegocioId,
            Nombre = "Ana Gómez",
            NombreNormalizado = "ana gomez",
            Telefono = "11-5555-5555",
            Dni = "30111222",
            FechaAlta = new DateOnly(2020, 1, 1),
        });

        var ficha = await servicio.ObtenerAsync(NegocioId, MiembroId, UsuarioIdQueLee);

        Assert.Equal("11-5555-5555", ficha.Telefono);
        Assert.Equal("30111222", ficha.Dni);
    }

    /// <summary>DATA-MODEL §2: cada lectura de la ficha completa queda registrada.</summary>
    [Fact]
    public async Task Cada_lectura_escribe_un_RegistroAuditoria_de_VerFichaCompleta()
    {
        var servicio = CrearServicio(out var miembroRepositorio, out var auditoriaRepositorio);
        miembroRepositorio.SembrarNuevo(NegocioId, MiembroId);

        await servicio.ObtenerAsync(NegocioId, MiembroId, UsuarioIdQueLee);
        await servicio.ObtenerAsync(NegocioId, MiembroId, UsuarioIdQueLee);

        Assert.Equal(2, auditoriaRepositorio.Registros.Count);
        Assert.All(auditoriaRepositorio.Registros, r =>
        {
            Assert.Equal("VerFichaCompleta", r.Accion);
            Assert.Equal(UsuarioIdQueLee, r.UsuarioId);
            Assert.Equal(MiembroId, r.EntidadId);
        });
    }
}
