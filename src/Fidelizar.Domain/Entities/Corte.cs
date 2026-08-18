namespace Fidelizar.Domain.Entities;

/// <summary>
/// The program's start date for one business — from when the system counts (DATA-MODEL §4). One
/// row per <c>NegocioId</c>, unique at the schema level. Declared at import, never a constant: a
/// hard-coded cutoff double-credits every purchase between the real cutoff and the import day,
/// as Octaviano learned. With no cutoff recorded, accrual fails loudly rather than invent one
/// (F0-07, <see cref="Fidelizar.Domain.Repositories.ICorteRepository"/>).
/// </summary>
public sealed class Corte
{
    public int Id { get; private set; }

    public int NegocioId { get; private set; }

    public DateOnly Fecha { get; private set; }

    /// <summary>Who declared this cutoff. Scalar column: the FK to <c>Usuario</c> is added by
    /// F1-03's migration, not here.</summary>
    public int DeclaradoPorUsuarioId { get; private set; }

    public DateTime DeclaradoEn { get; private set; }

    private Corte()
    {
    }

    public static Corte Declarar(int negocioId, DateOnly fecha, int declaradoPorUsuarioId, DateTime declaradoEn) =>
        new()
        {
            NegocioId = negocioId,
            Fecha = fecha,
            DeclaradoPorUsuarioId = declaradoPorUsuarioId,
            DeclaradoEn = declaradoEn,
        };
}
