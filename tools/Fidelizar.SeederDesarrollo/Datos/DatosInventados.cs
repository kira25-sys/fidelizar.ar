using Fidelizar.Domain.Entities;

namespace Fidelizar.SeederDesarrollo.Datos;

/// <summary>
/// The whole fixture this tool writes. <b>Every name, CUIT, address, phone, DNI, email and amount
/// below is invented, and deliberately invented in a way that looks invented</b> (CLAUDE.md): the
/// CUIT is all zeros, the addresses are "Calle Falsa 123", the emails end in the reserved
/// <c>.invalid</c> TLD (RFC 2606, which can never resolve), and the DNIs are in the 99,000,000
/// range that has never been issued. Nothing here was read from <c>octaviano.db</c>, from the
/// padron spreadsheets, or from any real database — a real member is never a fixture (CLAUDE.md,
/// "Real member data").
///
/// <para>
/// <b><see cref="NegocioCuit"/> doubles as this tool's own marker.</b> It is how a later run
/// recognises a database it seeded itself and completes what is missing instead of refusing —
/// see <c>Configuracion.Guardas</c>.
/// </para>
///
/// <para>
/// <b>There is no <c>ConfiguracionPrograma</c> here on purpose.</b> Its
/// <c>PorcentajeAcumulacion</c> is RN-01 and its <c>ObjetivoMensual</c> is RN-06 — per-business
/// numbers that ARCHITECTURE §6 forbids writing as literals in code, and that this tool has no
/// business inventing. No phase-1 screen needs one (accrual arrives with phase 2's import), so
/// the row is left to <c>Fidelizar.MigracionOctaviano --porcentaje-acumulacion</c>, which asks
/// for the real number and refuses to guess it.
/// </para>
/// </summary>
public static class DatosInventados
{
    public const string NegocioNombre = "Comercio de Prueba S.R.L.";

    /// <summary>All zeros: not a valid CUIT, so it can never collide with a real business. Also
    /// this tool's marker for "I seeded this database" (see <c>Guardas</c>).</summary>
    public const string NegocioCuit = "30-00000000-0";

    public const string NegocioDomicilio = "Calle Falsa 123, Localidad de Prueba, Provincia de Prueba";

    public const string CodigoSucursalCentro = "DEMO-CENTRO";

    public const string CodigoSucursalNorte = "DEMO-NORTE";

    /// <summary>The Dueño account, the one this whole tool exists for: without it there is no way
    /// to sign in and no way to create any other user (<c>POST /usuarios</c> is Dueño-only).</summary>
    public const string EmailDueno = "dueno@ejemplo.invalid";

    public static readonly IReadOnlyList<SucursalInventada> Sucursales =
    [
        new("Sucursal Centro (demo)", CodigoSucursalCentro),
        new("Sucursal Norte (demo)", CodigoSucursalNorte),
    ];

    /// <summary>
    /// Three accounts, not one: the Dueño is what the task requires, and the Encargada and Cajero
    /// are what makes the role-gated screens testable at all — a counter screen that only a Dueño
    /// ever opens is never exercised as a cashier sees it (ARCHITECTURE §8). All three are created
    /// with the same <c>FIDELIZAR_SEED_PASSWORD</c>, which is the operator's own value for that
    /// session and never a default.
    /// </summary>
    public static readonly IReadOnlyList<UsuarioInventado> Usuarios =
    [
        new("Dueño de Prueba", EmailDueno, RolUsuario.Dueno, CodigoSucursal: null),
        new("Encargada de Prueba", "encargada@ejemplo.invalid", RolUsuario.Encargada, CodigoSucursalCentro),
        new("Cajero de Prueba", "cajero@ejemplo.invalid", RolUsuario.Cajero, CodigoSucursalCentro),
    ];

    private const string MotivoSaldoInicial =
        "Saldo inicial de datos de demostración (seeder de desarrollo).";

