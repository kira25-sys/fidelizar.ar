# Architecture — Fidelizar

The technical contract. [Plan-fidelizacion.md](Plan-fidelizacion.md) says *why*; this file says
*how*. Section references like "Plan §4" point there.

Deviating from this document requires the owner's approval. Ask first.

---

## 1. What this product is

A loyalty program sold to independent retail businesses. Each business runs its own deployment.
Cashiers use a web counter app to look up a member, see their balance and alerts, and register
a redemption. Sales data arrives by file import (later by API), never by direct connection to
the business's own POS database.

**The core promise is trust in a number.** The balance a cashier reads must be defensible to
the peso. Everything else in this document exists to protect that.

## 2. Stack

| Concern | Choice | Why |
| --- | --- | --- |
| Runtime | .NET 10 (`net10.0`) | Continuity with the ported code; SDK already in use |
| Backend | ASP.NET Core **Web API (REST)** | A monolith in layers, not microservices. The backend is a contract, not a UI detail: the frontend can be replaced without touching it |
| Frontend | **Blazor WebAssembly** | One language across the stack, DTOs and validation shared with the API through `Shared`. No persistent connection, so a flaky counter WiFi does not take the screen down |
| Database | **PostgreSQL** | Concurrent writes from web, analytical queries over ticket lines, per-business backup and restore (Plan §4) |
| ORM | Entity Framework Core + Npgsql | Migrations are the schema's source of truth |
| Reverse proxy | Caddy | Automatic HTTPS, no configuration |
| Tests | xUnit | |

No SQLite. No JavaScript SPA framework. No microservices — the backend is a **monolith in
layers**. No shared package with Octaviano (Plan §1) — code is copied, not shared.

Adding any other dependency requires approval.

### Why the API and the client are separate (decided 2026-08-12)

The earlier plan was Blazor Server: one process, no API, fastest to build. It was replaced for
three reasons, in the owner's own words:

1. **The backend becomes an asset independent of the frontend.** Once the API works, the client
   can be swapped — for React, for a mobile app, for whatever comes — without reopening the
   backend.
2. **Backend and frontend work can be scheduled separately**, which matters when one person does
   both: the API can be finished and tested on its own before any screen exists.
3. **No persistent connection.** Blazor Server sends every UI interaction over a SignalR circuit;
   a flaky counter WiFi drops the circuit and the screen. WebAssembly runs in the browser and
   only talks to the API when it has something to say.

The cost, accepted knowingly: roughly a third more work than Blazor Server, and two artifacts
instead of one. Blazor WebAssembly rather than React/TypeScript keeps that cost as low as a split
allows — one language, and DTOs plus validation shared through `Shared` instead of written twice.

## 3. Solution layout

```
Fidelizar.sln
src/
  Fidelizar.Domain/          entities, business rules (RN), repository & service interfaces
  Fidelizar.Application/     use cases: balance, accrual, redemption, import orchestration
  Fidelizar.Infrastructure/  EF Core, repositories, migrations, POS adapters, importers
  Fidelizar.Shared/          DTOs and validation shared by API and client. No logic, no EF
  Fidelizar.Api/             REST controllers, auth, middleware, DI composition root. Hosts the client
  Fidelizar.Client/          Blazor WebAssembly: pages, components, API client
tests/
  Fidelizar.Domain.Tests/
  Fidelizar.Application.Tests/
  Fidelizar.Infrastructure.Tests/
  Fidelizar.Api.Tests/       endpoint contract and authorisation tests
docs/
  infra-ejemplo/             example infrastructure files, never the real ones
```

**Dependency direction, enforced without exception:**

```
Client  →  Shared  ←  Api  →  Application  →  Domain
                       ↓                        ↑
                  Infrastructure  ──────────────┘

Domain depends on nothing.
```

`Api` references `Infrastructure` only to wire up DI at the composition root — never to call a
repository directly. A controller talks to `Application`, never to `Infrastructure`.

`Domain` holds no EF Core attributes, no `DbContext`, no SQL, no HTTP, no file I/O. It defines
the interfaces that `Infrastructure` implements. A rule that lives in `Domain` must be testable
with no database.

`Application` holds the use cases — register a redemption, import a batch, compute a balance —
orchestrating `Domain` and the repository interfaces. It knows nothing about HTTP or EF either.

