using System.Text;

namespace Fidelizar.Domain.Texto;

/// <summary>
/// Member name normalisation, in one place. Used by the padron importer (F0-08) and by the
/// counter search (later phases): if the two normalised differently, the same member could be
/// registered twice or never be found.
///
/// <para>
/// Has to survive the source POS storing names as <c>"Ficticio (VC H), Prueba"</c> while the
/// club's own signup form has them as <c>"Prueba Ficticio"</c> — hence <see cref="ClaveComparable"/>
/// compares <b>sets of words</b>, not raw strings.
/// </para>
///
/// <para>
/// <b>I7 — this is for finding candidates, never for deciding they are the same member.</b>
/// Nothing here merges two <c>Miembro</c> rows automatically: <see cref="Normalizar"/> only
/// produces the value stored in <c>Miembro.NombreNormalizado</c> (an indexed column that feeds a
/// search — DATA-MODEL §3), and <see cref="ClaveComparable"/> only produces a comparison key. Both
/// are read-only text transforms with no side effects; nothing in this class looks up a member,
/// merges a record, or decides two people are the same. Whether two similarly-named members are
/// the same person is a call a human makes when shown both candidates (ARCHITECTURE I7).
/// </para>
/// </summary>
public static class VipNombres
{
    /// <summary>
    /// Lowercase, unaccented, without the source system's <c>(VC ...)</c> mark, without
    /// punctuation, with collapsed whitespace.
    /// </summary>
    public static string Normalizar(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return string.Empty;
        }

        var sinMarca = QuitarMarcaVip(nombre);
        var sinAcentos = QuitarAcentos(sinMarca).ToLowerInvariant();

        var limpio = new StringBuilder(sinAcentos.Length);
        foreach (var c in sinAcentos)
        {
            limpio.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ', limpio.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// The name's words, sorted, so two spellings can be compared without depending on whether
    /// the source wrote "Apellido, Nombre" or "Nombre Apellido".
    /// </summary>
    public static string ClaveComparable(string? nombre) =>
        string.Join(' ', Normalizar(nombre).Split(' ', StringSplitOptions.RemoveEmptyEntries).Order());

    /// <summary>
    /// Strips the source system's own club-membership mark from the name: the POS this business
    /// used stored members as <c>"Ficticio (VC H), Prueba"</c>, where the trailing letter is the
    /// branch where they were signed up. It is not always a bare <c>(VC)</c>, so this cuts from
    /// <c>(VC</c> to the closing parenthesis.
    /// </summary>
    public static string QuitarMarcaVip(string nombre)
    {
        var inicio = nombre.IndexOf("(VC", StringComparison.OrdinalIgnoreCase);
        if (inicio < 0)
        {
            return nombre;
        }

        var fin = nombre.IndexOf(')', inicio);
        return fin < 0
            ? nombre[..inicio]
            : nombre.Remove(inicio, fin - inicio + 1);
    }

    /// <summary>
    /// Explicit map from accented letters to their base letter.
    ///
    /// <b>Deliberately not <c>String.Normalize(FormD)</c>.</b> This kind of project tends to be
    /// compiled with <c>InvariantGlobalization=true</c>, and in that mode <c>Normalize</c> returns
    /// the string untouched instead of decomposing accents: "Crédito" would never reach "credito".
    /// The bug is especially treacherous because the test project does NOT carry that flag, so
    /// tests stayed green while the deployed binary genuinely failed — Octaviano only found this
    /// by running the import by hand. With an explicit map the result is identical in both modes.
    /// </summary>
    private static readonly Dictionary<char, char> Acentos = new()
    {
        ['á'] = 'a', ['à'] = 'a', ['ä'] = 'a', ['â'] = 'a', ['ã'] = 'a', ['å'] = 'a',
        ['é'] = 'e', ['è'] = 'e', ['ë'] = 'e', ['ê'] = 'e',
        ['í'] = 'i', ['ì'] = 'i', ['ï'] = 'i', ['î'] = 'i',
        ['ó'] = 'o', ['ò'] = 'o', ['ö'] = 'o', ['ô'] = 'o', ['õ'] = 'o',
        ['ú'] = 'u', ['ù'] = 'u', ['ü'] = 'u', ['û'] = 'u',
        ['ñ'] = 'n', ['ç'] = 'c', ['ý'] = 'y',
    };

    private static string QuitarAcentos(string texto)
    {
        var sb = new StringBuilder(texto.Length);

        foreach (var c in texto)
        {
            // Lowercased first so the map does not need to be duplicated for uppercase letters.
            var minuscula = char.ToLowerInvariant(c);
            sb.Append(Acentos.TryGetValue(minuscula, out var sinAcento) ? sinAcento : minuscula);
        }

        return sb.ToString();
    }
}
