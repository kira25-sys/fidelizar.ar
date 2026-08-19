namespace Fidelizar.Domain.Reglas;

/// <summary>
/// RN-11 — birthday notice starts 2 days before the date itself. Only day and month matter; the
/// year on <c>Miembro.FechaNacimiento</c> is ignored even when present. Kept in Domain, not
/// Application, so the rule is testable with no database (ARCHITECTURE §3) and carries its RN
/// number in one place instead of being re-derived inline wherever S3's alert strip is built.
/// </summary>
public static class AvisoCumpleanos
{
    private const int DiasDeAnticipacion = 2;

    /// <summary>Whether today falls within the notice window for this birthday.</summary>
    public static bool DebeAvisar(DateOnly? fechaNacimiento, DateOnly hoy)
    {
        if (fechaNacimiento is not { } fecha)
        {
            return false;
        }

        var diasHastaElCumple = DiasHastaElCumpleanos(fecha, hoy);
        return diasHastaElCumple >= 0 && diasHastaElCumple <= DiasDeAnticipacion;
    }

    private static int DiasHastaElCumpleanos(DateOnly fechaNacimiento, DateOnly hoy)
    {
        var proximo = ProximaOcurrencia(fechaNacimiento, hoy.Year);
        if (proximo < hoy)
        {
            proximo = ProximaOcurrencia(fechaNacimiento, hoy.Year + 1);
        }

        return proximo.DayNumber - hoy.DayNumber;
    }

    /// <summary>
    /// A birthday on Feb 29 falls back to Feb 28 in a non-leap year — simpler than a member born
    /// on a leap day getting a notice only one year in four.
    /// </summary>
    private static DateOnly ProximaOcurrencia(DateOnly fechaNacimiento, int anio)
    {
        var dia = fechaNacimiento.Day;
        if (fechaNacimiento.Month == 2 && dia == 29 && !DateTime.IsLeapYear(anio))
        {
            dia = 28;
        }

        return new DateOnly(anio, fechaNacimiento.Month, dia);
    }
}
