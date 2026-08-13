using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Entities;

/// <summary>
/// A real person with a role, replacing Octaviano's list of Telegram chat IDs (DATA-MODEL §2,
/// Plan §5). Every <see cref="MovimientoCredito"/> stamps the real actor who caused it — never
/// <c>telegram:123456</c> — and this is the row that stamp points at.
///
/// The constructor is private, like every other entity in this layer: <see cref="Crear"/> is the
/// only way to build one, so the invariants below (a required <see cref="SucursalId"/> for a
/// branch-scoped role, none for a business-wide one) cannot be bypassed by constructing the object
/// some other way — and are testable with no database (ARCHITECTURE §3, §11).
/// </summary>
public sealed class Usuario
{
    public int Id { get; private set; }

    public int NegocioId { get; private set; }

    /// <summary>What gets stamped on a movement (DATA-MODEL §2).</summary>
    public string NombreCompleto { get; private set; } = string.Empty;

    /// <summary>
    /// Unique per <see cref="NegocioId"/>, not global — <c>citext</c> at the database level
    /// (DATA-MODEL §2), so two accounts differing only in letter case can never coexist.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// ASP.NET Core Identity's hash format (DATA-MODEL §2) — never a plain password, never
    /// anything this class or any caller reads back.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    public RolUsuario Rol { get; private set; }

    /// <summary>
    /// Required for <see cref="RolUsuario.Cajero"/> and <see cref="RolUsuario.Encargada"/>; null
    /// for <see cref="RolUsuario.Dueno"/> and <see cref="RolUsuario.Soporte"/> (DATA-MODEL §2).
    /// </summary>
    public int? SucursalId { get; private set; }

    /// <summary>Deactivation, never deletion — movements reference this row forever (DATA-MODEL §2).</summary>
    public bool Activo { get; private set; }

    public DateTime CreadoEn { get; private set; }

    private Usuario()
    {
    }

    public static Usuario Crear(
        int negocioId,
        string nombreCompleto,
        string email,
        string passwordHash,
        RolUsuario rol,
        DateTime creadoEn,
        int? sucursalId = null)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            throw new ValidationException("NombreCompleto es obligatorio.", "NOMBRE_REQUERIDO");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Email es obligatorio.", "EMAIL_REQUERIDO");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ValidationException("PasswordHash es obligatorio.", "PASSWORD_HASH_REQUERIDO");
        }

        // DATA-MODEL §2: SucursalId is required for a branch-scoped role and must stay null for a
        // business-wide one — a Dueño is never accidentally pinned to one branch, and a Cajero is
        // never left without one.
        var requiereSucursal = rol is RolUsuario.Cajero or RolUsuario.Encargada;

        if (requiereSucursal && sucursalId is null)
        {
            throw new ValidationException(
                $"SucursalId es obligatorio para el rol {rol} (DATA-MODEL §2).", "SUCURSAL_REQUERIDA");
        }

        if (!requiereSucursal && sucursalId is not null)
        {
            throw new ValidationException(
                $"SucursalId debe quedar vacio para el rol {rol} (DATA-MODEL §2).", "SUCURSAL_NO_APLICA");
        }

        return new Usuario
        {
            NegocioId = negocioId,
            NombreCompleto = nombreCompleto,
            Email = email,
            PasswordHash = passwordHash,
            Rol = rol,
            SucursalId = sucursalId,
            Activo = true,
            CreadoEn = creadoEn,
        };
    }

    /// <summary>
    /// Deactivation, never deletion (DATA-MODEL §2) — the only mutation this entity allows after
    /// <see cref="Crear"/>. The row stays forever so every movement it ever stamped keeps its
    /// real actor.
    /// </summary>
    public void Desactivar()
    {
        Activo = false;
    }
}