**`Client` never references `Domain`, `Application` or `Infrastructure`.** Everything the client
compiles is downloaded to the browser at the counter: domain rules, connection strings and
entity internals must not be shippable. The client sees `Shared` and nothing else — treat any
other reference from `Client` as a defect, not a shortcut.

`Shared` holds DTOs and their validation attributes. No entities, no business rules, no EF: a
rule that matters lives in `Domain` and is enforced server-side. Client-side validation exists to
give the cashier a fast message, never to decide anything.

### `CrossCutting` is deliberately absent

This is Clean Architecture with dependency inversion, the same shape as the familiar
`Api / Application / Domain / Data / CrossCutting` split, with one departure: **no `CrossCutting`
project.** It is the layer that starts as logging and mapping and, months later, holds business
logic nobody can account for. Exception middleware, logging and the error contract belong to
`Api`, where they are used; shared exception types belong to `Domain`.

### No generic repository (decided 2026-08-12)

A generic `IPersistence` with `GetAll<T>`, `Update<T>` and `Delete<T>` over a common entity base
is a common and reasonable pattern. **It is incompatible with this product** and must not be
ported:

- I1 says the ledger is never updated and never deleted. A generic `Delete<T>` makes that
  unenforceable by construction — any caller can delete a `MovimientoCredito`, and no test can
  prove otherwise.
- §5 says every query filters by `NegocioId`. A generic `GetAll<T>` has nowhere to put that
  filter, so the rule degrades into a thing everyone must remember.

Instead: **one repository interface per aggregate**, exposing only the operations that aggregate
legitimately supports. `IMovimientoRepository` offers `Append` and queries — there is no delete
method to call, and `NegocioId` is a required parameter, not a convention.

### One deployable unit

`Api` serves the compiled WebAssembly client as static files from the same origin. One
container, one port, one Caddy site per business — deploy-per-client (§5) stays exactly as
simple as it was, and there is no CORS to configure.

**Domain vocabulary stays Spanish** (`Miembro`, `Movimiento`, `Canje`, `Saldo`, `Negocio`,
`Sucursal`, `Acumulacion`). Everything else — comments, technical identifiers, docs — is English.

## 4. Invariants — the things that are never negotiable

These are not preferences. Code that breaks one of these is rejected, however convenient.

**I1 — The ledger is append-only.** A `MovimientoCredito` row is never edited and never deleted.
There is no `UPDATE` and no `DELETE` against that table, anywhere, for any reason.

**I2 — Balance is always `SUM(Monto)`.** It is never read from a stored column. `SaldoResultante`
exists on each movement as *historical evidence* of what the balance was at that moment; it is
never the source of an answer.

**I3 — Every correction is a new movement** of type `Ajuste`, with a mandatory `Motivo` and the
identity of whoever made it. Reversing a mistake means adding a line, not removing one.

**I4 — Money is `decimal`. Never `double`, never `float`.** Rounding happens in exactly one
place, to 2 decimals, `MidpointRounding.AwayFromZero` — so the result matches what a person
gets computing it by hand on the spreadsheet the system is verified against (Plan §6).

**I5 — An ambiguous amount is rejected, never guessed.** `MontoParser` distinguishes a
hand-typed amount from a POS-exported one and refuses the ambiguous case. Port it unchanged.
With CSVs from unknown POS systems this behaviour is the difference between a wrong balance and
a clear error.

**I6 — A redemption never exceeds the available balance** (RN-24). A larger amount is a typo and
is rejected. **No human action can ever produce a negative balance.** The single exception is a
system-generated `Ajuste` — a credited sale voided after the member already redeemed it — and
while the balance is negative, further redemptions are blocked and the manager is notified
(RN-25).

**I7 — Ambiguous identity is resolved by a human.** Two members with similar names are both
shown so the cashier picks one. The system never guesses which — guessing credits money to the
wrong person (Plan §5).

**I8 — `NegocioId` is on every table from day one**, including the first migration, even though
each client gets its own database. Migrating to a shared database later becomes an operations
decision instead of a rewrite. The reverse does not work (Plan §4).

**I9 — No direct connection to a client's POS database.** Ingestion is by file or API, always.
Inherited from Octaviano's `NoDirectMySqlAccessTests` policy (Plan §13) and enforced by an
equivalent test here.

**I10 — Sensitive fields require recorded consent.** Without a consent record, diet, allergy
and health-adjacent fields cannot be written at all (Plan §7).

## 5. Multi-tenancy

