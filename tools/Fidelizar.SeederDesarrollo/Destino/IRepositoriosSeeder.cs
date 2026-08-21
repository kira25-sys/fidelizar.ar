using Fidelizar.Domain.Entities;

namespace Fidelizar.SeederDesarrollo.Destino;

/// <summary>
/// The two tool-local repositories this seeder needs and <c>Fidelizar.Domain.Repositories</c>
/// deliberately does not offer. Kept here, out of <c>Fidelizar.Domain</c>, for the same reason
/// <c>Fidelizar.MigracionOctaviano.Destino</c> keeps its own: they exist so this tool's logic can
/// be tested against fakes built from invented data, and the product itself must not grow a
/// "create a Negocio" or "count every table" operation just because a development tool wanted one.
/// They follow ARCHITECTURE §3 all the same — no generic repository, no <c>GetAll&lt;T&gt;</c>,
/// nothing that could ever update or delete a ledger row.
/// </summary>
public interface INegocioSeederRepository
{
    /// <summary>
    /// The single business row, or null on a database nobody has seeded yet.
    /// <c>Fidelizar.Domain.Repositories.INegocioRepository.ObtenerUnicoAsync</c> cannot be used
    /// here: it throws when the count is not exactly one, which is precisely the state this tool
    /// runs into first and has to handle rather than crash on.
    /// </summary>
    Task<Negocio?> ObtenerPrimeroAsync(CancellationToken cancellationToken = default);

    Task<Negocio> CrearAsync(Negocio negocio, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads how much is already in the target database. Read-only by construction: there is no write
/// on this interface at all, because its entire purpose is deciding whether writing is allowed.
/// </summary>
public interface IEstadoBaseRepository
{
    /// <summary>Whether the schema exists at all — no applied EF migration means an empty
    /// database, and counting rows in tables that do not exist would only throw.</summary>
    Task<bool> TieneEsquemaAsync(CancellationToken cancellationToken = default);

    Task<ConteoBase> ContarAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// How many rows the target database already holds in the tables this tool writes to. The ledger
/// count is what makes an accidental run against a real database impossible to miss.
/// </summary>
public sealed record ConteoBase(
    int Negocios, int Sucursales, int Usuarios, int Miembros, long Movimientos)
{
    public static readonly ConteoBase Vacia = new(0, 0, 0, 0, 0);

    public bool EstaVacia =>
        Negocios == 0 && Sucursales == 0 && Usuarios == 0 && Miembros == 0 && Movimientos == 0;
}
