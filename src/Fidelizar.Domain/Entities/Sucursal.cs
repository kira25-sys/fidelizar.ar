using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Entities;

/// <summary>
/// A physical branch of a <see cref="Negocio"/>. Organisational only — never a calculation
/// boundary (RN-07). Any query that filters totals by <see cref="Sucursal"/> is a defect
/// (DATA-MODEL §1). Private constructor, like <see cref="Usuario"/> and <see cref="Corte"/>:
/// <see cref="Crear"/> is the only way to build one, so S10's validation cannot be bypassed by a
/// caller that skips it.
/// </summary>
public sealed class Sucursal
{
    public int Id { get; private set; }

    public int NegocioId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>How the POS names this branch. Used to reject sales carrying an unknown code.</summary>
    public string? CodigoExterno { get; private set; }

    public bool Activa { get; private set; } = true;

    private Sucursal()
    {
    }

    public static Sucursal Crear(int negocioId, string nombre, string? codigoExterno = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ValidationException("Nombre es obligatorio para una Sucursal.", "NOMBRE_REQUERIDO");
        }

        return new Sucursal
        {
            NegocioId = negocioId,
            Nombre = nombre,
            CodigoExterno = codigoExterno,
            Activa = true,
        };
    }
}
