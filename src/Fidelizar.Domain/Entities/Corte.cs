namespace Fidelizar.Domain.Entities;

/// <summary>
/// The program's start date for one business — from when the system counts (DATA-MODEL §4).
/// One row per <c>NegocioId</c>, unique at the schema level (F0-03 migration), not by
/// discipline. Declared at import, never a constant: a hard-coded cutoff double-credits every
/// purchase between the real cutoff and the import day — Octaviano learned this the hard way.
/// With no cutoff recorded, accrual must fail loudly rather than invent one (F0-07,
/// <see cref="Fidelizar.Domain.Repositories.ICorteRepository"/>).
/// </summary>
public sealed class Corte
{
    public int Id { get; private set; }

    public int NegocioId { get; private set; }

    public DateOnly Fecha { get; private set; }

    /// <summary>
    /// Who declared this cutoff. Scalar column on purpose: <c>Usuario</c> is F1-03 and does not
    /// exist yet in this wave, so there is no navigation property and no FK constraint here.
    /// F1-03 introduces <c>Usuario</c> and adds the constraint in a later migration.
    /// </summary>
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
