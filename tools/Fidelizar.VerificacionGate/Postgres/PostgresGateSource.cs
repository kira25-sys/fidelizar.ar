using Fidelizar.Application.Services;
using Fidelizar.Infrastructure.Persistence;
using Fidelizar.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.VerificacionGate.Postgres;

public sealed record SocioPostgres(int MiembroId, string? ClienteExternoId, decimal Saldo);

/// <summary>
/// Punta 1 (ROADMAP F0-11): the balance Fidelizar's own ported code computes in Postgres, through
/// <see cref="ISaldoService.ObtenerSaldoAsync"/> — the exact same call path the product itself
/// uses (I2: <c>SUM(Monto)</c>). Deliberately not a raw SQL SUM written ad hoc for this tool: if
/// <see cref="SaldoService"/> itself carries a bug, this is the punta that has to see it too.
/// </summary>
public static class PostgresGateSource
{
    public static async Task<(int NegocioId, IReadOnlyList<SocioPostgres> Socios)> LeerAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<FidelizarDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new FidelizarDbContext(options);

        var negocio = await dbContext.Negocios.OrderBy(n => n.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No hay ningún Negocio en la base del gate. ¿Corrió la migración F0-09 contra este contenedor?");

        var miembros = await dbContext.Miembros
            .Where(m => m.NegocioId == negocio.Id)
            .Select(m => new { m.Id, m.ClienteExternoId })
            .ToListAsync(cancellationToken);

        var saldoService = new SaldoService(new MovimientoRepository(dbContext));

        var resultado = new List<SocioPostgres>(miembros.Count);
        foreach (var miembro in miembros)
        {
            var saldo = await saldoService.ObtenerSaldoAsync(negocio.Id, miembro.Id, cancellationToken);
            resultado.Add(new SocioPostgres(miembro.Id, miembro.ClienteExternoId, saldo));
        }

        return (negocio.Id, resultado);
    }
}
