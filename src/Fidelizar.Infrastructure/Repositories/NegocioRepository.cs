using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="INegocioRepository"/>.</summary>
public sealed class NegocioRepository(FidelizarDbContext dbContext) : INegocioRepository
{
    public async Task<Negocio> ObtenerUnicoAsync(CancellationToken cancellationToken = default)
    {
        var negocios = await dbContext.Negocios
            .Where(n => n.Activo)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (negocios.Count != 1)
        {
            throw new ConflictException(
                $"Se esperaba exactamente un Negocio activo en esta base de datos y se " +
                $"encontraron {negocios.Count} (ARCHITECTURE §5: un deployment por negocio). No " +
                "se adivina cual usar — hay que corregir los datos.",
                "NEGOCIO_UNICO_ESPERADO");
        }

        return negocios[0];
    }
}
