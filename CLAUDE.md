# Fidelizar — Operating rules

Multi-business customer loyalty product. Read this file before touching anything.

**Binding documents, in order of authority:**

1. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — the technical contract. Layers, namespaces,
   invariants, what is configurable and what is not.
2. [docs/BUSINESS-RULES.md](docs/BUSINESS-RULES.md) — the RN-xx catalog. Every rule the product
   implements traces back to a numbered rule here.
3. [docs/DATA-MODEL.md](docs/DATA-MODEL.md) — tables, columns, types, indexes, and the reason
   each one exists.
4. [docs/CANONICAL-FORMAT.md](docs/CANONICAL-FORMAT.md) — the ingestion contract. Binding from
   phase 2 on.
5. [docs/FUNCTIONAL-SPEC.md](docs/FUNCTIONAL-SPEC.md) — screen by screen: who sees what, and what
   happens when something fails.
6. `docs/Plan-fidelizacion.md` — the product plan. The *why* behind every decision, in Spanish.
   **The owner's document: only the owner edits it.** It is **not in the repository** (it holds
   pricing and commercial reasoning) and lives only on the owner's machine. If it is not on
   disk, do not look for it and do not recreate it — everything binding was carried into the
   documents above.

[docs/README.md](docs/README.md) is the index, and it carries the full list of decisions already
taken and still open. **Read it before asking a question** — the answer is usually there.

If your task contradicts any of these, **stop and ask**. Do not resolve the contradiction
on your own — the plan was written to prevent exactly that kind of improvisation.

---

## Language

- **Code, comments, identifiers, documentation, PR text: English.**
  Exception: domain terms stay in Spanish (`Miembro`, `Canje`, `Saldo`, `Movimiento`,
  `Acumulacion`, `Negocio`, `Sucursal`). They are the words the business actually uses and
  translating them loses the tie to the business rules.
- **Commit messages: Spanish.** See the format below.
- **User-facing UI text: Spanish (Argentina).** Cashiers do not speak English.

## Commits

One commit per meaningful change. Message in Spanish, imperative mood, with a body that says
what changed and why — not a restatement of the diff.

```
<área>: <qué se hizo, en una línea>

<Por qué se hizo. Qué problema resuelve o qué regla implementa.>
<Referencia a la regla o sección: RN-04, ARCHITECTURE §3, Plan §6.>
<Qué queda fuera de este commit, si algo quedó fuera.>
```

Areas: `domain`, `application`, `infra`, `api`, `client`, `shared`, `import`, `tests`, `docs`,
`chore`.

Never commit a build that does not compile. Never commit with failing tests. If you must stop
mid-task, commit what works and say plainly in the body what is incomplete.

## Branches

One branch per implementation, created from `main`:

- `feat/<short-description>` — new capability
- `fix/<short-description>` — correction to something already merged
- `chore/<short-description>` — tooling, docs, structure
- `test/<short-description>` — test-only work

Never commit directly to `main`, and **never merge — the owner reviews and merges.** There is one
developer on this project; "the orchestrator" is the owner, working through this session.

**Pushing a branch and opening its pull request is allowed** (decided by the owner 2026-08-18).
Waiting for the owner to push by hand only delayed the review it was supposed to protect: the
merge button is the gate, not the push. So an agent finishes its branch, pushes it, opens the PR
describing what it did and what it verified, and stops there. What it never does is merge, force-push,
or touch a branch it does not own.

`gh` is installed via WinGet and is **not on `PATH`** — invoke it as
`"$LOCALAPPDATA/Microsoft/WinGet/Links/gh.exe"`.

---

## Files you must never read, write, or execute

These are blocked at the permission layer too, but the rule is yours to respect regardless:

- `.env` and any environment file holding real values
- **Environment variables holding real values** — never set, export, echo, or write one, and
  never read a secret out of one into a file, a log, a commit or a report
- `appsettings.Production.json`, certificates (`*.pfx`, `*.pem`, `*.key`), anything under a
  `secrets/` directory
- Real customer data: member rosters, sales exports, database dumps

**Container and proxy definitions are not on this list** (decided by the owner 2026-08-13).
`docker-compose.yml`, `compose.yml`, `Dockerfile`, `.dockerignore` and `Caddyfile` may be read,
written and run. They describe *how services are wired*, which is ordinary engineering work and
is reviewable in a diff. What made them dangerous was never the YAML — it was the secrets people
paste into it.

So the line moved, it did not disappear: **a container definition must reference every secret,
never contain one.** `POSTGRES_PASSWORD: ${FIDELIZAR_GATE_PG_PASSWORD}` is fine; the password
itself typed into the file is not, and neither is inventing a plausible-looking default so a
command runs unattended. The owner supplies the values; the file only names them.

**What you may do instead:** write documented example files under
[docs/infra-ejemplo/](docs/infra-ejemplo/), with names that cannot be mistaken for the real
thing — `compose.ejemplo.yml`, `caddyfile.ejemplo`, `variables-entorno.ejemplo.txt`. Use
obvious placeholders (`CAMBIAR_ESTO`), never a plausible-looking fake secret. The owner creates
the real files from those.

If a task cannot be completed without a real secret or real data, say so and stop. Do not
invent a connection string, a credential, or a sample member list that looks real.

### The one exception: phase 0 migration and verification

Decided by the owner 2026-08-12, extended to F0-15 the same day. **F0-09, F0-11 and F0-15 may read
`../../Botquery-Pizarra/data/octaviano.db` and the spreadsheets under
`../../Botquery-Pizarra/vip-padron/`.** Those two tasks cannot exist otherwise: they migrate the
293 real members and verify their balances to the peso.

The exception is narrow and does not widen on its own:

- **Only F0-09, F0-11 and F0-15.** No other task reads real member data, in any phase, for any
  reason. F0-15 is on the list because a restore drill that does not re-verify the restored
  balances against the original sources proves only that the file could be loaded, not that the
  backup is worth having.
- **Read-only**, like everything under `Botquery-Pizarra`.
- **Nothing personal is ever written down.** Not in a commit, not in a test fixture, not in a
  document, not in a log, not in a report. Discrepancy reports identify a member by
  `ClienteExternoId`, never by name, phone or DNI.
- **Test fixtures are invented data**, always. A real member never becomes a test case.
- If a task that is not F0-09 or F0-11 seems to need this data, that is a sign the task is wrong.
  Stop and ask.

## Reference code: Octaviano

`../../Botquery-Pizarra/` holds the previous system (`Octaviano.sln`). The assets listed in
Plan §2 come from there.

- **Read-only. Always.** Octaviano is frozen and in production (Plan §13). Not one line changes.
- Copy the code, do not reference it. There is no shared package (Plan §1).
- When you port a file, keep its explanatory comments — they record decisions verified against
  real data, with dates. That reasoning is worth more than the code. Adapt them to English,
  keep the dates and the RN references.

---

## How to work

**Ask when in doubt.** A wrong assumption about money, identity, or personal data is more
expensive than a question. Specifically, always ask before: changing anything that affects how
a balance is computed, adding a field that holds personal or health data, deviating from
ARCHITECTURE.md, or adding a dependency.

**Stay in scope.** Implement the task you were given, completely. Do not refactor code you were
not asked to touch, do not add features nobody requested, do not "improve" a decision the plan
made deliberately. If you spot a real problem outside your scope, report it — do not fix it.

**Report honestly.** If tests fail, say so and paste the output. If you skipped part of the
task, say which part and why. Never report work as done that you have not verified.

**Verify before claiming.** Run `dotnet build` and `dotnet test` before every commit. "It should
work" is not a result.
