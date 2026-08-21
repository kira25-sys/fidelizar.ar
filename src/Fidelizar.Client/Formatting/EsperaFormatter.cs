namespace Fidelizar.Client.Formatting;

/// <summary>
/// F1-14 — how long a member has been waiting for its <c>ClienteExternoId</c>, in plain Spanish.
/// The list arrives oldest first, so this is what makes the top of it read as the urgent end.
/// </summary>
public static class EsperaFormatter
{
    /// <summary>Days only, never months: "hace 214 días" is the figure that prompts the fix.</summary>
    public static string Espera(DateOnly fechaAlta, DateOnly hoy)
    {
        var dias = hoy.DayNumber - fechaAlta.DayNumber;

        return dias switch
        {
            // <= 0 covers a member registered today and a tablet clock running behind the server.
            <= 0 => "hoy",
            1 => "hace 1 día",
            _ => $"hace {dias} días",
        };
    }
}
