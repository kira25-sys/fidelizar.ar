using Fidelizar.Application.Services;
using Fidelizar.Application.Tests.Fakes;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Application.Tests.Services;

/// <summary>ARCHITECTURE §8: session authentication. Same outcome for "email doesn't exist",
/// "wrong password" and "account deactivated" — a login endpoint that distinguishes these is a
/// user-enumeration leak.</summary>
public class AuthServiceTests
{
    private static readonly Negocio ElNegocio = new() { Id = 1, Nombre = "El Negocio" };
    private static readonly DateTime Ahora = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    private static Usuario UsuarioActivo(FakePasswordHasher hasher, string password = "correcta") =>
        Usuario.Crear(
            ElNegocio.Id, "Ana Gomez", "ana@x.com", hasher.Hash(password), RolUsuario.Cajero, Ahora, sucursalId: 5);

    [Fact]
    public async Task Credenciales_correctas_devuelven_el_Usuario()
    {
        var hasher = new FakePasswordHasher();
        var usuario = UsuarioActivo(hasher);
        var servicio = new AuthService(
            new FakeNegocioRepository(ElNegocio), new FakeUsuarioRepository(usuario), hasher);

        var autenticado = await servicio.AutenticarAsync("ana@x.com", "correcta");

        Assert.Equal(usuario.Email, autenticado.Email);
    }

    [Fact]
    public async Task Password_incorrecta_lanza_AuthenticationException()
    {
        var hasher = new FakePasswordHasher();
        var usuario = UsuarioActivo(hasher);
        var servicio = new AuthService(
            new FakeNegocioRepository(ElNegocio), new FakeUsuarioRepository(usuario), hasher);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => servicio.AutenticarAsync("ana@x.com", "incorrecta"));
    }

    [Fact]
    public async Task Email_inexistente_lanza_AuthenticationException()
    {
        var hasher = new FakePasswordHasher();
        var servicio = new AuthService(
            new FakeNegocioRepository(ElNegocio), new FakeUsuarioRepository(), hasher);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => servicio.AutenticarAsync("nadie@x.com", "cualquiera"));
    }

    [Fact]
    public async Task Usuario_desactivado_lanza_AuthenticationException_aunque_la_password_sea_correcta()
    {
        var hasher = new FakePasswordHasher();
        var usuario = UsuarioActivo(hasher);
        usuario.Desactivar();
        var servicio = new AuthService(
            new FakeNegocioRepository(ElNegocio), new FakeUsuarioRepository(usuario), hasher);

        await Assert.ThrowsAsync<AuthenticationException>(
            () => servicio.AutenticarAsync("ana@x.com", "correcta"));
    }

    [Fact]
    public async Task Email_o_password_vacios_lanzan_AuthenticationException_sin_tocar_el_repositorio()
    {
        var hasher = new FakePasswordHasher();
        // FakeNegocioRepository sin negocios: si el servicio llegara a llamarlo, ObtenerUnicoAsync
        // tira ConflictException — esto prueba que la validación vacía corta antes de esa llamada.
        var servicio = new AuthService(new FakeNegocioRepository(), new FakeUsuarioRepository(), hasher);

        await Assert.ThrowsAsync<AuthenticationException>(() => servicio.AutenticarAsync("", "algo"));
        await Assert.ThrowsAsync<AuthenticationException>(() => servicio.AutenticarAsync("ana@x.com", ""));
    }

    [Fact]
    public async Task Todos_los_casos_de_falla_devuelven_el_mismo_mensaje()
    {
        var hasher = new FakePasswordHasher();
        var usuario = UsuarioActivo(hasher);
        var servicio = new AuthService(
            new FakeNegocioRepository(ElNegocio), new FakeUsuarioRepository(usuario), hasher);

        var porPasswordIncorrecta = await Assert.ThrowsAsync<AuthenticationException>(
            () => servicio.AutenticarAsync("ana@x.com", "incorrecta"));
        var porEmailInexistente = await Assert.ThrowsAsync<AuthenticationException>(
            () => servicio.AutenticarAsync("nadie@x.com", "incorrecta"));

        Assert.Equal(porPasswordIncorrecta.Message, porEmailInexistente.Message);
    }
}
