using System.ComponentModel.DataAnnotations;

namespace Fidelizar.Shared.Auth;

/// <summary>
/// S1 Ingreso — login request body (FUNCTIONAL-SPEC §13.2). Attributes give the cashier a fast
/// client-side message; the server verifies the real credential regardless (ARCHITECTURE §3).
/// </summary>
public sealed record LoginRequest(
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no es válido.")]
    string Email,
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    string Password);
