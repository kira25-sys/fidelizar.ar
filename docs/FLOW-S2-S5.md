# Flow design — S2 to S5 (F1-02)

What happens on screen, in what order, and what it says, for the four counter screens: **S2
Buscar socio · S3 Ficha del socio · S4 Registrar canje · S5 Alta de socio**. This is the layer
between [FUNCTIONAL-SPEC.md](FUNCTIONAL-SPEC.md) (the *what*) and [DESIGN-SYSTEM.md](DESIGN-SYSTEM.md)
(the tokens and the interaction contract) — it does not redefine either, it fills the gap
between them: every state a screen can be in, and the exact Spanish sentence it shows in that
state.

**Not in scope, on purpose:** no Razor, no CSS, no `Fidelizar.Client` — that is `frontend-dev`'s
job from `F1-05` on. No colour, size or keyboard rule already fixed by `DESIGN-SYSTEM.md` §4–§11
is repeated here except where a flow decision depends on it. No consent wording — S5's two
checkboxes are marked `[texto pendiente del dueño]` throughout, per `FUNCTIONAL-SPEC.md` §12 and
the task's own instruction.

**Conventions used below**

- Breakpoints, named as in `DESIGN-SYSTEM.md` §10: **Narrow** (`< 640px`), **Medium**
  (`640–1023px`), **Wide** (`≥ 1024px`, the shop's computer — the primary target).
- Money: `$ 12.400` — `$` then a space, `.` as the thousands separator, no decimals when the
  amount is a whole number of pesos, `,NN` when it is not (I4's rounding already guarantees at
  most two decimal places).
- Dates in prose: `DD/MM/AAAA`.
- Every example name, phone, DNI, and amount below is invented. None comes from a real member
  (`CLAUDE.md`).
- `.list-row`, `.badge--*`, `.card`, `.field-error`, `.split`/`.split__aside`, `.page`,
  `.form-column` are the primitives `DESIGN-SYSTEM.md` §14 already defines in
  [`design-system/tokens.css`](design-system/tokens.css); this document says which primitive
  each element uses, not how the primitive is built.

---

## 0. The one mechanic that runs through all four screens: the stale-data warning

R3 (`ARCHITECTURE.md` §13) and the two-tier badge (`DESIGN-SYSTEM.md` §11.1) are already fixed.
What this document adds is *where the badge appears* and *what the cashier can still do while it
is showing the older tier*.

### 0.1 What the date actually is — **a contradiction found while writing this, flagged, not resolved**

`DATA-MODEL.md` §3 (`Corte`) describes `Corte.Fecha` as **"the program's start date — from when
the system counts"**, declared once per business at the first import, with a unique index that
guarantees exactly one row per `NegocioId`. `CANONICAL-FORMAT.md` §6 confirms the same reading:
`Corte` is the boundary before which a sale is silently out of scope, not a rolling "last import"
marker. `REST-CONTRACT-F1.md` says the **implemented** `GET /miembros/{id}/saldo` sources its
`corteFecha` field from exactly that: `ICorteService.ObtenerCorteVigenteAsync`.

But `FUNCTIONAL-SPEC.md` §5 and `DESIGN-SYSTEM.md` §11.1 both describe a date that is supposed to
move — "sales data arrives weekly," a badge that flips from neutral to warning **past 7 days**,
the literal label "Datos de ventas al …". A fixed, one-time program-start date cannot do that
job: days after go-live it would already read as permanently stale (frozen at the same date
forever), while the two-tier badge would never again say anything true about how recent the
balance actually is.

**This is a real conflict between binding documents**, not a wording nit, and it sits at the
centre of the one thing this task was named for (the stale-data warning). I have not resolved
it — that is a decision about what `corteFecha` on the wire *means*, which is exactly the kind of
thing `CLAUDE.md` says to stop and ask about, not improvise. What follows assumes the **intended**
behaviour — a date that reflects the most recent successful `LoteImportacion.EjecutadoEn` for the
business, which is the only concept in `DATA-MODEL.md` that actually varies week to week — and
flags every place that assumption touches. See §9 for the full list of what needs an owner/backend
decision before `F1-06`/`F1-07` build against this.

### 0.2 Where the badge appears

Per R3's own wording — "not in any screen" — a balance is never shown without it, which means the
badge is not only an S3 fixture:

| Screen | Where |
| --- | --- |
| S2 results | Next to each row's `saldo`, small `.badge--neutral`/`.badge--warning`, no text beyond the date — the row is already dense (see §1.4 for the resulting `MiembroResumen` gap) |
| S3 | Directly under the balance figure, exactly as `FUNCTIONAL-SPEC.md`'s mockup shows: `"Datos de ventas al DD/MM/AAAA"` |
| S4 dialog | Repeated once, small, under the read-only "Saldo disponible" line — the cashier is about to act on this number and should not have to remember it from S3 |

### 0.3 Exact copy, both tiers

| Tier | Badge text | Extra sentence (S3 and S4 only, not S2 — no room in a result row) |
| --- | --- | --- |
| ≤ 7 days (`.badge--neutral`) | `Datos de ventas al DD/MM/AAAA` | — |
| > 7 days (`.badge--warning`) | `Datos de ventas al DD/MM/AAAA · hace N días` | `"Puede haber compras más recientes todavía sin acreditar."` — one line, `--text-sm`, `--color-text-muted`, directly under the badge |

The `ⓘ` in `FUNCTIONAL-SPEC.md`'s mockup is **not interactive**. `DESIGN-SYSTEM.md` §8.2
forbids a tooltip holding information the cashier needs, so there is nothing for it to reveal on
click or hover — it is a static glyph that reinforces "this line is a disclosure," and the full
sentence is already on screen at rest. No `F1-06`/`F1-07` implementation should wire a popover to
it.

### 0.4 What the cashier can and cannot do while the badge reads "warning"

**Nothing is blocked by data age alone.** The warning tier is honesty about the *sales* half of
the balance (accrual from purchases not yet imported); it says nothing about the *redemption*
half, which is always live — every `Canje` is written the instant the cashier confirms it,
regardless of when the last import ran. Search, opening a ficha, and registering a canje all work
exactly the same under either tier. The only thing that blocks a redemption is a negative balance
under review (§2.5), which is unrelated to staleness and uses a different, unconditional message.

This is the flow-level answer to the brief's "what can and cannot the cashier do while the data is
old": **everything, and the badge is there so a customer who spent yesterday and does not see it
reflected yet understands why, rather than the cashier having to explain it verbally.**

---

## 1. S2 · Buscar socio

### 1.1 Layout

One `.page`, one `.form-column`-width search field, autofocused on load — the **only** autofocus
in the product (`DESIGN-SYSTEM.md` §9.2). No header chrome above it beyond the app's persistent
nav bar (session, sign out — out of this document's scope).

```
┌───────────────────────────────────────────┐
│  Buscar socio                              │
│  [ 🔍  Nombre del socio...              ]  │
│                                             │
│  (results appear here as the cashier types)│
└───────────────────────────────────────────┘
```

At **Wide**, once a member is selected, the screen becomes a `.split`: the search field and its
result list move into `.split__aside` (sticky, ~22rem) and the selected member's S3 record renders
in the main column beside it — `DESIGN-SYSTEM.md` §10.2's explicit purpose: "resolving a homonym
means comparing candidates; side by side, a wrong guess costs one click instead of a trip back."
At **Narrow/Medium**, S2 and S3 are sequential screens (§1.6).

### 1.2 The field and when search fires

- `role="combobox"`, the ARIA-combobox contract already fixed by `DESIGN-SYSTEM.md` §9.3 — this
  document does not redefine `↓`/`↑`/`Home`/`End`/`Enter`/`Esc`, only what fills the listbox.
- Placeholder: `"Nombre del socio…"`.
- No search fires below 3 characters (`FUNCTIONAL-SPEC.md` §4). Below that, the space under the
  field shows one line of hint text, `--text-sm`, `--color-text-muted`:
  `"Escribí al menos 3 letras para buscar."`
- From the 3rd character, search is **debounced 300ms** and **cancels the previous request**
  before firing the next one — a flow-level choice, not one `DESIGN-SYSTEM.md` fixes. This
  matters beyond snappiness: without cancellation, a slow response to `"gom"` can arrive *after*
  the fast response to `"gomez ana"` and silently overwrite the correct list with a stale,
  broader one — on a screen whose whole job is picking the right person, showing the wrong
  candidate set is worse than showing nothing for an extra 200ms.
- While a request is in flight, the field's own row shows a small inline spinner (not the results
  area — nothing overwrites the last good list while a new one loads, so the cashier can still
  tap a visible row mid-keystroke without the list flickering to empty).

### 1.3 Result rows — the ordinary case

Each result is one `.list-row` inside a `role="listbox"`. Two lines:

```
Ana María Gómez                                  N° 0142
$ 12.400              Datos de ventas al 12/08/2026
```

- Line 1: name (`--text-lg`) left, member number right (`--text-sm`, `--color-text-muted`,
  `"N° 0142"`). **`NumeroSocio` is nullable** (`DATA-MODEL.md` §3) — every member registered
  through S5 will have one (`AltaMiembroRequest` has no field for it, and nothing assigns one).
  When it is null, that half of line 1 is simply blank — never `"N° —"` or `"N° null"`.
- Line 2: `saldo` (`--text-base`, bold) and the stale-data badge from §0, inline.
- **A negative balance is never rendered as a number here either** (RN-25's "never shown as a
  debt" is not S3-only). When `saldo < 0`, line 2 replaces the figure with a
  `.badge--warning`: `"En revisión"` — enough for the cashier to recognise the row without seeing
  a number that looks like the member owes money, before they have even opened the ficha.
- Gender-neutral by design: `FUNCTIONAL-SPEC.md`'s mockup uses `"Socia #0142"`, matching its
  example name. This document uses `"N° 0142"` instead — the product does not collect a gender
  field, and a mis-gendered label read by the customer over the cashier's shoulder is a real,
  avoidable annoyance for a five-character fix.

Tap, click, or `Enter` on the active option opens the member's record (§2). At **Wide**, that
means the main column now renders S3 while the aside keeps the same query and result list — no
re-search needed to try the next candidate.

### 1.4 Homonyms — the trigger, precisely

`FUNCTIONAL-SPEC.md` §4 says "when two results are genuinely indistinguishable by name," which is
not by itself a testable rule. This document fixes one:

> **Two or more results whose displayed `nombre` is identical after the same normalisation the
> search already applies** (`NombreNormalizado` — accent- and case-folded) **are ambiguous.**

For every row inside such a group, add a third line: `"Alta: DD/MM/AAAA"` (`fechaAlta`,
`--text-sm`, `--color-text-muted`) — the one field in `MiembroResumen` that is never null,
unlike `NumeroSocio`. This is deliberately narrower than "similar-looking names": *similar* names
already read differently enough on screen that the member number and balance are usually enough
to tell them apart without extra text competing for the cashier's attention; *identical* names
carry real risk of a wrong tap and get the extra line every time, unconditionally.

```
Ana Gómez                                        N° 0089
$ 3.200                Datos de ventas al 12/08/2026
Alta: 14/02/2023

Ana Gómez                                        N° 0301
$ 18.900                Datos de ventas al 12/08/2026
Alta: 03/06/2025
```

Nothing is auto-selected, nothing is hidden, no dialog interrupts to ask "¿cuál de las dos?" —
both rows are just there, exactly like any other result, which is the entire point (I7,
`FUNCTIONAL-SPEC.md` §4: "it costs nothing extra to build").

**A known gap in `MiembroResumen`** (`openapi-fase1.yaml`): the schema carries `saldo` but not a
cutoff date, and `FUNCTIONAL-SPEC.md` §4 itself only lists "name, member number, balance" for a
result row — no date. §0.2 above resolves this in favour of `ARCHITECTURE.md` §13 R3 ("not in any
screen"), because `CLAUDE.md`'s document-authority order ranks `ARCHITECTURE.md` above
`FUNCTIONAL-SPEC.md`, and R3's wording leaves no screen exempt. That resolution needs
`MiembroResumen` to gain a cutoff-date field before `F1-05` can build this row as specified — flagged
for backend, not something this document can add to the contract itself.

### 1.5 Other states

| State | Trigger | Copy |
| --- | --- | --- |
| Empty query | Field empty on load or cleared | Hint only (§1.2), no results area rendered |
| Too short | 1–2 characters | `"Escribí al menos 3 letras para buscar."` |
| No matches | ≥ 3 characters, 0 results | `"No encontramos a nadie con ese nombre. Probá con otro nombre o con menos letras."` — the exact wording `DESIGN-SYSTEM.md` §11.2 already fixed, plus one link below it: `"¿Es un socio nuevo? Dar de alta →"`, going to S5 (§4) |
| Search failed | Network/server error while searching | Results area shows one line, neutral tone (not `--color-danger` — this is connectivity, not the cashier's mistake): `"No pudimos buscar. Revisá la conexión e intentá de nuevo."` The last successful list is **not** kept on screen once an error is shown — a stale list presented as current would be a worse failure than an honest blank, given what a wrong tap here costs (I7) |

### 1.6 Narrow/Medium: state carried across the S2 ↔ S3 trip

At these widths S2 and S3 are separate screens, not a `.split`. Selecting a result navigates
forward (§9.2's rule: focus lands on S3's `<h1>`, document title changes). **The query text and
the last result list are cached in memory for the session**, keyed by nothing more than "the last
search" — so pressing back from S3 restores S2 exactly as it was, with no re-typing and no second
round trip, which matters for the 15-second budget as much as any single screen does. The cache
is cleared the moment the cashier types a new query, not on any timer.

---

## 2. S3 · Ficha del socio

### 2.1 Layout

Exactly the card `FUNCTIONAL-SPEC.md` §5 already sketches; this section only adds what happens
around it.

```
┌──────────────────────────────────────────────┐
│  Ana María Gómez                       N° 0142│
│                                                │
│      SALDO DISPONIBLE                         │
│      $ 12.400                                 │
│      Datos de ventas al 12/08/2026  ⓘ         │
│                                                │
│  🎂 Cumple el 4/9                             │
│                                                │
│  [   REGISTRAR CANJE   ]                      │
└──────────────────────────────────────────────┘
```

`<h1>` is the member's name (focus target on arrival, per `DESIGN-SYSTEM.md` §9.2). One `.card`.
`REGISTRAR CANJE` is `--control-size-primary`, full width on Narrow.

### 2.2 The alert strip in phase 1

`FUNCTIONAL-SPEC.md` §5 lists four alert kinds. Phase 1 can only ever populate one of them:

| Alert kind | Data source | Available in phase 1? |
| --- | --- | --- |
| Unredeemed balance | The hero `saldo` figure itself | Already satisfied structurally — it is always visible (`FUNCTIONAL-SPEC.md` §13, decision 1), so there is no separate strip row for it. A strip line duplicating the number already on screen in 56px type would be noise, not a signal |
| Birthday (RN-11) | `Miembro.FechaNacimiento` | **Yes.** Renders `"🎂 Cumple el D/M"` starting 2 days before, per RN-11 |
| Allergy/diet | `PerfilMiembro`, gated by `Consentimiento(DatosSensibles)` | **No.** `PerfilMiembro` is phase 3 (`F3-01`, per `REST-CONTRACT-F1.md`). `FichaMostradorResponse` reserves the `AlergiaODieta` alert kind, but nothing produces one yet |
| Usual purchases | Phase 4 aggregation | **No.** `FUNCTIONAL-SPEC.md` §5 names phase 4 explicitly |

So in phase 1, a ficha with no upcoming birthday shows **no alert strip at all** — not an empty
box, not a placeholder line. This document does not design the allergy/diet or usual-purchases
rows beyond confirming the reserved slot and its consent gate (`.badge--warning` for the allergy
line specifically, per `DESIGN-SYSTEM.md` §4.5) — that is real design work for whichever task
wires `PerfilMiembro` in phase 3, working from real fields this document has no visibility into
today.

### 2.3 Unhappy path — the business has no `Corte` declared yet

`GET /miembros/{id}/saldo` returns `409 CORTE_NO_DECLARADO` when nothing has ever been imported
for the business. This is not a per-member problem, so S3 does not show a broken card — it
replaces the whole card with one message, no `REGISTRAR CANJE` button at all (there is no way to
know a balance is safe to redeem against):

> **"Todavía no hay datos de ventas cargados para este negocio."**
> "Pedile a la encargada o al dueño que complete la primera carga antes de registrar canjes."

### 2.4 Unhappy path — the member cannot be found

Not reachable through the normal S2 → S3 tap (a result row only exists for a real id), but
defensible against a stale back-navigation, a bookmarked URL, or a member deactivated between
searches. Once `GET /miembros/{id}/ficha-mostrador` exists with a real `Miembro` lookup, a missing
id is a `404`, and S3 shows:

> **"No encontramos ese socio."**
> "Puede que ya no esté disponible. Volvé a buscar."
> `[ Volver a la búsqueda ]` → S2, cache from §1.6 discarded (a different search was clearly
> needed)

### 2.5 Saldo en revisión (RN-25)

Unconditional — never mixed with the staleness badge's wording, because they are unrelated facts
(§0.4). When `saldo < 0`:

- The balance figure still renders (in `--color-warning`, not `--color-danger` — §4.4 of
  `DESIGN-SYSTEM.md` already made this call and this document does not revisit it) — never
  hidden, never replaced with "En revisión" here the way §1.3 does at the denser S2 row; S3 has
  room to be exact, and the cashier looking at one specific member benefits from seeing the real
  figure with its context.
- `REGISTRAR CANJE` renders `disabled` **and** `aria-disabled="true"`, and directly beneath it,
  always visible (never a hover reveal, per `DESIGN-SYSTEM.md` §8.2):
  > **"Saldo en revisión — consultá con la encargada."**
- The cashier is never asked to explain further, and the exact wording is `FUNCTIONAL-SPEC.md`
  §6's own text, unchanged.

### 2.6 Entry to S4

`REGISTRAR CANJE` opens the S4 dialog (§3) over this card. S3 stays mounted and visible behind it
(the point of a native `<dialog>` with an inert background, `DESIGN-SYSTEM.md` §9.2) — the
customer standing at the counter keeps seeing their own balance the entire time the cashier fills
the form, which is the "second pair of eyes" design decision this task named explicitly.

---

## 3. S4 · Registrar canje

### 3.1 Why a dialog, not a new screen

`DESIGN-SYSTEM.md` already assumes this shape without spelling it out: §6 calls it "the
confirm-redemption sheet" among the things that get elevation shadow, and §9.2 lists a dialog's
focus behaviour in the same table as S8's. This document makes it explicit: **S4 is a native
`<dialog>` opened from S3's `REGISTRAR CANJE` button**, not a page navigation. Two reasons beyond
following the hint:

1. **Speed.** A navigation is a round trip and a re-render of chrome the cashier does not need to
   see twice in one interaction; a dialog over the same card is one visual context, not two, and
   every screen change costs against the 15-second budget.
2. **The customer keeps watching their own balance** (§2.6) instead of it disappearing behind a
   new screen mid-transaction.

Unlike S8's destructive confirm, this dialog is **not** the "focus lands on cancel, never on the
destructive control" pattern — that pattern exists because `Enter` on reflex must not void a
movement (`DESIGN-SYSTEM.md` §9.2). Registering a canje is the opposite of a reflex risk: it is
validated server-side against the balance before it writes anything (I6), so focus lands on the
form's first field, exactly as §9.2's general rule says for any dialog with fields.

### 3.2 Layout

```
┌────────────────────────────────────────────┐
│  Registrar canje — Ana María Gómez      [×] │
│  Saldo disponible: $ 12.400                 │
│  Datos de ventas al 12/08/2026              │
│                                              │
│  Monto                                      │
│  [ $                                    ]   │
│  Ej: 1.500 = mil quinientos pesos           │
│                                              │
│  Fecha                                      │
│  [ 18/08/2026                     ▾ ]       │
│                                              │
│  Motivo                                     │
│  ( Descuento en la compra )                 │
│  ( Carga retroactiva )  ( Pedido especial ) │
│  ( Otro… )                                  │
│                                              │
│         [ Cancelar ]  [ Confirmar canje ]   │
└────────────────────────────────────────────┘
```

Focus on open: the **Monto** field. `<h2>`: `"Registrar canje — {Nombre}"`.

### 3.3 Fields

| Field | Control | Default | Notes |
| --- | --- | --- | --- |
| **Monto** | Text input, `inputmode="decimal"`, `$` prefix shown outside the field | Empty | Parsed by `MontoParser` in **typed mode** (`FUNCTIONAL-SPEC.md` §6): `.` is the thousands separator, `,` is the decimal separator, River Plate convention — no ambiguous case exists here the way it does for CSV ingestion (`CANONICAL-FORMAT.md` §5), because a human typing it is never export data. Helper caption below the field, always visible, not an error: `"Ej: 1.500 = mil quinientos pesos"` |
| **Fecha** | Native date picker | Today | May be set in the past (§6, retroactive redemption). **May not be set in the future** — a canje dated ahead of today has no operational meaning, since it is not export data being reconciled after the fact, it is a person choosing a date. This is a flow-level constraint, not a business rule from the RN catalog |
| **Motivo** | Preset chips (radio-group), `"Otro…"` opens a required text input | None selected | Mandatory always, unconditionally (the `RegistrarCanjeRequest.motivo` `[Required]`, per the discrepancy `REST-CONTRACT-F1.md` already resolved 2026-08-18 in favour of the code). The presets below are this document's invention — nothing in `DATA-MODEL.md` fixes a reason vocabulary, `Motivo` is free text |

**Preset reasons** (chips, tap instead of typing, per `FUNCTIONAL-SPEC.md` §6's own instruction):

- `"Descuento en la compra"` — the ordinary case, redeeming against what is being bought right now
- `"Carga retroactiva"` — **auto-highlighted, not auto-selected**, whenever Fecha is a past date;
  the cashier still taps to confirm it, but the nudge saves a read for the single most common
  reason a past date exists at all (§6, the power-cut scenario)
- `"Pedido especial"`
- `"Otro…"` — opens a plain required text field in its place; whatever is typed there becomes
  `motivo` verbatim

### 3.4 Validation — client-side, before a request is even sent

| Condition | Message |
| --- | --- |
| Monto empty | `"Ingresá un monto."` |
| Monto is `0` or negative | `"El monto tiene que ser mayor a $0."` |
| Monto not a recognisable number | `"No entendimos ese monto. Escribilo en números — por ejemplo 1.500."` |
| Fecha in the future | `"La fecha no puede ser posterior a hoy."` |
| Motivo not chosen, or "Otro…" left blank | `"Elegí o escribí un motivo."` |

Each renders with `.field-error` under its field, `--color-danger` border on the field (1px→2px,
per `DESIGN-SYSTEM.md` §11 — colour is never the only signal). `Confirmar canje` stays enabled and
re-validates on submit rather than disabling itself while the form is merely incomplete — a
cashier fixing the third field should not have to guess why the button will not respond, and a
disabled-until-perfect button hides *which* field is wrong instead of naming it.

### 3.5 What the server can still reject (I6, RN-24, RN-25)

Client-side validation cannot know the balance changed on another device a second ago, so the
server is always the real check:

| `errorCode` | Message shown |
| --- | --- |
| `MONTO_INVALIDO` | Same as the client-side "greater than $0" message — a defence-in-depth case, not expected to fire given §3.4 |
| `CANJE_SUPERA_SALDO` (I6/RN-24) | `"El canje ($15.000) es mayor que el saldo de Ana María Gómez ($12.400)."` — both figures named, exactly the standard this task's own brief set |
| `SALDO_EN_REVISION` (RN-25) | The balance went negative *while the dialog was open* (e.g. another cashier voided a sale in another branch mid-fill — rare, but the server check exists precisely because client state can go stale). Banner at the top of the dialog, `.badge--warning` styling: `"El saldo de Ana María Gómez quedó en revisión mientras completabas este canje. No se puede registrar hasta que la encargada lo revise."` `Confirmar canje` becomes disabled; `Cancelar` closes the dialog, and S3 behind it re-fetches the balance so it reflects the disabled state from §2.5 immediately |

Server errors render as a banner at the top of the dialog (`--color-danger-bg`/`--color-danger`
for the first two, `--color-warning-bg`/`--color-warning` for the third, per `DESIGN-SYSTEM.md`
§4.4's reasoning), **the fields keep every value the cashier typed** — nothing clears on a
rejected submit.

### 3.6 Submit, loading, and the double-submit hole

`Confirmar canje` on click or `Enter` (§9.4: single-purpose forms submit on `Enter`):

1. Button becomes `aria-busy="true"` **and** carries `disabled` in the markup, same footprint —
   the exact contract `DESIGN-SYSTEM.md` §9.5 spells out, closing the keyboard path a second
   `Enter` would otherwise still reach through `pointer-events: none` alone.
2. On success (`200`): dialog closes, S3 updates its balance figure immediately from
   `CanjeResponse.saldoResultante`, and a brief `.badge--success` confirmation shows on S3:
   `"Canje registrado."` Focus returns to the `REGISTRAR CANJE` button (§9.2's rule: a closed
   dialog returns focus to what opened it).
3. On a `400` (§3.5): banner shown, button re-enabled, focus moves to the banner so a screen
   reader announces it, fields intact.
4. On a network failure or a `5xx`: same as a `400` visually (banner, fields intact, button
   re-enabled), but the message is about connectivity, not the transaction:
   > **"No pudimos registrar el canje. Los datos que cargaste siguen acá — probá de nuevo."**
5. On `401` (session expired mid-fill — rare given the full-shift session model, but real): the
   dialog does not just vanish into a login redirect that would drop every field. Banner:
   > **"Tu sesión venció. Iniciá sesión de nuevo para continuar — no perdés lo que cargaste."**
   with an `"Iniciar sesión"` button. **Recommended pattern, not committed here**: re-authenticate
   through an in-place overlay (S1's fields, shown as another `<dialog>` layered above this one)
   so S4's `<dialog>` and its filled fields never unmount; a full page navigation to `/login`
   would lose them regardless of any client-side cache, the way §1.6's search cache cannot help a
   full reload. Confirm feasibility with `frontend-dev` before `F1-07` — flagged, not decided
   here, since it depends on how `F1-04c`'s session handling is actually built.

### 3.7 Esc, and the rule that a stray key must not lose a form

`DESIGN-SYSTEM.md` §9.4 states two things about `Esc` that read as being in tension for a dialog
that *is* a form: "`Esc` … closes a dialog" and "`Esc` … never discards a half-filled form." The
resolution: **closing is not discarding.** `Esc` (or `Cancelar`, or a click outside) closes the
dialog exactly as the keyboard contract says, but the three field values are cached in memory,
scoped to *this member's open S3 record*. Reopening `REGISTRAR CANJE` for the same member restores
them exactly as they were. The cache is dropped only when: the canje succeeds, or the cashier
navigates away to search for someone else (§1.6) — a different customer at the counter makes a
stale draft actively misleading, not merely unused.

### 3.8 A risk flagged, not resolved: retry after a lost response

§3.6's network-failure path assumes the failed request never reached the server, or the server
rolled it back. If a request actually succeeds server-side but its response is lost in transit
(a real possibility on a flaky connection, which is exactly the condition `ARCHITECTURE.md` §14
names for phase 1), the cashier sees the generic failure message, taps `Confirmar canje` again
with the same values, and — with nothing in `RegistrarCanjeRequest` today to make the request
idempotent — could produce **two** `Canje` movements instead of one. This is beyond what a UI can
close on its own; it needs either an idempotency key on the request or a "did this already
happen?" check before the retry fires, both backend decisions. `ARCHITECTURE.md` §14's own
wording ("never submits it twice") is most naturally read as covering the UI-level double-submit
`DESIGN-SYSTEM.md` §9.5 already closes, not this network-level case — flagging the gap rather
than assuming it is already covered.

---

## 4. S5 · Alta de socio

### 4.1 Entry points

- The primary path: S2's empty-result state (§1.5), `"¿Es un socio nuevo? Dar de alta →"` — the
  cashier already tried the name that does not exist yet.
- A secondary, always-present link on S2 itself, small and clearly secondary (never competing
  with the search field for attention): `"+ Dar de alta un socio nuevo"`, for a walk-in the
  cashier has not searched for at all.

Both go to the same full `.page` / `.form-column` screen — not a dialog. Unlike S4, this is not a
few-seconds counter interaction: it has up to eight fields and two consent decisions that need
room to be read, not rushed past in a sheet.

### 4.2 Layout and fields

`<h1>`: `"Alta de socio"`.

| Field | Required | Control | Notes |
| --- | --- | --- | --- |
| Nombre | Yes | Text | — |
| Código de cliente del POS | No | Text | Helper text, always visible under the field: `"Normalmente no lo vas a tener a mano. Podés dejarlo vacío — el socio queda dado de alta igual, pero no acumula puntos hasta que la encargada o el dueño lo vinculen desde 'Socios sin vincular'."` This is the "socio sin vincular" unhappy path the brief names, satisfied at the point `FUNCTIONAL-SPEC.md` §7 itself puts it: the form says it plainly, right here. The linking flow itself is `F1-14`, out of this document's scope |
| Teléfono | No | Text (`tel`) | — |
| DNI | No | Text | — |
| Fecha de nacimiento | No | Two selects: día, mes | Day and month only (RN-11) — no year field to leave blank or fill with a fake one |
| Sucursal | Conditional | Select | Only shown to `Encargada`/`Dueño`, who may serve more than one branch; a `Cajero` never sees it — their own branch is inferred server-side from the session, consistent with `FUNCTIONAL-SPEC.md` §13's "found and served normally" for cross-branch members not implying cross-branch *registration* choice for the role whose whole scope is one branch (§2, role table) |
| **Consentimiento de datos personales** | **Yes** | Checkbox, unticked by default | `[texto pendiente del dueño — FUNCTIONAL-SPEC §12]` |
| **Consentimiento de datos sensibles** | No | Checkbox, unticked by default | `[texto pendiente del dueño — FUNCTIONAL-SPEC §12]`. Ticking it does **not** reveal any diet/allergy fields in phase 1 — there are none yet (`PerfilMiembro` is phase 3, §2.2). What is recorded today is only the consent decision itself, ahead of having anything to gate with it, exactly as I10 requires: consent recorded before the field it will one day protect exists, never the other way around |

### 4.3 Validation

| Condition | Message |
| --- | --- |
| Nombre empty | `"Ingresá el nombre del socio."` |
| Consentimiento de datos personales unticked on submit | Submit blocked, focus moves to the checkbox: `"Para dar de alta al socio hace falta el consentimiento de datos personales."` |

No other field is required, matching `AltaMiembroRequest`. No duplicate-name check is designed
here — nothing in `DATA-MODEL.md` enforces name uniqueness and nothing in the brief asked for one;
inventing a "possible duplicate" flow would be exactly the scope creep `CLAUDE.md` warns against.

### 4.4 Submit, loading, failure

Same shape as S4 (§3.6), adapted to a full page rather than a dialog:

- `Crear socio` (`--control-size-primary`) becomes `aria-busy` + `disabled` on submit.
- Network/server failure: banner at the top of the form, **every field keeps its value**, button
  re-enabled: `"No pudimos registrar al socio. Los datos que cargaste siguen acá — probá de
  nuevo."`
- `400` validation failures from the server render the same way as §4.3's client-side messages,
  attached to the field named in `ErrorResponse.details[].field` when present.
- On success (`201`): navigate straight to the new member's S3 (§2), with a brief
  `.badge--success` on arrival: `"Socio registrado."` — the cashier lands exactly where they would
  need to be next if this member wants to redeem something immediately.

---

## 5. Copy glossary

Every literal Spanish string used above, in one place — for `frontend-dev` to grep against rather
than re-derive from prose.

| Context | Text |
| --- | --- |
| S2 hint, < 3 chars | Escribí al menos 3 letras para buscar. |
| S2 empty result | No encontramos a nadie con ese nombre. Probá con otro nombre o con menos letras. |
| S2 empty-result CTA | ¿Es un socio nuevo? Dar de alta → |
| S2 secondary CTA | + Dar de alta un socio nuevo |
| S2 search failed | No pudimos buscar. Revisá la conexión e intentá de nuevo. |
| S2/S3 negative balance (list) | En revisión |
| Stale badge, fresh | Datos de ventas al DD/MM/AAAA |
| Stale badge, warning | Datos de ventas al DD/MM/AAAA · hace N días |
| Stale, extra line | Puede haber compras más recientes todavía sin acreditar. |
| S3, no Corte | Todavía no hay datos de ventas cargados para este negocio. / Pedile a la encargada o al dueño que complete la primera carga antes de registrar canjes. |
| S3, member not found | No encontramos ese socio. / Puede que ya no esté disponible. Volvé a buscar. / Volver a la búsqueda |
| S3, saldo en revisión | Saldo en revisión — consultá con la encargada. |
| S4, monto helper | Ej: 1.500 = mil quinientos pesos |
| S4, monto empty | Ingresá un monto. |
| S4, monto ≤ 0 | El monto tiene que ser mayor a $0. |
| S4, monto ilegible | No entendimos ese monto. Escribilo en números — por ejemplo 1.500. |
| S4, fecha futura | La fecha no puede ser posterior a hoy. |
| S4, motivo faltante | Elegí o escribí un motivo. |
| S4, motivo preset | Descuento en la compra / Carga retroactiva / Pedido especial / Otro… |
| S4, supera saldo | El canje ($X) es mayor que el saldo de {Nombre} ($Y). |
| S4, saldo en revisión mid-fill | El saldo de {Nombre} quedó en revisión mientras completabas este canje. No se puede registrar hasta que la encargada lo revise. |
| S4, falla de red | No pudimos registrar el canje. Los datos que cargaste siguen acá — probá de nuevo. |
| S4, sesión vencida | Tu sesión venció. Iniciá sesión de nuevo para continuar — no perdés lo que cargaste. |
| S4, éxito | Canje registrado. |
| S5, clienteExternoId helper | Normalmente no lo vas a tener a mano. Podés dejarlo vacío — el socio queda dado de alta igual, pero no acumula puntos hasta que la encargada o el dueño lo vinculen desde "Socios sin vincular". |
| S5, nombre faltante | Ingresá el nombre del socio. |
| S5, sin consentimiento | Para dar de alta al socio hace falta el consentimiento de datos personales. |
| S5, falla de red | No pudimos registrar al socio. Los datos que cargaste siguen acá — probá de nuevo. |
| S5, éxito | Socio registrado. |

---

## 6. What is deliberately out of this document

- **The linking flow for unlinked members** ("Socios sin vincular" list, the actual linking
  action) — `F1-14`, a separate task. S5 only explains the state at the point of creation.
- **S6/S7/S8/S9** — manager and owner screens, different budget (density, not speed), not named
  in `F1-02`'s scope.
- **The allergy/diet and usual-purchases alert rows** beyond confirming their reserved slot and
  consent gate — no real fields exist to design against until `PerfilMiembro` (phase 3).
- **Consent wording** — both checkboxes stay `[texto pendiente del dueño]`.
- **Any change to `DESIGN-SYSTEM.md`'s tokens, states, or keyboard contract.**

---

## 7. Open questions and contradictions found — none resolved unilaterally

| # | What | Where | Why it matters |
| --- | --- | --- | --- |
| 1 | **`Corte.Fecha` is a fixed one-time program-start date, not a rolling "last import" date** — but the stale-data badge this task was named for needs the latter. §0.1 has the full reasoning | `DATA-MODEL.md` §3, `CANONICAL-FORMAT.md` §6 vs. `FUNCTIONAL-SPEC.md` §5, `DESIGN-SYSTEM.md` §11.1 | The badge as currently wired (`ICorteService.ObtenerCorteVigenteAsync`) would freeze at go-live and never again say anything true. This is the most important item on this list |
| 2 | `MiembroResumen` has no cutoff-date field, but R3's "not in any screen" reading requires one on S2's rows too | `openapi-fase1.yaml`, §1.4 | Needs a schema addition before `F1-05` builds S2 as specified here |
| 3 | Retry after a lost response on S4 submit could double-write a `Canje` — no idempotency mechanism exists in `RegistrarCanjeRequest` today | §3.8 | Money-affecting; needs a backend decision, not a UI one |
| 4 | Both consent texts | `FUNCTIONAL-SPEC.md` §12 | Already an open decision on record, restated here only because S5 depends on it directly |
| 5 | Re-authenticating in place (as an overlay) versus a full navigation on a mid-canje `401` | §3.6 point 5 | A recommendation, not a commitment — needs confirmation against how `F1-04c` actually implements session handling |

None of the above were decided by this document. Items 1–3 and 5 are technical; item 4 is the
owner's, already on record.