One deployment per business: its own container, its own database (Plan §4). This makes
cross-tenant leakage physically impossible and makes backup, restore and offboarding trivial.

Even so: `NegocioId` on every table (I8), and every query filters by it. Treat a missing
`NegocioId` filter as a defect even when the database holds a single business — that forgotten
`WHERE` is the failure mode that would end the product.

## 6. Configuration vs. constants

Octaviano hard-coded the program's numbers because it served one shop. A product cannot
(Plan §6).

| Rule | Where it lives |
| --- | --- |
| Accrual percentage | Per-business configuration |
| Monthly target | Per-business configuration, optional — some programs have none |
| "Paid" sale status | **Property of the POS adapter**, not of the engine |
| Program cutoff date | One per business, declared at import, persisted |
| Grace / expiry by inactivity | Configurable, **off by default** |
| Rounding of credit | **Fixed.** 2 decimals, `AwayFromZero` (I4) |

Never configurable: I1–I10. Those are the product's trust core, not a customer preference.

A business rule number must never appear as a literal in code. If you find `0.03` or `120000`
written inline anywhere, it is a defect.

## 7. Ingestion — the competitive advantage

```
POS-specific adapter  →  canonical format  →  engine
```

Each client business runs a different point-of-sale system. Only the adapter knows about that
system's quirks (column names, status codes, encodings, date formats). Everything downstream
sees one canonical shape.

**The manual CSV and the future API share that canonical format** (Plan §3). While no API
exists the owner exports by hand — fine — but the importer reads the canonical format from day
one. A "temporary" format means rewriting half the product when the API arrives.

**Sales must carry line detail**, not just totals:

| Today in Octaviano | Required here |
| --- | --- |
| one row per sale | one row per **product** per sale |
| `id, customer_id, sale_date, total, status` | `+ producto, cantidad, precio_unitario, subtotal` |
| enough for: balance | enough for: balance + habits + preferences + recommendations |

Without line detail the product has nothing to sell beyond a balance (Plan §3). Phase 2 exists
for this and is where the product actually begins.

**Preferences do not come from the POS.** Diet, allergies, tastes and birthdays are entered by
the member or the cashier. That module depends on nobody's API and can be built independently.

## 8. Identity and permissions

Real users with roles, replacing Octaviano's list of Telegram chat IDs (Plan §5).

| Role | Can | Cannot |
| --- | --- | --- |
| **Cajero** | Search a member, see balance and alerts, register a redemption | List all members, see phone/DNI, void movements, see other branches |
| **Encargada** | Everything a cashier can + full record, history, voids, branch reports | Configure the program, create users |
| **Dueño** | Everything, all branches, configuration and global reports | — |
| **Soporte** | Audited technical access | — |

Two privacy decisions that are part of the architecture, not of the UI:

1. **The cashier searches; the cashier does not browse.** There is no "all members" screen. A
   scrollable list of hundreds of people with phone numbers and ID numbers is a leak waiting to
   happen and adds nothing at the counter.
2. **The cashier sees name + balance + alerts. Nothing else.** Phone, DNI and full date of birth
   are for `Encargada` and `Dueno`.

Every movement records the real person who caused it. `usuario` is never `telegram:123456`.

### Authentication: a JWT carried in an `HttpOnly` cookie

Decided 2026-08-12. The two mechanisms are combined rather than chosen between, and each does
what it is good at:

- **The token is a JWT.** Signed, with claims and an expiry the API validates on its own — no
  server-side session store, and the same token would work unchanged if the client ever moved
  off this origin.
- **It travels in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie**, not in `localStorage` and
  not in an `Authorization` header set by JavaScript. A token in `localStorage` is readable by
  any script that reaches the page; an `HttpOnly` cookie is not.

`JwtBearer` is configured to read the token from the cookie (`OnMessageReceived`) instead of the
header. Everything downstream — `[Authorize]`, policies, roles — is unchanged.

**The cost of cookies is CSRF**, and it is paid explicitly: the browser attaches the cookie to
any request it makes, including one triggered by another site. `SameSite=Strict` closes the
common case (the app has no external redirect flows to break), and **every state-changing
endpoint additionally requires an antiforgery token**. A `POST` that registers a redemption and
relies on `SameSite` alone is a defect.

Practical requirements, all of them non-negotiable:

- The signing key comes from an environment variable or user secrets. **It is never committed**,
  not even to a development settings file — a key in the repository is a key in the history
  forever.
