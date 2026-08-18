# Roadmap

The work breakdown. Each task below becomes **one branch** and one or more commits.

Nothing starts until the previous phase's gate is met. The gates come from Plan §10 and are not
negotiable — least of all the phase 0 one.

**Roles:** `BE` backend-dev · `FE` frontend-dev · `UX` ux-designer · `QA-F` qa-functional ·
`QA-D` qa-data. The orchestrator assigns, reviews and merges.

**Backend and frontend are scheduled separately** (ARCHITECTURE §3). The REST contract
(F1-04b) is the seam: once it is agreed and the endpoints pass their tests, backend work and
screen work proceed without waiting on each other.

---

## Phase 0 — Foundations ✅ **closed 2026-08-13**

> **Gate:** the 293 members and their entire ledger are in Postgres, and every balance matches
> **to the peso** in all three places: Postgres, Octaviano as it runs today, and the owner's
> spreadsheet. Comparing the migration against the database it was migrated from proves only that
> the copy did not break — the third point is what proves the ported calculation is right.
> Without that verification, nothing advances.

| # | Task | Role | Depends on |
| --- | --- | --- | --- |
| F0-01 | Solution skeleton: `Fidelizar.sln`, the six source projects (`Domain`, `Application`, `Infrastructure`, `Shared`, `Api`, `Client`), the four test projects, `.gitignore`, `.editorconfig`, dependency direction enforced — including `Client` referencing only `Shared` | BE | — |
| F0-02 | `Domain` entities: `Negocio`, `Sucursal`, `Miembro`, `MovimientoCredito`, `Corte`, `ConfiguracionPrograma` | BE | F0-01 |
| F0-03 | `Infrastructure`: `DbContext`, EF configuration, **first migration with `NegocioId` on every table** | BE | F0-02 |
| F0-04 | Port `MontoParser` **unchanged**, with its full test suite including the ambiguity cases | BE | F0-01 |
| F0-05 | Port `VipNombres` name normalisation + tests | BE | F0-01 |
| F0-06 | Per-aggregate repositories (**no generic repository**, ARCHITECTURE §3) + `SaldoService` in `Application`: balance as `SUM(Monto)`, redemption capped at balance (RN-24) | BE | F0-03 |
| F0-06b | Plumbing ported from the university project (ARCHITECTURE §15): composition extensions, exception middleware and hierarchy, error contract in `Shared`, Serilog, health check | BE | F0-01 |
| F0-07 | `Corte` per business: declared at import, persisted, accrual **fails loudly** when absent | BE | F0-03 |
| F0-08 | Port `VipPadronImporter` — the entry door for every new business | BE | F0-04, F0-05 |
| F0-09 | One-off migration tool: Octaviano SQLite → Fidelizar Postgres, members and full ledger, **plus a `Consentimiento` row per member** (`Canal = MigracionVerbal`, all types, dated `FechaAlta` — DATA-MODEL §7) | BE | F0-06, F0-08 |
| F0-10 | **Invariant test suite** — one test per invariant I1–I10 in ARCHITECTURE §4 | QA-D | F0-06 |
| F0-11 | **Peso-by-peso verification harness — three-way**: for every member, the balance computed in Postgres vs the one Octaviano returns today from its own `VipSaldoService` vs `vip-padron/VIP-CLUB-puntos.xlsx`. Reports every discrepancy **by `ClienteExternoId`, never by name** | QA-D | F0-09 |
| F0-12 | Example infrastructure files under `docs/infra-ejemplo/` (compose, Caddy, environment variables), with obvious placeholders | BE | F0-03 |
| F0-13 | CI: GitHub Actions running `dotnet build` and `dotnet test` on every push, on every branch — **on `push` only, so a PR gets one check and not two** (ARCHITECTURE §14) | BE | F0-01 |
| F0-14 | Migrations run on container start; a failed migration aborts the start and leaves the previous version serving (ARCHITECTURE §14) | BE | F0-03 |
| F0-15 | **Restore drill**: restore a backup into a clean database and verify balances against the source. Repeated periodically afterwards | QA-D | F0-11 |

**F0-11 is the gate.** It is not a formality: if one balance is off by one peso, the cause is
found and fixed before anything in phase 1 begins.

### Gate met — 2026-08-13

