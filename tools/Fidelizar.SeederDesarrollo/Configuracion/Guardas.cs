using Fidelizar.Domain.Entities;
using Fidelizar.SeederDesarrollo.Datos;
using Fidelizar.SeederDesarrollo.Destino;

namespace Fidelizar.SeederDesarrollo.Configuracion;

/// <summary>
/// The rails that decide whether this tool is allowed to write anything at all. Pure functions,
/// so every refusal is a test and not a hope.
///
/// <para>
/// The reason they are this paranoid: on this machine there is a development Postgres and, on a
/// different port, another one holding the real balances of 293 members. A seeder that wrote into
/// the second one because an environment variable was left over from another session would be
/// unrecoverable in the only way that matters — the ledger is append-only (I1), so there is no
/// undo. These functions are written assuming that mistake will be made again.
/// </para>
/// </summary>
public static class Guardas
{
    /// <summary>
    /// Database names this tool refuses outright, with no override flag anywhere. <c>gate</c> is
    /// the migration/verification database holding real member balances; <c>prod</c> and
    /// <c>octaviano</c> are the other two names a real database could plausibly carry here.
    /// Matched as substrings and case-insensitively, so <c>fidelizar_gate</c>,
    /// <c>FIDELIZAR_GATE_2</c> and <c>gate_backup</c> are all refused.
    /// </summary>
    public static readonly IReadOnlyList<string> NombresProhibidos = ["gate", "prod", "octaviano"];

    /// <summary>The refusal message when <paramref name="baseDeDatos"/> is a name this tool must
    /// never write to, or null when the name is acceptable.</summary>
    public static string? NombreProhibido(string baseDeDatos)
    {
        var prohibido = NombresProhibidos.FirstOrDefault(
            n => baseDeDatos.Contains(n, StringComparison.OrdinalIgnoreCase));

        if (prohibido is null)
        {
            return null;
        }

        return $"""
            La base '{baseDeDatos}' contiene '{prohibido}' y esta herramienta se niega a escribir
            en ella. Sin excepción y sin bandera que lo habilite.

            'gate' es la base con los saldos reales de los 293 socios, y el ledger es append-only
            (I1): lo que se escribe ahí no se borra nunca. Si de verdad necesitás sembrar datos de
            demostración en una base con uno de estos nombres, renombrá la base — no esta
            herramienta.

            No se escribió nada.
            """;
    }

    /// <summary>What to do with a target database that already has rows in it.</summary>
    /// <param name="conteo">What the database currently holds.</param>
    /// <param name="negocioExistente">
    /// Its first <c>Negocio</c> row, or null when there is none. Its <c>Cuit</c> is how a database
    /// this same tool seeded is told apart from any other: nothing but this seeder writes
    /// <see cref="DatosInventados.NegocioCuit"/>.
    /// </param>
    /// <param name="permitirBaseNoVacia">Whether <c>--permitir-base-no-vacia</c> was passed.</param>
    public static DecisionSobreBase DecidirSobreBase(
        ConteoBase conteo, Negocio? negocioExistente, bool permitirBaseNoVacia)
    {
        if (conteo.EstaVacia)
        {
            return new DecisionSobreBase(true, "La base está vacía. Se siembra todo.");
        }

        var esNuestra = conteo.Negocios == 1
            && negocioExistente is not null
            && negocioExistente.Cuit == DatosInventados.NegocioCuit;

        if (esNuestra)
        {
            return new DecisionSobreBase(
                true,
                $"""
                La base ya fue sembrada por esta herramienta (Negocio '{negocioExistente!.Nombre}',
                CUIT {DatosInventados.NegocioCuit}). El seeder es idempotente: completa lo que
                falte y no duplica ni pisa nada de lo que ya está.
                """);
        }

        if (permitirBaseNoVacia)
        {
            return new DecisionSobreBase(
                true,
                """
                La base NO está vacía y NO es una que esta herramienta haya sembrado. Se continúa
                únicamente porque pasaste --permitir-base-no-vacia. No se borra ni se modifica
                nada existente: el seeder solo agrega lo que falta.
                """);
        }

        return new DecisionSobreBase(
            false,
            $"""
            La base no está vacía y no es una que esta herramienta haya sembrado.

              Negocios: {conteo.Negocios}   Sucursales: {conteo.Sucursales}   Usuarios: {conteo.Usuarios}
              Miembros: {conteo.Miembros}   Movimientos de crédito: {conteo.Movimientos}

            No se escribió nada. Si es la base que querés y sabés lo que hay adentro, volvé a
            correr con --permitir-base-no-vacia. Si estos números te sorprenden, la variable de
            entorno está apuntando a otra base: revisala antes de insistir.
            """);
    }
}

/// <summary>Whether the seeder may write, and the sentence explaining why — printed either way,
/// so the operator always reads what the tool concluded about the target database.</summary>
public sealed record DecisionSobreBase(bool Continuar, string Mensaje);
