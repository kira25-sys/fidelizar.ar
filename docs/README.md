# Documentation index

Read in this order. Each one assumes the ones above it.

| Document | What it answers | Authority |
| --- | --- | --- |
| `Plan-fidelizacion.md` | **Why** the product exists and why each decision was taken. In Spanish. The owner's document — **not in the repository**, see below | Source of truth for intent. **Never edited** |
| [ARCHITECTURE.md](ARCHITECTURE.md) | **How** it is built: stack, layers, the ten invariants, phase gates | The technical contract. Deviating requires approval |
| [BUSINESS-RULES.md](BUSINESS-RULES.md) | The RN-01..RN-25 catalog: what the loyalty program actually does | Every rule in code traces back to a number here |
| [DATA-MODEL.md](DATA-MODEL.md) | Tables, columns, types, indexes and why each exists | Schema decisions |
| [CANONICAL-FORMAT.md](CANONICAL-FORMAT.md) | The one shape every sale takes before reaching the engine | The ingestion contract |
| [FUNCTIONAL-SPEC.md](FUNCTIONAL-SPEC.md) | Screen by screen: who sees what, what happens when it fails | Product behaviour |
| [DESIGN-SYSTEM.md](DESIGN-SYSTEM.md) | Colours, type scale, control sizes, states, hover, keyboard, breakpoints, light/dark, and why — with [`../src/Fidelizar.Client/wwwroot/css/tokens.css`](../src/Fidelizar.Client/wwwroot/css/tokens.css) as the CSS to load | What `F1-02` and every screen task build from |
| [FLOW-S2-S5.md](FLOW-S2-S5.md) | S2–S5 flow by flow: every state, every unhappy path, the exact Spanish copy — the homonym case and the stale-data warning in full | What `F1-05` through `F1-09` build from |
| [ROADMAP.md](ROADMAP.md) | The work, phase by phase, task by task, with gates | Sequencing |
| [DESARROLLO-LOCAL.md](DESARROLLO-LOCAL.md) | Setting up a working machine: the dev Postgres on 5434, the two user-secrets values, and why a persistent environment variable is dangerous | Not a contract — how to run it locally |

> ARCHITECTURE §15 records what is ported from the university project `Dsw2026Tpi` and, just as
> importantly, what is not.

> **`Plan-fidelizacion.md` is deliberately not in the repository.** It holds pricing, sales risks
> and internal commercial reasoning, and lives only on the owner's machine. The `Plan §n`
> references throughout these documents point at it; every decision it drives has already been
> carried into the documents above, so nothing here depends on having it. If the file is not on
> disk, that is expected — do not recreate it.

Team rules — branches, commits, language, forbidden files — are in [../CLAUDE.md](../CLAUDE.md).

---

## The five things that matter most

If you read nothing else:

1. **The ledger is append-only and the balance is always `SUM(Monto)`.** Nothing is edited,
   nothing is deleted, every correction is a new row. This is the product's entire claim to
   trustworthiness.
2. **No business number is a literal in code.** The 3% and the $120.000 belong to one shop.
   The product configures them per business.
3. **Ticket line detail is the product.** Totals alone buy you a balance. Lines buy you habits,
   preferences and recommendations — everything the product actually promises.
4. **Consent is built in phase 1, not retrofitted.** Diet and allergies are health data under
   Law 25.326. Adding consent to thousands of loaded members later is miserable work with a legal
   hole in the middle.
5. **The cashier searches, sees a name, a balance and alerts, and nothing else.** No listing, no
   phone number, no DNI. The counter screen is visible to the customer standing there.

## Open decisions

Collected from the documents above. None of them may be resolved by an agent on its own.

| # | Decision | Where | Blocks |
| --- | --- | --- | --- |
| 1 | Tolerance on the `SUM(subtotal)` vs `total_venta` check | CANONICAL-FORMAT §10 | Phase 2 |
| 2 | Returns and credit notes — how the POS even expresses them | CANONICAL-FORMAT §10 | Phase 2 |
| 4 | Pricing model — deliberately deferred until a real client asks | Plan §12.2 | Phase 5 |
| 5 | Which POS the second adapter supports — not decided until a real client asks | Plan §12.3 | Phase 5 |

**Nothing on this list blocks phase 0.** Former items 3 (consent wording) and 6 (canje
idempotency) were resolved by the owner 2026-08-19 — see "Decisions already taken" below. Their
numbers are retired rather than reused, since `REST-CONTRACT-F1.md` and `openapi-fase1.yaml`
already point at "decision #6" by that number.

## Decisions already taken

All of the below were decided 2026-08-12 unless noted.

### Product and program

