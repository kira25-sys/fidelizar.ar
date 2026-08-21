namespace Fidelizar.Api.Options;

/// <summary>
/// Bound once from the "Monitoreo" configuration section (ARCHITECTURE §14). Nothing here is a
/// secret — it is the identity and the retention of one deployment — so, unlike
/// <c>Jwt:SigningKey</c>, it has defaults. See docs/OPERACION-MONITOREO.md.
/// </summary>
public sealed class MonitoreoSettings
{
    public const string SeccionConfiguracion = "Monitoreo";

    /// <summary>
    /// Which deployment this is, e.g. <c>octaviano</c>. One VPS hosts every client (§14), so an
    /// alert or a log line without this does not say whose shop is down. The default is
    /// deliberately not plausible: an alert reading "sin-configurar" is a bug report.
    /// Env var: <c>Monitoreo__Instancia</c>.
    /// </summary>
    public string Instancia { get; set; } = "sin-configurar";

    /// <summary>
    /// Rolling-file path template for the JSON log. Points at the volume the client's logs are
    /// retained on. Env var: <c>Monitoreo__RutaArchivoLog</c>.
    /// </summary>
    public string RutaArchivoLog { get; set; } = "Logs/fidelizar-.log";

    /// <summary>
    /// How many daily files are kept — "structured logs, retained per client" (§14). One file per
    /// day, so this is a number of days.
    /// </summary>
    public int RetencionDias { get; set; } = 31;

    /// <summary>
    /// Cap per file, rolled rather than truncated. One VPS hosts every client, so one instance
    /// logging in a loop must not fill the disk out from under the others.
    /// </summary>
    public long TamanoMaximoArchivoBytes { get; set; } = 50L * 1024 * 1024;
}
