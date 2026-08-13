# Example infrastructure files

**Nothing here is real, and nothing here is used by anything.** These are annotated templates the
owner copies and fills in on the server. The real `compose.yml`, `Caddyfile` and `.env` live only
on the deployment host and are never committed (CLAUDE.md).

Every value that must change is written as `CAMBIAR_ESTO`. That is deliberate: a plausible-looking
fake secret is worse than an obvious placeholder, because it survives a careless copy-paste and
nobody notices until it is in production.

| File | What it is |
| --- | --- |
| [compose.ejemplo.yml](compose.ejemplo.yml) | The three services one business needs: the API, its database, and the nightly backup |
| [caddyfile.ejemplo](caddyfile.ejemplo) | Reverse proxy with automatic HTTPS, one site per business |
| [variables-entorno.ejemplo.txt](variables-entorno.ejemplo.txt) | Every environment variable the API reads, and which are mandatory |

## The shape this assumes

One deployment per business: its own container, its own database (ARCHITECTURE §5). That makes
cross-tenant leakage physically impossible and backup, restore and offboarding trivial, at the
cost of doing everything N times. These files are written so that cost stays flat — the same
three files with a different domain and a different volume name.

The API serves the compiled WebAssembly client from its own origin, so there is one container to
run and no CORS to configure (ARCHITECTURE §3).

## Before the first deploy

- **Region.** The server goes in a datacenter close to Argentina — São Paulo or an Argentine VPS.
  Not the cheapest European region: the counter flow is meant to finish in under 15 seconds and
  each lookup is a round trip (ARCHITECTURE §14).
- **The signing key is generated on the server**, never here and never in the repository. A key
  that touches git is burned permanently.
- **Restore the backup once before going live.** A backup that has never been restored is a
  hypothesis, not a backup (ARCHITECTURE §14, ROADMAP F0-15).