| Decision | Outcome | Where |
| --- | --- | --- |
| Product name | **Fidelizar** as a working name; the commercial brand may still change without renaming code | Plan §12.1 |
| `ClienteExternoId` type | `text`, not `int` — a future POS with alphanumeric ids must not force a ledger migration | DATA-MODEL §7 |
| `ClienteExternoId` requiredness | **Nullable.** A member registered at the counter starts unlinked and accrues nothing until linked | DATA-MODEL §7 |
| Program configuration | **Versioned**, never overwritten. Each movement stores which configuration produced it | DATA-MODEL §7 |
| When credit is written | **Per paid sale, at import time** — not at month end. The manager's veto is an `Ajuste` | BUSINESS-RULES RN-22 |
| `TopeMesesCongelados` | **3** frozen months before one counts as bad | BUSINESS-RULES RN-23 |
| Defaults for a new business | Grace streak **off**, **no** monthly target — only the accrual percentage | BUSINESS-RULES §resolved |
| Product matching | External code first, normalised name as fallback; unmatched lines kept with `ProductoId = null` | DATA-MODEL §7 |
| Branches | Configured by hand; an import with an unknown branch code rejects those sales | DATA-MODEL §7 |
| Consent of the 293 existing members | Migrated as **verbal**, all types, `Canal = MigracionVerbal`. Known caveat recorded | DATA-MODEL §7 |
| Sale voided after its credit was redeemed | **RN-25.** The `Ajuste` is written, the balance may go negative, redemptions are blocked, the manager is notified | CANONICAL-FORMAT §11 |
| Effective date on movements | `FechaEfectiva` (the day it happened) is separate from `RegistradoEn` (when the system learned) | DATA-MODEL §4 |
| Balance on the counter screen | **Always visible** — the customer seeing it is part of the hook (RN-12) | FUNCTIONAL-SPEC §13 |
| Cashier session | **Full login per shift**, no short auto-lock. Trade-off accepted by the owner | FUNCTIONAL-SPEC §13 |
| Member of another branch | Found and served normally — the program is one and the target is global (RN-07) | FUNCTIONAL-SPEC §13 |
| What `Corte.Fecha` means | **The date up to which sales data has been loaded, advancing on every import** — not a fixed program-start date. One row per business holds the current value only; the history of every past cutoff lives in `LoteImportacion.CorteDeclarado`, not in a column of its own. Corrects a reading found while writing `F1-02` and resolved the same day | DATA-MODEL §4, decided 2026-08-18 |
| Wording of the two consent texts (former open decision #3) | **Both approved, provisional until production.** `DatosPersonales` and `DatosSensibles` texts carry `[RAZÓN SOCIAL]`, `CUIT` and (for `DatosPersonales`) `[DOMICILIO]` placeholders resolved from the acting `Negocio`'s own data at render time — never a business literal in code. The asymmetry in the wording is enforced, not cosmetic: `DatosPersonales` says alta is impossible without it (mandatory, alta rejected without a granted consent); `DatosSensibles` says alta and membership are possible without it and it is revocable any time with no effect on the account (optional, alta accepted without it, revocation never touches balance or points) | FUNCTIONAL-SPEC §7/§12, decided 2026-08-19 |
| Idempotency on `POST /miembros/{id}/canjes` (former open decision #6) | **Client-generated `ClaveIdempotencia`, one per redemption attempt**, carried on `RegistrarCanjeRequest`. A retry with the same key and the same member/amount/date/reason returns the original `CanjeResponse`, no second `Canje`; the same key with different data is rejected (`409 CLAVE_IDEMPOTENCIA_REUTILIZADA`). The guarantee against two simultaneous identical retries lives in a unique partial index on `(NegocioId, ClaveIdempotencia)`, not only in a check before the insert | REST-CONTRACT-F1, decided 2026-08-19 |

### Technical

| Decision | Outcome | Where |
| --- | --- | --- |
| Frontend architecture | **REST API + Blazor WebAssembly**, replacing Blazor Server: the backend becomes an asset independent of the client, and back/front work can be scheduled separately | ARCHITECTURE §2 |
| Layering | `Domain` · `Application` · `Infrastructure` · `Shared` · `Api` · `Client`. Clean Architecture with dependency inversion, monolithic backend | ARCHITECTURE §3 |
| `CrossCutting` project | **Not created** — it becomes a junk drawer. Middleware and logging live in `Api` | ARCHITECTURE §3 |
| Generic repository | **Rejected.** A generic `Delete<T>` makes I1 unenforceable and leaves nowhere to require `NegocioId`. One repository per aggregate | ARCHITECTURE §3 |
| Authentication | **A JWT carried in an `HttpOnly` cookie** — stateless validation plus a token no script can read. Antiforgery on state-changing endpoints | ARCHITECTURE §8 |
| CORS | **Not enabled.** The client is served from the API's own origin | ARCHITECTURE §3, §15 |
| Hosting region | A datacenter close to Argentina (São Paulo or an Argentine VPS) | ARCHITECTURE §14 |
| Reuse from the university project | Plumbing yes (composition, middleware, error contract, Serilog, health check, rate limiting); domain and generic repository no | ARCHITECTURE §15 |
| `Rol` identifier | `Dueno`, no `ñ`. "Dueño" is UI text only | DATA-MODEL §2 |
| Real member data | Readable **only** by F0-09 and F0-11, read-only, nothing personal ever written down | CLAUDE.md |
| Phase 0 gate | **Three-way** balance comparison: Postgres vs Octaviano vs the owner's spreadsheet | ROADMAP phase 0 |
| CI trigger | **`push` only, not `pull_request`** — one check per commit instead of two. The owner reviews and merges every PR by hand (decided 2026-08-13) | ARCHITECTURE §14 |
| Target device | **The shop's computer first**, then phone and tablet — one responsive web page, never a tablet-specific UI. Replaces the earlier "cheap counter tablet" premise (decided 2026-08-18) | FUNCTIONAL-SPEC §1, ROADMAP F1-17 |