`Fidelizar.VerificacionGate` was run against the real sources and returned, verbatim:

> `VEREDICTO: GATE CUMPLIDO — las tres puntas coinciden para los 293 socios, sin excepciones.`

| | |
| --- | --- |
| Members migrated | 293 — none skipped |
| Ledger movements | 575 — none skipped |
| `Consentimiento` rows | 879 (293 × 3, `Canal = MigracionVerbal`) |
| Members compared, all three sources | 293 / 293 / 293 |
| **Discrepancies** | **0** |
| Control sum, identical across all three | 2.390.011,35 |

The comparison is exact `decimal` equality with no tolerance and no epsilon, routed through the
single rounding point (I4). The Postgres side is read through `SaldoService` → `SUM(Monto)` (I2),
not a query written for the occasion, so a defect in the ported calculation would surface here
rather than be masked.

A fourth, independent confirmation: the five `TOTAL` cells the owner typed by hand in the
spreadsheet — cells the harness never reads — sum to that same 2.390.011,35. The spreadsheet
carries 298 rows against 293 members; the five extras were each verified to be a sheet total
matching the sum of its own sheet's member rows (78+50+39+85+41 = 293), so no member went
uncompared.

**The report itself is deliberately not in this repository** and never will be: it identifies
members by `ClienteExternoId` and lives wherever `--salida` pointed, outside the working tree.

**Phase 1 is unblocked.**

> **F0-09, F0-11 and F0-15 are the only tasks in the whole roadmap allowed to read real member data**
> (CLAUDE.md, "the one exception"). Read-only, and nothing personal is ever written down:
> discrepancies are reported by `ClienteExternoId`, never by name, phone or DNI, and test
> fixtures are invented data. Any other task that seems to need this data is a task that is
> wrong — stop and ask.

## Phase 1 — Counter web

> **Gate:** the 5 branches operate through the web and the VIP bot is no longer used to redeem —
> verified on the computer a branch actually uses, over that branch's real connection (F1-17),
> not only on a dev machine.

| # | Task | Role | Depends on |
| --- | --- | --- | --- |
| F1-01 | Design system: colours, type scale, target sizes, states, light and dark. Built for the shop's computer first, and responsive down to a phone | UX | — |
| F1-01b | Design system revision for a computer first: hover states, keyboard navigation, focus order, and what a wide screen shows that a narrow one does not. Follows the platform decision of 2026-08-18 | UX | F1-01 |
| F1-02 | Flow design for S2–S5, including the homonym case and the stale-data warning | UX | F1-01 |
| F1-03 | Identity: `Usuario`, roles, branches, `RegistroAuditoria`, and **JWT-in-`HttpOnly`-cookie authentication** — key from the environment and validated at startup, UTC expiry, antiforgery on state-changing endpoints, rate-limited login verified to be in the pipeline (ARCHITECTURE §8) | BE | F0-03 |
| F1-04 | Authorisation enforced **server-side on every endpoint**, per role, per branch | BE | F1-03 |
| F1-04b | REST contract for phase 1: endpoints, DTOs in `Shared`, error shape, OpenAPI. **The backend can be finished and tested before a single screen exists** | BE | F1-03 |
| F1-04c | `Client` shell: WebAssembly project, typed API client over `Shared`, session handling, one Spanish error/offline treatment used everywhere | FE | F1-04b |
| F1-05 | S1 Ingreso · S2 Buscar socio, with homonym resolution | FE | F1-02, F1-04c |
| F1-06 | S3 Ficha del socio: balance, prominent cutoff date, alert strip | FE | F1-05 |
| F1-07 | S4 Registrar canje, including retroactive date + mandatory reason | FE + BE | F1-06 |
| F1-08 | Consent: entity, service, and the rule that sensitive fields cannot be written without it | BE | F1-03 |
| F1-09 | S5 Alta de socio with both consent checkboxes | FE | F1-08 |
| F1-10 | S6 Ficha completa · S7 Historial de movimientos | FE | F1-06 |
| F1-11 | S8 Anular movimiento → writes an `Ajuste`, never an edit | FE + BE | F1-10 |
| F1-12 | S9 Cierre diario de canjes, exportable | FE | F1-07 |
| F1-13 | S10 Usuarios y sucursales | FE | F1-04 |
| F1-14 | "Socios sin vincular": list of members without `ClienteExternoId` and the linking flow, for `Encargada`/`Dueno` — a counter-registered member must not stay unlinked silently | FE + BE | F1-09 |
| F1-15 | **Permission matrix tests, run against the API**: every role against every endpoint, including the negatives — a cashier must not reach a phone number by calling the endpoint directly, with no screen involved | QA-F | F1-13 |
| F1-16 | End-to-end counter flow: search → record → redeem, under 15 seconds | QA-F | F1-12 |
| F1-17 | **Real counter conditions**: the whole flow on the computer a branch actually uses, over that branch's real connection. Measure first-load time; a request failing mid-flow shows a clear Spanish message, never loses a half-filled form and never submits it twice (ARCHITECTURE §14) | QA-F | F1-16 |
| F1-18 | Uptime check per instance alerting to the phone + structured logs retained per client (ARCHITECTURE §14) | BE | F1-16 |

