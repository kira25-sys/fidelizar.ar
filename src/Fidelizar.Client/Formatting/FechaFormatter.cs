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
}
