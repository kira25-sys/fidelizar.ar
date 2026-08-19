using Fidelizar.Application.Services;

namespace Fidelizar.Api.Tests.Controllers.Fakes;

public sealed class FakeUsuarioService : IUsuarioService
{
    public IReadOnlyList<UsuarioResultado> UsuariosARetornar { get; set; } = [];

    public UsuarioResultado? UsuarioCreadoARetornar { get; set; }

    public CrearUsuarioSolicitud? UltimaSolicitud { get; private set; }

    public Task<IReadOnlyList<UsuarioResultado>> ListarAsync(int negocioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(UsuariosARetornar);

    public Task<UsuarioResultado> CrearAsync(
        CrearUsuarioSolicitud solicitud, CancellationToken cancellationToken = default)
    {
        UltimaSolicitud = solicitud;
        return Task.FromResult(UsuarioCreadoARetornar!);
    }
}
