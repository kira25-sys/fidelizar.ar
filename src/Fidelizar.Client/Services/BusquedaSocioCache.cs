using Fidelizar.Shared.Miembros;

namespace Fidelizar.Client.Services;

/// <summary>
/// S2's last query and result list, kept in memory for the browser session (FLOW-S2-S5.md
/// §1.6): a Narrow/Medium round trip to S3 and back must not force a re-type or a second
/// search — pressing back restores S2 exactly as it was. Registered scoped in Program.cs,
/// which in Blazor WebAssembly means one instance for the whole app session (no server-side
/// scope boundary), so it survives the S2 component being disposed and recreated across a
/// route change.
///
/// Cleared only when the cashier explicitly starts over: a member not found (FLOW-S2-S5.md
/// §2.4, "a different search was clearly needed"). Never cleared on a timer.
/// </summary>
public sealed class BusquedaSocioCache
{
    public string Query { get; private set; } = string.Empty;
    public IReadOnlyList<MiembroResumen>? Resultados { get; private set; }

    public void Set(string query, IReadOnlyList<MiembroResumen> resultados)
    {
        Query = query;
        Resultados = resultados;
    }

    public void Clear()
    {
        Query = string.Empty;
        Resultados = null;
    }
}
