using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Exceptions;
using Fidelizar.Domain.Repositories;
using Fidelizar.Domain.Security;

namespace Fidelizar.Application.Services;

/// <summary>See <see cref="IAuthService"/>.</summary>
public sealed class AuthService(
    INegocioRepository negocioRepository,
    IUsuarioRepository usuarioRepository,
    IPasswordHasher passwordHasher) : IAuthService
{
    private const string MensajeCredencialesInvalidas = "Email o contraseña incorrectos.";

    public async Task<Usuario> AutenticarAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new AuthenticationException(MensajeCredencialesInvalidas);
        }

        // FUNCTIONAL-SPEC §13.2: a cashier signs in with only email and password, never a
        // business selector — NegocioId is resolved here, server-side, instead. It stays a
        // required, explicit value passed to the repository (I8), never a convention baked into
        // the query — see INegocioRepository for why this lookup exists at all.
        var negocio = await negocioRepository.ObtenerUnicoAsync(cancellationToken);
        var usuario = await usuarioRepository.ObtenerPorEmailAsync(negocio.Id, email, cancellationToken);

        // Same outcome whether the email does not exist, the account is deactivated, or the
        // password is wrong: a login endpoint that distinguishes these is a user-enumeration leak
        // on a password endpoint exposed to the internet.
        if (usuario is null || !usuario.Activo || !passwordHasher.Verify(usuario.PasswordHash, password))
        {
            throw new AuthenticationException(MensajeCredencialesInvalidas);
        }

        return usuario;
    }
}
