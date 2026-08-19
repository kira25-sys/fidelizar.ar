using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Tests.Entities;

/// <summary>S10 Sucursales — <c>Sucursal.Crear</c> is the only way to build one, same discipline
/// as <see cref="Usuario"/>.</summary>
public class SucursalTests
{
    [Fact]
    public void Crear_con_nombre_valido_queda_activa()
    {
        var sucursal = Sucursal.Crear(1, "Sucursal Centro", "COD-1");

        Assert.Equal(1, sucursal.NegocioId);
        Assert.Equal("Sucursal Centro", sucursal.Nombre);
        Assert.Equal("COD-1", sucursal.CodigoExterno);
        Assert.True(sucursal.Activa);
    }

    [Fact]
    public void CodigoExterno_es_opcional()
    {
        var sucursal = Sucursal.Crear(1, "Sucursal Centro");

        Assert.Null(sucursal.CodigoExterno);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nombre_es_obligatorio(string? nombre)
    {
        var ex = Assert.Throws<ValidationException>(() => Sucursal.Crear(1, nombre!));

        Assert.Equal("NOMBRE_REQUERIDO", ex.ErrorCode);
    }
}
