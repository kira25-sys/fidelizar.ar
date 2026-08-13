# The restore drill

> Example only — commands here use `CAMBIAR_ESTO` placeholders for the values that come from
> the real `compose.yml` / `.env` on the server (never in this repository). See
> [compose.ejemplo.yml](compose.ejemplo.yml) for where those services and volumes come from.

**A backup that has never been restored is a hypothesis, not a backup** (ARCHITECTURE §14). This
drill is mandatory once before the phase 1 go-live, and **repeats periodically afterwards** — the
same steps, run again, not a one-off. F0-15 (ROADMAP) ran it once against the 293-member database
to prove the mechanism; this document is how the owner repeats it on the real server.

The drill has four parts: back up, restore into a database that never held the original data,
verify balances against the source, and check that the schema itself — not just the rows — came
through intact.

## 1. Back up

Same mechanism the `backup` service in `compose.ejemplo.yml` already runs daily — this drill
exercises that exact path, not a substitute invented for the occasion:

```sh
docker exec CAMBIAR_ESTO-db sh -c \
  'pg_dump -U CAMBIAR_ESTO -d CAMBIAR_ESTO | gzip' > backups/fidelizar-drill.sql.gz
```

(`CAMBIAR_ESTO-db` is the compose service name — e.g. `fidelizar-lacentral-db-1`.) This is a
plain, uncompressed-by-`pg_dump` SQL script piped through `gzip`, exactly what the `backup`
service's entrypoint produces. It dumps the database's objects and data — not a `CREATE DATABASE`
statement — so restoring it means creating an empty target database first, then loading the
script into it.

Confirm the file is a valid gzip archive before trusting it:

```sh
gzip -t backups/fidelizar-drill.sql.gz && echo OK
```

## 2. Restore into a clean database

**Clean means clean: a new container, a new volume, never the original.** Reusing the source
database or truncating its tables proves nothing — the point is the path a real disaster
recovery takes, starting from nothing.

```sh
docker volume create fidelizar-restore-drill-data

docker run -d --name fidelizar-restore-drill \
  -e POSTGRES_DB=CAMBIAR_ESTO \
  -e POSTGRES_USER=CAMBIAR_ESTO \
  -e POSTGRES_PASSWORD=CAMBIAR_ESTO \
  -p 5434:5432 \
  -v fidelizar-restore-drill-data:/var/lib/postgresql/data \
  postgres:17
```

Wait for it to accept connections (`docker exec fidelizar-restore-drill pg_isready -U
CAMBIAR_ESTO`), then confirm it is actually empty — `\dt` inside `psql` should list no relations.
If it lists anything, the container or volume was not new; stop and start over.

## 3. Load the dump

```sh
gunzip -c backups/fidelizar-drill.sql.gz | \
  docker exec -i fidelizar-restore-drill psql -U CAMBIAR_ESTO -d CAMBIAR_ESTO -v ON_ERROR_STOP=1
```

`ON_ERROR_STOP=1` is not optional: without it `psql` keeps going after a failed statement and a
partially-loaded database can look complete at a glance. Exit code must be `0` and there must be
no `ERROR` lines in the output. A dump that fails to load cleanly means the backup mechanism
itself is broken — stop here and fix that before anything else, because a backup that cannot be
loaded is not a backup at all, restored or not.

## 4. Verify balances against the source

Run the same three-way verification harness the phase 0 gate uses (ROADMAP F0-11), pointed at
the restored container instead of the live one:

```sh
dotnet run --project tools/Fidelizar.VerificacionGate -- \
  --sqlite <ruta a la fuente que corresponda> \
  --planilla <ruta a la fuente que corresponda> \
  --pg-host localhost --pg-port 5434 --pg-db CAMBIAR_ESTO --pg-user CAMBIAR_ESTO \
  --salida <ruta fuera del repositorio>
```

(`FIDELIZAR_GATE_PG_PASSWORD` still has to be set in the environment — the harness never takes a
password on the command line.) The report's verdict line says `GATE CUMPLIDO` only when every
member's balance in the restored database matches the source, to the peso, with zero
discrepancies. **Anything else — even a single peso, even a single member — means the drill
failed.** A partial match is not a pass.

Once a business is live, "the source" is the business's own paying-client record set, not
Octaviano — the harness's three-way comparison was specific to the phase 0 migration. Post
phase-0, verifying a restore means comparing row counts and the ledger's `SUM(Monto)` per member
in the restored database against the same query run on the live database right before the backup
was taken.

## 5. Check the schema came through, not just the rows

Row counts alone are not enough: a restore can bring every row back and still lose a constraint.
**A restored database missing a unique index is a restore that looks fine and lets the same sale
get credited twice** — the failure would not show up until it happened in production. Check,
against the restored database:

- Every table has its expected row count (compare against a count taken on the source right
  before the backup).
- `SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = 'public'` returns the same set
  of indexes as the source, name for name and definition for definition. Three are load-bearing
  because they are what turn a business rule into something the database itself refuses to
  violate, not just something the application layer is expected to remember:
  - The partial unique index on `ConfiguracionesPrograma` (`NegocioId` where `VigenteHasta IS
    NULL`) — exactly one current program configuration per business.
  - The unique index on `Cortes.NegocioId` — exactly one cutoff per business.
  - The partial unique index on `MovimientosCredito` (`NegocioId, MiembroId, Tipo,
    ReferenciaVenta` where `Tipo` is `Acumulacion`) — the same sale can never be credited twice.
- The money columns are still `numeric(14,2)` (`SELECT table_name, column_name, numeric_precision,
  numeric_scale FROM information_schema.columns WHERE data_type = 'numeric'`) — a restore that
  silently widens or narrows precision is a restore that changes what a balance means.

## When it fails

A failed drill is not a footnote — it means the daily backup the business is relying on would
not have saved it. Do not retry quietly and report success on the second attempt: report the
first failure, what it lost (a row count mismatch, a missing index, a balance off by any amount),
and only then investigate the cause. The next real disaster will not offer a second attempt.

## Cleanup

The restore container and volume created for the drill are scaffolding, not infrastructure —
destroy them when the drill is done:

```sh
docker rm -f fidelizar-restore-drill
docker volume rm fidelizar-restore-drill-data
```

If the dump file held real member data (as it does for the periodic drill against a live
business database), delete it too. It has no reason to outlive the exercise that produced it.
