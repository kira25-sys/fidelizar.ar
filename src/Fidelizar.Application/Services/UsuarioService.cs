using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Security;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IUsuarioService"/>.</summary>
public sealed class UsuarioService(
    IUsuarioRepository usuarioRepository,
    ISucursalRepository sucursalRepository,
    IPasswordHasher passwordHasher) : IUsuarioService
{
    public async Task<IReadOnlyList<UsuarioResultado>> ListarAsync(
        int negocioId, CancellationToken cancellationToken = default)
    {
        var usuarios = await usuarioRepository.ListarAsync(negocioId, cancellationToken);
        return usuarios.Select(AResultado).ToList();
    }

    public async Task<UsuarioResultado> CrearAsync(
        CrearUsuarioSolicitud solicitud, CancellationToken cancellationToken = default)
    {
        var existente = await usuarioRepository.ObtenerPorEmailAsync(
            solicitud.NegocioId, solicitud.Email, cancellationToken);
        if (existente is not null)
        {
            throw new ConflictException(
                $"Ya existe un usuario con el email '{solicitud.Email}' en este negocio.",
                "USUARIO_EMAIL_DUPLICADO");
        }

        if (solicitud.SucursalId is { } sucursalId
            && await sucursalRepository.GetByIdAsync(solicitud.NegocioId, sucursalId, cancellationToken) is null)
        {
            throw new ValidationException(
                $"La sucursal {sucursalId} no existe en este negocio.", "SUCURSAL_INEXISTENTE");
        }

        // Usuario.Crear enforces the SucursalId/Rol combination (DATA-MODEL §2) — not repeated
        // here, just triggered by passing the values through.
        var usuario = Usuario.Crear(
            solicitud.NegocioId,
            solicitud.NombreCompleto,
            solicitud.Email,
            passwordHasher.Hash(solicitud.Password),
            solicitud.Rol,
            DateTime.UtcNow,
            solicitud.SucursalId);

        var creado = await usuarioRepository.CrearAsync(usuario, cancellationToken);
        return AResultado(creado);
    }

    private static UsuarioResultado AResultado(Usuario usuario) =>
        new(usuario.Id, usuario.NombreCompleto, usuario.Email, usuario.Rol, usuario.SucursalId, usuario.Activo);
}