    /// <summary>
    /// Six members, enough for S2 Buscar socio to return more than one candidate and for S3 Ficha
    /// and S6 Historial to show something other than an empty list, and few enough to read at a
    /// glance in psql.
    /// </summary>
    /// <param name="corte">
    /// The program's cutoff. Every <c>SaldoInicial</c> is dated here, exactly like a real padron
    /// import would date it.
    /// </param>
    /// <param name="hoy">
    /// Today. Redemptions hang off it so the history is always recent no matter when the seeder
    /// runs, and the first member's birthday is set to today's day and month so RN-11's birthday
    /// alert actually renders on S3 instead of being a code path nobody ever sees. Passed in
    /// rather than read from the clock so the fixture stays deterministic in tests.
    /// </param>
    public static IReadOnlyList<MiembroInventado> Miembros(DateOnly corte, DateOnly hoy)
    {
        // A --corte close to today would otherwise date a redemption before the cutoff.
        DateOnly Dia(int diasAntesDeHoy)
        {
            var fecha = hoy.AddDays(-diasAntesDeHoy);
            return fecha < corte ? corte : fecha;
        }

        return
        [
            new("DEMO-001", "D-001", "Ana Prueba", "+54 9 11 0000-0001", "99000001",
                new DateOnly(1988, hoy.Month, hoy.Day), CodigoSucursalCentro,
                [
                    new(TipoMovimientoCredito.SaldoInicial, corte, 1_500.00m, MotivoSaldoInicial),
                    new(TipoMovimientoCredito.Canje, Dia(21), -500.00m, "Canje de demostración en mostrador."),
                ]),

            new("DEMO-002", "D-002", "Bruno Ejemplo", "+54 9 11 0000-0002", "99000002",
                new DateOnly(1979, 3, 14), CodigoSucursalCentro,
                [
                    new(TipoMovimientoCredito.SaldoInicial, corte, 320.00m, MotivoSaldoInicial),
                ]),

            new("DEMO-003", "D-003", "Carla Ficticia", "+54 9 11 0000-0003", "99000003",
                new DateOnly(1995, 11, 2), CodigoSucursalNorte,
                [
                    new(TipoMovimientoCredito.SaldoInicial, corte, 7_250.50m, MotivoSaldoInicial),
                    new(TipoMovimientoCredito.Canje, Dia(45), -250.50m, "Canje de demostración en mostrador."),
                    new(TipoMovimientoCredito.Canje, Dia(9), -1_000.00m, "Canje de demostración en mostrador."),
                ]),

            new("DEMO-004", "D-004", "Diego Muestra", "+54 9 11 0000-0004", "99000004",
                new DateOnly(2001, 6, 30), CodigoSucursalNorte,
                [
                    new(TipoMovimientoCredito.SaldoInicial, corte, 45.75m, MotivoSaldoInicial),
                ]),

            // Ends at exactly $0: the balance a cashier has to be able to tell apart from "this
            // member does not exist", and the one S4 must refuse to redeem against (RN-24, I6).
            new("DEMO-005", "D-005", "Elena Testigo", "+54 9 11 0000-0005", "99000005",
                new DateOnly(1968, 1, 9), CodigoSucursalCentro,
                [
                    new(TipoMovimientoCredito.SaldoInicial, corte, 900.00m, MotivoSaldoInicial),
                    new(TipoMovimientoCredito.Canje, Dia(3), -900.00m, "Canje de demostración en mostrador."),
                ]),

            // Carries an Ajuste so S6 Historial shows the one and only way anything is ever
            // corrected here — a new line, never an edit (I1, I3).
            new("DEMO-006", "D-006", "Fabián Simulado", "+54 9 11 0000-0006", "99000006",
                new DateOnly(1983, 9, 21), CodigoSucursalNorte,
                [
                    new(TipoMovimientoCredito.SaldoInicial, corte, 12_000.00m, MotivoSaldoInicial),
                    new(TipoMovimientoCredito.Ajuste, Dia(14), -1_200.00m,
                        "Ajuste de demostración: corrección de un saldo inicial cargado de más."),
                ]),
        ];
    }
}

public sealed record SucursalInventada(string Nombre, string CodigoExterno);

public sealed record UsuarioInventado(
    string NombreCompleto, string Email, RolUsuario Rol, string? CodigoSucursal);

public sealed record MiembroInventado(
    string ClienteExternoId,
    string NumeroSocio,
    string Nombre,
    string Telefono,
    string Dni,
    DateOnly FechaNacimiento,
    string CodigoSucursal,
    IReadOnlyList<MovimientoInventado> Movimientos);

/// <summary>Every one of these carries a <c>Motivo</c>: they are all dated in the past, and a
/// retroactive movement requires one whatever its type (DATA-MODEL §4).</summary>
public sealed record MovimientoInventado(
    TipoMovimientoCredito Tipo, DateOnly FechaEfectiva, decimal Monto, string Motivo);
