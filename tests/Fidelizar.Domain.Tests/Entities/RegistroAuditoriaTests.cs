using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Tests.Entities;

/// <summary>DATA-MODEL §2: <c>RegistroAuditoria</c> requires a non-empty <c>Accion</c> and has no
/// public setter beyond <see cref="RegistroAuditoria.Registrar"/> — append-only, like the ledger.</summary>
public class RegistroAuditoriaTests
{
    private static readonly DateTime Ahora = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Registrar_exige_una_Accion_no_vacia()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            RegistroAuditoria.Registrar(1, usuarioId: 7, accion: "  ", ocurridoEn: Ahora));

        Assert.Equal("ACCION_REQUERIDA", ex.ErrorCode);
    }

    [Fact]
    public void Registrar_completa_todos_los_campos()
    {
        var registro = RegistroAuditoria.Registrar(
            negocioId: 1,
            usuarioId: 7,
            accion: "VerFichaCompleta",
            ocurridoEn: Ahora,
            entidadTipo: "Miembro",
            entidadId: 42,
            detalle: "{\"motivo\":\"soporte\"}");

        Assert.Equal(1, registro.NegocioId);
        Assert.Equal(7, registro.UsuarioId);
        Assert.Equal("VerFichaCompleta", registro.Accion);
        Assert.Equal("Miembro", registro.EntidadTipo);
        Assert.Equal(42, registro.EntidadId);
        Assert.Equal("{\"motivo\":\"soporte\"}", registro.Detalle);
        Assert.Equal(Ahora, registro.OcurridoEn);
    }

    [Fact]
    public void EntidadTipo_EntidadId_y_Detalle_son_opcionales()
    {
        var registro = RegistroAuditoria.Registrar(1, usuarioId: 7, accion: "Login", ocurridoEn: Ahora);

        Assert.Null(registro.EntidadTipo);
        Assert.Null(registro.EntidadId);
        Assert.Null(registro.Detalle);
    }
}
