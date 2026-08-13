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
| [ROADMAP.md](ROADMAP.md) | The work, phase by phase, task by task, with gates | Sequencing |

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
| 3 | Wording of the two consent texts — legal, belongs to the business owner | FUNCTIONAL-SPEC §12 | Phase 1 |
| 4 | Pricing model — deliberately deferred until a real client asks | Plan §12.2 | Phase 5 |
| 5 | Which POS the second adapter supports — not decided until a real client asks | Plan §12.3 | Phase 5 |

**Nothing on this list blocks phase 0.**

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
