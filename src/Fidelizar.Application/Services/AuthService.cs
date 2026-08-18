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

        // RolUsuario.Sistema (F1-03 follow-up) is not a person and never an account — it exists
        // only to be pointed at from a movement/cutoff/configuration row. Checked before the
        // password so a Sistema user is never authenticated by any path, coincidental password
        // match included; the check short-circuits, so passwordHasher.Verify never even runs
        // against its hash.
        //
        // Same outcome whether the email does not exist, the account is deactivated, the role is
        // Sistema, or the password is wrong: a login endpoint that distinguishes these is a
        // user-enumeration leak on a password endpoint exposed to the internet.
        if (usuario is null || usuario.Rol == RolUsuario.Sistema || !usuario.Activo ||
            !passwordHasher.Verify(usuario.PasswordHash, password))
        {
            throw new AuthenticationException(MensajeCredencialesInvalidas);
        }

        return usuario;
    }
}
