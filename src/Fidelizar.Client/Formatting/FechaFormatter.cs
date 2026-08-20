using System.Globalization;

namespace Fidelizar.Client.Formatting;

/// <summary>
/// "DD/MM/AAAA" everywhere a date appears in prose (FLOW-S2-S5.md "Conventions used below").
/// Always <see cref="CultureInfo.InvariantCulture"/>, never the browser's current culture: "/"
/// in a .NET custom date format string is the culture's date-separator placeholder, not a
/// literal character, so an unspecified culture can silently render a different symbol.
/// </summary>
public static class FechaFormatter
{
    public static string Corta(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// "DD/MM/AAAA HH:MM" in the reader's own time zone, for <c>RegistradoEn</c> — the only
    /// instant S7 renders (DATA-MODEL §4: "when the system learned about it", a
    /// <c>timestamptz</c> stored and served in UTC). A manager reconciling a day's ledger reads
    /// wall-clock time, not UTC, so it is converted here; the browser's zone is what
    /// <see cref="DateTime.ToLocalTime"/> resolves to under WebAssembly.
    ///
    /// A value that arrives with no kind is assumed UTC rather than local: the API only ever
    /// sends UTC, and guessing "local" would silently shift every row by the zone offset.
    /// <see cref="CultureInfo.InvariantCulture"/> for the same reason <see cref="Corta"/> uses
    /// it — "/" and ":" are culture separator placeholders in a .NET format string, not literals.
    /// </summary>
    public static string ConHora(DateTime instante)
    {
        var utc = instante.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(instante, DateTimeKind.Utc)
            : instante.ToUniversalTime();

        return utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>The date half of <see cref="ConHora"/>, for comparing an instant against a
    /// <c>FechaEfectiva</c> — S7 flags a movement as retroactive only when the two differ.</summary>
    public static DateOnly FechaLocalDe(DateTime instante)
    {
        var utc = instante.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(instante, DateTimeKind.Utc)
            : instante.ToUniversalTime();

        return DateOnly.FromDateTime(utc.ToLocalTime());
    }
}