## Phase 2 — Ticket line ingestion ⭐

> **Gate:** "what did Juana buy in the last 6 months, product by product" can be answered.
> **This is where the product actually begins.**

| # | Task | Role | Depends on |
| --- | --- | --- | --- |
| F2-01 | Canonical format reader with the full validation set in CANONICAL-FORMAT §6 | BE | F0-04 |
| F2-02 | `Venta` + `VentaLinea` + `Producto` + `LoteImportacion` + `FilaRechazada` | BE | F2-01 |
| F2-03 | `PosOctaviano` adapter — the first real one | BE | F2-01 |
| F2-04 | Accrual engine: per-sale credit, re-import window, `Ajuste` on changed sales, double-credit impossible at the index level | BE | F2-02 |
| F2-05 | Versioned `ConfiguracionPrograma` + S12, replacing every remaining constant | BE + FE | F2-04 |
| F2-06 | S11 Importar ventas, **dry run first**, downloadable rejections | FE | F2-02 |
| F2-07 | R1 metric: % of sales carrying a `cliente_id`, surfaced as a program metric | BE + FE | F2-02 |
| F2-08 | Ingestion test suite: ambiguous amounts, inconsistent totals, re-imports, voided sales, missing cutoff | QA-D | F2-04 |
| F2-09 | Product query: everything a member bought over a date range | BE | F2-02 |

## Phase 3 — Member profile

| # | Task | Role |
| --- | --- | --- |
| F3-01 | `PerfilMiembro`: diet, allergies, preferences, cashier notes — gated by consent | BE |
| F3-02 | S13 profile screen for manager and owner | FE |
| F3-03 | Data export and deletion on member request | BE |
| F3-04 | S14 member self-service form (link / QR), including its own consent flow | UX + FE |
| F3-05 | Sensitive-data tests: no write without consent, every read audited | QA-D |

## Phase 4 — Intelligence

| # | Task | Role |
| --- | --- | --- |
| F4-01 | Purchase habits: what, how often, what they stopped buying | BE |
| F4-02 | Counter alerts driven by habits | FE |
| F4-03 | S15 owner reports: who is leaving, who never redeems, top products by segment | FE |
| F4-04 | Grace streak (RN-16..RN-23), **off by default**, system proposes and a person decides | BE |

## Phase 5 — Product

| # | Task | Role |
| --- | --- | --- |
| F5-01 | Onboarding a new business without touching code |
| F5-02 | Second POS adapter — **only when a real client asks** |
| F5-03 | WhatsApp as a premium tier |
| F5-04 | Pricing, contract, data policy, support |

> **Client #1 is the owner.** If it runs flawlessly across the 5 branches for a few months, there
> is a sales case backed by evidence. Building for sale before it works in your own shop produces
> features nobody asked for (Plan §10).

---

## What is not scheduled, and why

- **Retiring Octaviano's VIP bot** — not until the product runs in all 5 branches with verified
  balances. Then: bot goes read-only, `/canje` is withdrawn so there is one single truth, and the
  bot is deleted once nobody uses it (Plan §13).
- **The second POS adapter** — building it before a real client asks is guesswork (Plan §12).
- **Pricing model** — it decides whether shared multi-tenancy arrives earlier than planned, and
  it is a business decision, not a technical one.
