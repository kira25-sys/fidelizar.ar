# Operación: uptime y logs

How an instance is watched and what it writes down (ROADMAP F1-18, ARCHITECTURE §14).

One VPS hosts every client, so both halves of this document exist to answer the same question:
**whose shop is affected.**

---

## 1. The two probes

Both are anonymous — an external uptime service cannot log in — and both answer the same JSON
shape.

| Route | Runs | Answers |
| --- | --- | --- |
| `/health` and `/health/live` | no check at all | is the process answering HTTP? |
| `/health/ready` | the `base-de-datos` check | is the process up **and** Postgres responding? |

```json
{ "estado": "Healthy", "instancia": "octaviano", "chequeos": { "base-de-datos": "Healthy" } }
```

`/health/live` deliberately keeps answering while the database is down. That is the whole point of
having two: **liveness up + readiness down means Postgres died; both down means the host died**,
and those two are not the same emergency.

The body carries status and instance and nothing else — never an exception, a stack trace or a
connection string. Both routes are public.

## 2. Configuration

Section `Monitoreo` in `appsettings.json`, overridable per environment. **Nothing here is a
secret** — it is the identity and the retention of one deployment — so unlike `Jwt:SigningKey`
these do have defaults.

| Key | Env var | Default | What it is |
| --- | --- | --- | --- |
| `Monitoreo:Instancia` | `Monitoreo__Instancia` | `sin-configurar` | Which deployment this is, e.g. `octaviano` |
| `Monitoreo:RutaArchivoLog` | `Monitoreo__RutaArchivoLog` | `Logs/fidelizar-.log` | Rolling-file template for the JSON log |
| `Monitoreo:RetencionDias` | `Monitoreo__RetencionDias` | `31` | Daily files kept |
| `Monitoreo:TamanoMaximoArchivoBytes` | `Monitoreo__TamanoMaximoArchivoBytes` | `52428800` | Cap per file, rolled and not truncated |

**Set `Instancia` on every deployment.** The default is implausible on purpose: an alert that
reads `sin-configurar` is itself a bug report. And the size cap is not decoration — one VPS hosts
every client, so one instance logging in a loop must not fill the disk out from under the others.

## 3. Logs

Structured JSON, one rolling file per day, every line carrying `Instancia` so a line is always
attributable to a business.

**What never goes into a log: a member's name, phone, DNI or email.** A member is an id. This is
CLAUDE.md, and `LogsEstructuradosTests` is what keeps it true rather than aspirational — if a
future log line starts carrying a name, that test fails.

Retention is per client because the files are per instance; there is no shared log anyone would
have to filter.

## 4. The uptime check itself

The application exposes the probes; **something outside has to poll them**, because a process that
died cannot report that it died. Point an external monitor at `/health/ready` for each instance.

What to configure, whatever service ends up being used:

- **One check per instance**, each labelled with the same name as `Monitoreo:Instancia`.
- **`/health/ready`**, not `/health` — a process that is up but cannot reach Postgres serves no
  cashier, and answering `200` on `/health` alone would hide exactly that.
- Alert on **two consecutive failures**, not one: a single missed poll on a shop's connection is
  normal (ARCHITECTURE §14 assumes a flaky link, which is why phase 1 is built the way it is).

## 5. Open decision — which service sends the alert

**Not taken. It is the owner's.**

`IAlertaOperativa` is the seam. Today the only implementation, `AlertaOperativaEnLog`, writes one
structured `Error` line carrying the instance. That is a real audit trail and a real call site,
but **nobody's phone rings.** A sender replaces that implementation without touching a caller.

The choice was left open because it is not a technical toss-up — it decides who pays for what and
what has to be trusted with a credential:

| Option | What it costs | What it implies |
| --- | --- | --- |
| **External uptime service** (UptimeRobot, Better Stack and similar) | Free tiers usually cover a handful of checks | It polls from outside, so it also catches "the whole VPS is gone" — the case an in-process alerter can never report. **Recommended starting point.** |
| **WhatsApp / Telegram from the application** | A bot token or a Business API account | Reaches the phone the owner already looks at. But an alerter living inside the process cannot alert about that process being dead |
| **Email** | Almost nothing | Slowest to notice; a counter outage measured in hours is not an outage anyone acted on |

The two are not exclusive, and the honest combination is **external polling for "is it up",
in-process `IAlertaOperativa` for "something specific went wrong inside"**.

Whichever is chosen, its credential is a secret: it goes in the owner's environment or user
secrets, **never in a file in this repository** (CLAUDE.md). An example lives in
[`infra-ejemplo/variables-entorno.ejemplo.txt`](infra-ejemplo/variables-entorno.ejemplo.txt) with
an obvious placeholder.
