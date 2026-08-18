using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Tests.Entities;

/// <summary>DATA-MODEL §2: <c>Usuario</c>'s invariants — a branch-scoped role always carries a
/// <c>SucursalId</c>, a business-wide one never does, and deactivation never deletes.</summary>
public class UsuarioTests
{
    private static readonly DateTime Ahora = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(RolUsuario.Cajero)]
    [InlineData(RolUsuario.Encargada)]
    public void Cajero_y_Encargada_requieren_SucursalId(RolUsuario rol)
    {
        var ex = Assert.Throws<ValidationException>(() =>
            Usuario.Crear(1, "Ana Gomez", "ana@x.com", "hash", rol, Ahora, sucursalId: null));

        Assert.Equal("SUCURSAL_REQUERIDA", ex.ErrorCode);
    }

    [Theory]
    [InlineData(RolUsuario.Dueno)]
    [InlineData(RolUsuario.Soporte)]
    public void Dueno_y_Soporte_no_admiten_SucursalId(RolUsuario rol)
    {
        var ex = Assert.Throws<ValidationException>(() =>
            Usuario.Crear(1, "Ana Gomez", "ana@x.com", "hash", rol, Ahora, sucursalId: 5));

        Assert.Equal("SUCURSAL_NO_APLICA", ex.ErrorCode);
    }

    [Fact]
    public void Cajero_con_SucursalId_se_crea_activo()
    {
        var usuario = Usuario.Crear(1, "Ana Gomez", "ana@x.com", "hash", RolUsuario.Cajero, Ahora, sucursalId: 5);

        Assert.True(usuario.Activo);
        Assert.Equal(5, usuario.SucursalId);
        Assert.Equal(Ahora, usuario.CreadoEn);
    }

    [Fact]
    public void Dueno_sin_SucursalId_se_crea_correctamente()
    {
        var usuario = Usuario.Crear(1, "Marta Dueña", "marta@x.com", "hash", RolUsuario.Dueno, Ahora);

        Assert.Null(usuario.SucursalId);
        Assert.Equal(RolUsuario.Dueno, usuario.Rol);
    }

    [Theory]
    [InlineData("", "email@x.com", "hash", "NOMBRE_REQUERIDO")]
    [InlineData("Ana", "", "hash", "EMAIL_REQUERIDO")]
    [InlineData("Ana", "email@x.com", "", "PASSWORD_HASH_REQUERIDO")]
    public void Campos_obligatorios_no_pueden_quedar_vacios(
        string nombre, string email, string passwordHash, string codigoEsperado)
    {
        var ex = Assert.Throws<ValidationException>(() =>
            Usuario.Crear(1, nombre, email, passwordHash, RolUsuario.Dueno, Ahora));

        Assert.Equal(codigoEsperado, ex.ErrorCode);
    }

    [Fact]
    public void Desactivar_apaga_Activo_sin_borrar_nada_mas()
    {
        var usuario = Usuario.Crear(1, "Ana Gomez", "ana@x.com", "hash", RolUsuario.Cajero, Ahora, sucursalId: 5);

        usuario.Desactivar();

        Assert.False(usuario.Activo);
        Assert.Equal("Ana Gomez", usuario.NombreCompleto);
        Assert.Equal(5, usuario.SucursalId);
    }
}
