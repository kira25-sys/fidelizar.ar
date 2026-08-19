using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>S10 Usuarios (Dueño only).</summary>
public class UsuarioServiceTests
{
    private const int NegocioId = 1;

    private static UsuarioService CrearServicio(
        out FakeUsuarioRepository usuarioRepositorio, out FakeSucursalRepository sucursalRepositorio)
    {
        usuarioRepositorio = new FakeUsuarioRepository();
        sucursalRepositorio = new FakeSucursalRepository();
        return new UsuarioService(usuarioRepositorio, sucursalRepositorio, new FakePasswordHasher());
    }

    [Fact]
    public async Task Crear_hashea_la_contrasena_antes_de_persistir()
    {
        var servicio = CrearServicio(out var usuarioRepositorio, out var sucursalRepositorio);
        var sucursal = sucursalRepositorio.Sembrar(NegocioId);

        var creado = await servicio.CrearAsync(new CrearUsuarioSolicitud(
            NegocioId, "Ana Cajera", "ana@x.com", "una-contrasena", RolUsuario.Cajero, sucursal.Id));

        Assert.Equal("Ana Cajera", creado.NombreCompleto);
        Assert.Equal(RolUsuario.Cajero, creado.Rol);
        var persistido = await usuarioRepositorio.ObtenerPorEmailAsync(NegocioId, "ana@x.com");
        Assert.NotNull(persistido);
        Assert.NotEqual("una-contrasena", persistido!.PasswordHash);
    }

    [Fact]
    public async Task Email_duplicado_en_el_mismo_negocio_se_rechaza()
    {
        var servicio = CrearServicio(out var usuarioRepositorio, out var sucursalRepositorio);
        var sucursal = sucursalRepositorio.Sembrar(NegocioId);
        await usuarioRepositorio.CrearAsync(Usuario.Crear(
            NegocioId, "Ana Cajera", "ana@x.com", "hash", RolUsuario.Cajero, DateTime.UtcNow, sucursal.Id));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => servicio.CrearAsync(new CrearUsuarioSolicitud(
            NegocioId, "Ana Otra", "ana@x.com", "otra-contrasena", RolUsuario.Cajero, sucursal.Id)));

        Assert.Equal("USUARIO_EMAIL_DUPLICADO", ex.ErrorCode);
    }

    [Fact]
    public async Task SucursalId_inexistente_se_rechaza()
    {
        var servicio = CrearServicio(out _, out _);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => servicio.CrearAsync(new CrearUsuarioSolicitud(
            NegocioId, "Ana Cajera", "ana@x.com", "una-contrasena", RolUsuario.Cajero, SucursalId: 999)));

        Assert.Equal("SUCURSAL_INEXISTENTE", ex.ErrorCode);
    }

    [Fact]
    public async Task Dueno_sin_SucursalId_se_crea_correctamente()
    {
        var servicio = CrearServicio(out _, out _);

        var creado = await servicio.CrearAsync(new CrearUsuarioSolicitud(
            NegocioId, "El Dueño", "dueno@x.com", "una-contrasena", RolUsuario.Dueno, SucursalId: null));

        Assert.Null(creado.SucursalId);
    }

    [Fact]
    public async Task Listar_filtra_por_NegocioId()
    {
        var servicio = CrearServicio(out var usuarioRepositorio, out _);
        await usuarioRepositorio.CrearAsync(Usuario.Crear(
            NegocioId, "Ana Cajera", "ana@x.com", "hash", RolUsuario.Cajero, DateTime.UtcNow, sucursalId: 1));
        await usuarioRepositorio.CrearAsync(Usuario.Crear(
            negocioId: 2, "Otro Negocio", "otro@x.com", "hash", RolUsuario.Cajero, DateTime.UtcNow, sucursalId: 1));

        var usuarios = await servicio.ListarAsync(NegocioId);

        var usuario = Assert.Single(usuarios);
        Assert.Equal("Ana Cajera", usuario.NombreCompleto);
    }
}
