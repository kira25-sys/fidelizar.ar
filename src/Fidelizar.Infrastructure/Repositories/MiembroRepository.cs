using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IMiembroRepository"/>.</summary>
public sealed class MiembroRepository(FidelizarDbContext dbContext) : IMiembroRepository
{
    /// <summary>S2 never becomes S-list: capped so a search stays a handful of homonym
    /// candidates, never a scroll through the roster (FUNCTIONAL-SPEC §4).</summary>
    private const int MaxResultadosBusqueda = 25;

    public Task<Miembro?> GetByClienteExternoIdAsync(
        int negocioId, string clienteExternoId, CancellationToken cancellationToken = default) =>
        dbContext.Miembros.SingleOrDefaultAsync(
            m => m.NegocioId == negocioId && m.ClienteExternoId == clienteExternoId, cancellationToken);

    public Task<Miembro?> GetByIdAsync(int negocioId, int id, CancellationToken cancellationToken = default) =>
        dbContext.Miembros.SingleOrDefaultAsync(m => m.NegocioId == negocioId && m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Miembro>> BuscarAsync(
        int negocioId, IReadOnlyList<string> palabrasNormalizadas, CancellationToken cancellationToken = default)
    {
        var consulta = dbContext.Miembros.Where(m => m.NegocioId == negocioId);

        // AND across query words — FUNCTIONAL-SPEC §4's own example ("gomez ana" finds
        // "Ana María Gómez") needs every typed word present, not merely one of them.
        foreach (var palabra in palabrasNormalizadas)
        {
            consulta = consulta.Where(m => m.NombreNormalizado.Contains(palabra));
        }

        return await consulta
            .OrderBy(m => m.Nombre)
            .Take(MaxResultadosBusqueda)
            .ToListAsync(cancellationToken);
    }

    public async Task<Miembro> AddAsync(Miembro miembro, CancellationToken cancellationToken = default)
    {
        dbContext.Miembros.Add(miembro);
        await dbContext.SaveChangesAsync(cancellationToken);
        return miembro;
    }

    /// <summary>Oldest first: the member who has been unlinked longest is the one accruing
    /// nothing for longest (ROADMAP F1-14). No cap — a capped work queue hides work.</summary>
    public async Task<IReadOnlyList<Miembro>> ListarSinVincularAsync(
        int negocioId, CancellationToken cancellationToken = default) =>
        await dbContext.Miembros
            .Where(m => m.NegocioId == negocioId && m.ClienteExternoId == null)
            .OrderBy(m => m.FechaAlta)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The <c>ClienteExternoId IS NULL</c> predicate is the write's own guard: two concurrent
    /// requests for the same member cannot both link it, and the loser gets 0 rows rather than
    /// overwriting the winner's id. The duplicate-id race is closed by the partial unique index,
    /// translated below (DATA-MODEL §3).
    /// </summary>
    public async Task<bool> VincularClienteExternoAsync(
        int negocioId,
        int miembroId,
        string clienteExternoId,
        DateTime ahoraUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filas = await dbContext.Miembros
                .Where(m => m.NegocioId == negocioId && m.Id == miembroId && m.ClienteExternoId == null)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(m => m.ClienteExternoId, clienteExternoId)
                        .SetProperty(m => m.ActualizadoEn, ahoraUtc),
                    cancellationToken);

            return filas > 0;
        }
        catch (Exception ex) when (EsViolacionDeClienteExternoIdDuplicado(ex))
        {
            throw new ConflictException(
                $"Ya hay un socio vinculado al id de POS '{clienteExternoId}' en este negocio.",
                "CLIENTE_EXTERNO_ID_DUPLICADO");
        }
    }

    /// <summary>ExecuteUpdate surfaces the Postgres error directly; a tracked SaveChanges would
    /// wrap it in DbUpdateException. Both shapes are recognised so this keeps working if the
    /// write above ever changes form.</summary>
    private static bool EsViolacionDeClienteExternoIdDuplicado(Exception ex) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException) is { } pg
        && pg.SqlState == PostgresErrorCodes.UniqueViolation
        && pg.ConstraintName == "IX_Miembros_NegocioId_ClienteExternoId";
}
