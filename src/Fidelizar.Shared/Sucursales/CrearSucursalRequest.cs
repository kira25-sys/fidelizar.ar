using System.ComponentModel.DataAnnotations;

namespace Fidelizar.Shared.Sucursales;

/// <summary>S10 Sucursales — create (Dueño only). The import is strict about branch codes
/// (DATA-MODEL §7): this is where a business's own <c>CodigoExterno</c> vocabulary is defined by
/// hand before any file import can match against it.</summary>
public sealed record CrearSucursalRequest(
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    string Nombre,
    string? CodigoExterno);