- The key is validated **at startup**: a missing or short key stops the application from
  starting, rather than throwing on the first login attempt.
- Token lifetime is short and renewal is silent, so a cashier is never logged out mid-flow.
- Expiry is computed in **UTC** (`DateTime.UtcNow`). Local time here shifts every token's
  lifetime by the machine's offset.

**Authorisation is decided server-side, on every endpoint** (Plan §5). The client hides what a
role cannot use, but hiding a button is a courtesy, not a control: an endpoint must reject a
cashier asking for a phone number even when no screen offers it. The permission matrix tests
(ROADMAP F1-15) run against the API, not against the UI.

**Login endpoints are rate-limited.** A counter login is a password endpoint exposed to the
internet; without a limit it is a free brute-force target. Verify the limiter is actually in the
pipeline, not merely registered in DI — a rate limiter configured but never added to the request
pipeline protects nothing while looking like it does.

## 9. Personal and sensitive data

Diet and allergies are health data. Argentine Law 25.326 classifies them as sensitive and
requires express, informed consent (Plan §7).

Built from phase 1, never retrofitted:

- Explicit consent checkbox at member registration, storing the date and who recorded it.
- A member can request deletion and an export of their data.
- With no consent record, sensitive fields are not written (I10).

## 10. What is deliberately not built

- **Offline synchronisation.** Every branch has internet. Instead: allow registering a
  redemption with a **past date** plus a mandatory reason. Power cut → written on paper → loaded
  later with the real date. The ledger already separates the movement's date from when it was
  recorded, so the model supports it unchanged (Plan §9).
- **WhatsApp notifications** in the early phases. The cashier's screen costs nothing, needs no
  approvals and converts better at the counter. WhatsApp is a later premium tier (Plan §8).
- **A shared library with Octaviano** (Plan §1).

## 11. Testing

Every business rule gets a test that names its RN number. Every invariant in §4 gets a test that
fails if the invariant is broken.

Required before any merge:

- `dotnet build` with no warnings introduced by the change
- `dotnet test` green
- `Domain` and `Application` tests run with no database

Specific tests that must exist and must never be deleted:

- Balance equals the sum of movements after an arbitrary sequence of operations (I2)
- A redemption above the balance is rejected (I6, RN-24)
- A correction produces an `Ajuste` and leaves the original row untouched (I1, I3)
- An ambiguous amount is rejected (I5)
- No code path opens a connection to an external POS database (I9)
- Sensitive fields cannot be written without a consent record (I10)
- Every endpoint rejects a role that must not reach it, called directly and ignoring the UI (§8)
- `Client` references only `Shared` (§3)
- No repository interface exposes a delete on the ledger (§3, I1)

## 12. Phase gates

No phase starts before the previous one's gate is met. The gates are the plan's, not
negotiable (Plan §10).

| Phase | Gate |
| --- | --- |
| **0 — Foundations** | The 293 members and their entire ledger are in Postgres and every balance matches the current system **to the peso**. Without that verification, nothing advances |
| **1 — Counter web** | The 5 branches operate through the web and the VIP bot is no longer used to redeem |
| **2 — Ticket lines** ⭐ | "What did Juana buy in the last 6 months, product by product" can be answered |
| **3 — Member profile** | Diet, allergies, preferences, birthdays, cashier notes; member self-service form |
| **4 — Intelligence** | Habits, churn alerts on the cashier's screen, owner reports |
| **5 — Product** | Onboarding a new business without touching code; second POS adapter; WhatsApp premium |

## 13. Known risks that shape the code

- **R1 — If the cashier does not identify the customer in the POS, there is no data at all.**
  No customer_id, no balance, no habits, nothing. Expose "% of identified sales" as a
  first-class program metric. A low number is a process problem at the business, and it has to
  be said out loud before selling anything.
- **R3 — Sales data arrives weekly and by hand, but the web looks live.** The as-of date must be
  displayed *more* prominently than the bot displayed it, not less. Never render a balance
  without its cutoff date next to it.

## 14. Operations

Deploy-per-client (§5) buys isolation at the cost of doing everything N times. These decisions
keep that cost flat instead of linear.

### Hosting region — decide before the first deploy

**The server goes in a datacenter close to Argentina** (São Paulo: Vultr, AWS Lightsail, Azure;
or an Argentine VPS). Not Hetzner or DigitalOcean, whose cheapest regions are in Europe or North
America.

