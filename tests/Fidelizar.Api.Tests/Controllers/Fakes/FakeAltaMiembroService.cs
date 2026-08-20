using Fidelizar.Application.Services;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

/// <summary>Records what the controller asked for, no database involved (ARCHITECTURE §11).</summary>
public sealed class FakeAltaMiembroService : IAltaMiembroService
{
    public AltaMiembroSolicitud? UltimaSolicitud { get; private set; }

    public Miembro? MiembroARetornar { get; set; }

    public Task<Miembro> DarDeAltaAsync(AltaMiembroSolicitud solicitud, CancellationToken cancellationToken = default)
    {
        UltimaSolicitud = solicitud;
        return Task.FromResult(MiembroARetornar!);
    }
}