WebAssembly makes this less critical than Blazor Server would have — typing and navigating
happen in the browser, and only real requests cross the network. It still matters: the counter
flow is meant to finish in under 15 seconds (FUNCTIONAL-SPEC §1), and searching a member, loading
a record and registering a redemption are each a round trip. At ~250 ms to Europe that is most of
a second of pure distance per lookup. The price difference between regions is a few dollars a
month.

**Counter conditions are part of the phase 1 gate.** The whole flow is exercised on the computer
a branch actually uses, over that branch's real connection, before phase 1 is declared done — not
only on a dev machine. What is being verified with WebAssembly is different from what Blazor
Server would have needed: first-load time over that connection (the .NET runtime downloads once,
then caches), and that a request failing mid-flow shows a clear Spanish message and never loses a
half-filled redemption form or submits it twice.

### Schema updates across N deployments

Every release means migrating N databases. EF Core migrations run **on container start**, and a
failed migration must abort the start and leave the previous version serving — never a half
migrated database answering questions about money.

Clients are updated one at a time, own shop first. A migration that cannot be applied to the
owner's own database never reaches a paying client.

### Backups and the restore drill

A backup that has never been restored is a hypothesis, not a backup.

- Daily automatic backup per client database (the pattern already working in Octaviano's
  `docker-compose.yml`).
- **A restore drill is mandatory before the phase 1 go-live**: restore a backup into a clean
  database and verify balances against the source. Repeated periodically afterwards.

### Continuous integration

§11 requires a clean build and green tests before every merge. That requirement is enforced by
CI, not by memory: a GitHub Actions workflow runs `dotnet build` and `dotnet test` on every push,
on every branch. A red build is not merged.

**On `push` only, not on `pull_request`.** Listening to both events runs the suite twice per
commit in an open PR — once on the branch commit, once on the merge commit GitHub synthesises
from the branch and the base — and the PR page reports the push run either way. The duplicate
covers exactly one case: branch and base each green, their merge red. The owner reviews and
merges every PR by hand and accepts that case in exchange for one unambiguous check per commit
(decided 2026-08-13).

### Monitoring

One VPS hosting every client means one host failure takes all of them down at once, and nobody
is watching at 9pm.

- An external uptime check per instance (UptimeRobot or equivalent is enough to start), alerting
  to the phone.
- Structured application logs, retained per client.
- Import failures surface in the product itself (S11 history), not only in the logs.

Revisit this section when there are paying clients; until then, cheap and simple beats complete.

## 15. Plumbing ported from the university project

Reviewed 2026-08-12: `Dsw2026Tpi` (medical appointments, ASP.NET Core, same layering). It solves
the same plumbing problems this product has, and that work is not worth redoing.

**Ported — adapted, not copied blindly:**

| Piece | Note |
| --- | --- |
| `Program.cs` composition style | One `Add*Configuration` extension per concern. Readable and easy to keep ordered |
| `ExceptionHandlingMiddleware` | Exception type → HTTP status mapping in one place |
| Exception hierarchy | `AppException` + `Validation` / `EntityNotFound` / `Conflict` / `Authentication` / `Authorization`. Lands in `Domain`, not in a `CrossCutting` project |
| `ErrorResponse` / `ErrorDetail` | Error contract with per-field detail. Moves to `Shared` so the client deserialises the same type the API produces |
| Serilog with rolling file + retention | Satisfies §14's logging requirement |
| Health check endpoint | The uptime monitor of §14 needs a target |
| Rate limiting on login | Needed here too — see §8 |
| ASP.NET Core Identity setup | **Arrives with F1-03, not with the phase 0 plumbing.** Password policy, roles, EF stores. Keep Identity's tables in their own `DbContext`, separate from the domain schema |
| Policy and role name constants | Avoids magic strings in `[Authorize]` |

**Not ported, and why:**

| Piece | Reason |
| --- | --- |
| Generic `IPersistence` repository | Incompatible with I1 and with per-tenant filtering — see §3 |
| CORS configuration | Same-origin here (§3). CORS enabled "just in case" is attack surface given away |
| JWT key in a settings file | The key must come from the environment (§8). The university key is in git history and is burned |
| `IConfiguration` read inside the token service | Bind options once and validate at startup, so a missing key fails the boot, not the first login |
| `DateTime.Now` for token expiry | Must be `DateTime.UtcNow` (§8) |
| Domain entities and services | This product's domain is the ported, already-verified Octaviano code (Plan §2) |
