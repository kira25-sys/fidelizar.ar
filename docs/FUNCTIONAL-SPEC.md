# Functional specification

How the application behaves, screen by screen. The *what*, not the *how* — implementation
belongs in [ARCHITECTURE.md](ARCHITECTURE.md).

UI text is **Spanish (Argentina)**. Cashiers do not speak English.

---

## 1. The design constraint everything else follows from

**The primary user is a cashier with a customer waiting in front of them.**

That single fact decides more than any preference:

- The counter flow must complete in **under 15 seconds** and in **under 4 steps**.
- Search is the entry point. There is no navigation to learn.
- Nothing that requires reading a paragraph belongs on the counter screen.
- Targets sized for a mouse first and a finger second: the counter runs on the computer the shop
  already has, and the same screen has to stay usable on a phone.
- The screen is visible to the customer standing there. **Never render a phone number or DNI on
  the counter screen** (§4, and Plan §5).

The manager's and owner's screens have the opposite budget: they are used sitting down, and
density beats speed there.

## 2. Roles

| Role | Scope |
| --- | --- |
| **Cajero** | Their own branch. Search, view, redeem |
| **Encargada** | Their own branch, in full. Plus history, voids, reports |
| **Dueño** | Everything, every branch. Configuration, imports, global reports |
| **Soporte** | Audited technical access |

## 3. Screen map

| # | Screen | Cajero | Encargada | Dueño | Phase |
| --- | --- | :---: | :---: | :---: | :---: |
| S1 | Ingreso | ✅ | ✅ | ✅ | 1 |
| S2 | Buscar socio | ✅ | ✅ | ✅ | 1 |
| S3 | Ficha del socio (counter view) | ✅ | ✅ | ✅ | 1 |
| S4 | Registrar canje | ✅ | ✅ | ✅ | 1 |
| S5 | Alta de socio | ✅ | ✅ | ✅ | 1 |
| S6 | Ficha completa | — | ✅ | ✅ | 1 |
| S7 | Historial de movimientos | — | ✅ | ✅ | 1 |
| S8 | Anular movimiento | — | ✅ | ✅ | 1 |
| S9 | Cierre diario de canjes | — | ✅ | ✅ | 1 |
| S10 | Usuarios y sucursales | — | — | ✅ | 1 |
| S11 | Importar ventas | — | — | ✅ | 2 |
| S12 | Configuración del programa | — | — | ✅ | 2 |
| S13 | Perfil y preferencias | — | ✅ | ✅ | 3 |
| S14 | Autogestión del socio (public link/QR) | — | — | — | 3 |
| S15 | Reportes | — | branch | global | 4 |

---

## 4. S2 · Buscar socio — the entry point

The cashier's home screen. Nothing else on it.

- A single text field, focused on load, so the cashier types straight away — and on a touch
  device the on-screen keyboard comes up on its own.
- Searches `NombreNormalizado` — accent- and case-insensitive, matching on any word
  (`"gomez ana"` finds `"Ana María Gómez"`).
- Results appear as the cashier types, from the 3rd character.

**Each result shows: name, member number, balance. Nothing else.**

> **There is no "all members" screen, for any role below Dueño.** A scrollable list of hundreds
> of people is a leak waiting to happen and adds nothing at the counter (Plan §5). This is an
> architectural decision, not a UI choice — do not add one later "for convenience".

### Homonyms — the biggest usability win in the whole change

Octaviano's bot refused to choose between two similar names and asked the operator to be more
specific. That was correct: choosing wrong credits money to the wrong person.

On a web screen, **both candidates are simply shown and the cashier taps the right one**. Same
safety, none of the friction. It costs nothing extra to build (Plan §5).

When two results are genuinely indistinguishable by name, each row also shows the member number
and the registration date — enough to disambiguate without exposing a phone or a DNI.

## 5. S3 · Ficha del socio — the counter view

The screen the customer can see over the cashier's shoulder. Everything on it is deliberate.

```
┌──────────────────────────────────────────────┐
│  Juana Pérez                    Socia #0142  │
│                                              │
│      SALDO DISPONIBLE                        │
│      $ 12.400                                │
│      Datos de ventas al 04/08/2026  ⓘ        │
│                                              │
│  🎂 Cumple el 4/9                            │
│  ⚠️  Celíaca                                  │
│  🛒 Suele llevar yerba y harina sin TACC     │
│                                              │
│  [   REGISTRAR CANJE   ]                     │
└──────────────────────────────────────────────┘
```

**Visible to the cashier:** name, member number, balance, cutoff date, alerts, the redeem button.

**Never visible to the cashier:** phone, DNI, full date of birth, full movement history, any
other member.

### The cutoff date is not decoration (R3)

Sales data arrives weekly and by hand, but a web page looks live. The as-of date must be **more
prominent here than the bot made it, not less** (Plan §11).

- Under 7 days old: neutral grey.
- Over 7 days old: a visible warning — the data is stale and whoever is looking should know.
- Never render a balance without its cutoff date next to it. Not in any screen, not in any
  report, not in any export.

### The alert strip

This is the product's whole notification strategy in phase 1: **zero cost, zero approvals, and
it converts better at the counter than any message** (Plan §8). The customer is already there.

Alerts, in priority order: unredeemed balance · upcoming birthday (2 days ahead, RN-11) ·
allergy or diet · usual purchases (phase 4) · about to lose points (phase 4, only when the grace
streak is enabled).

Allergy and diet alerts render **only** when a `DatosSensibles` consent is on record (I10).

## 6. S4 · Registrar canje

Reached from S3. One screen, three fields.

| Field | Rules |
| --- | --- |
| **Monto** | Parsed with `MontoParser` in **typed mode** — the person is known to be an Argentine operator, so `1.500` → 1500 and `1,500` → 1.5 by River Plate convention. Rejected if `≤ 0`, or **greater than the balance** (RN-24, I6). The error names both figures |
| **Fecha** | Defaults to today. **May be set in the past** (§7 below) |
| **Motivo** | **Mandatory, always** — a redemption dated today needs one too (DATA-MODEL §4, enforced by `MovimientoCredito.Crear`). Every line of the ledger says why it exists; that is what makes the balance defensible. S4 offers preset reasons so the cashier taps instead of typing |

On confirmation: a `Canje` movement is written with a negative amount, the acting user's id, and
the effective date. The new balance is shown immediately.

Nothing is ever edited or deleted. A mistake is corrected by S8, which writes an `Ajuste`.

### Balance under review (RN-25)

When a member's balance is negative — which only happens when a credited sale was voided *after*
its credit had been redeemed — **the redeem button is disabled** and the screen says
*"Saldo en revisión — consultá con la encargada"*.

The cashier is never asked to explain it and the member is never shown a negative number as a
debt. The manager gets the detail: the voided sale, the redemption that preceded it, and both
amounts.

### Retroactive redemption replaces offline sync

Every branch has internet. Real offline synchronisation — a local queue, conflict resolution,
divergent balances between branches — is among the most expensive things to build well and, with
money involved, among the most dangerous to build badly.

**The 5% solution:** allow a past date plus a mandatory reason. Power cut → written on paper →
loaded afterwards with the real date. The ledger already separates `FechaEfectiva` from
`RegistradoEn`, so the model supports it with no change (Plan §9).

## 7. S5 · Alta de socio — consent is part of the form

A member cannot be registered without an explicit, recorded consent decision. Not a
pre-ticked box, not buried in a link (Plan §7).

| Field | Required | Notes |
| --- | --- | --- |
| Nombre | ✅ | |
| `ClienteExternoId` | — | Optional at registration: the id is born in the POS and the cashier usually does not have it. The member is created **unlinked** and accrues nothing until `Encargada`/`Dueno` links the POS id from the "socios sin vincular" list. The form says this plainly |
| Teléfono, DNI | — | |
| Fecha de nacimiento | — | Day and month only (RN-11) |
| **Consentimiento de datos personales** | ✅ | Explicit checkbox. Stores date, wording version and who recorded it |
| **Consentimiento de datos sensibles** | — | Separate checkbox. **Without it, diet and allergy fields are not shown at all** (I10) |

Diet and allergies are health data. Law 25.326 classifies them as sensitive and requires express,
informed consent. With 293 members of one's own you can improvise; selling to third parties you
cannot — the client business is the data controller and the product has to give it the tools to
comply.

> Retrofitting consent onto thousands of loaded members is miserable work and leaves a legal hole
> in the meantime. This is one of the few things that must not be deferred (Plan §7).

The member may later request **deletion** and an **export** of their data. Both are S13
functions, both write to `RegistroAuditoria`.

## 8. S8 · Anular movimiento — corrections, not edits

Only `Encargada` and `Dueno`.

There is no "edit" and no "delete", anywhere in the product. Voiding movement *M* writes a **new**
`Ajuste` movement of `-M.Monto`, with a mandatory reason and the acting user. Both rows remain
visible in the history, the second marked as the correction of the first.

This is invariant I1 surfaced in the UI. A user who can make a row disappear can make money
disappear, and the balance stops being defensible.

## 9. S9 · Cierre diario de canjes

For the manager. Today's redemptions in her branch: member, amount, cashier, time, reason.
Totals at the foot. Exportable.

This is what replaces the Trello board the manager keeps by hand today — same information, with
who, when and why attached.

## 10. S11 · Importar ventas

Owner and support only.

1. Upload the file and pick the adapter.
2. **Dry run first, always.** The system reports: rows read, sales detected, sales accepted,
   sales rejected with reasons, **% of sales carrying a `cliente_id`** (§8 of
   [CANONICAL-FORMAT.md](CANONICAL-FORMAT.md)), and the credit that would be posted.
3. Only then may the import be confirmed.

Rejected rows are downloadable with their raw content and the rejection reason. Import history is
permanent.

If the business has no `Corte` recorded, the import refuses to run and says so. It never invents
one.

## 11. What is deliberately absent

| Not built | Why |
| --- | --- |
| A full member listing for cashiers | Privacy. It is a leak with no operational value (§4) |
| Edit / delete on any movement | The ledger is append-only (I1) |
| Offline mode | Retroactive redemption solves the real case for 5% of the cost (§6) |
| WhatsApp / email / SMS in phases 1–3 | The cashier's screen costs nothing and converts better. WhatsApp is a later premium tier (Plan §8) |
| Automatic point expiry | The system **proposes**, a person decides (RN-20). Automatic confiscation of what the member sees as money destroys the program |
| A mobile app | A responsive web page is the whole requirement: the shop's computer first, phone and tablet on the same page |

## 12. Open decisions

None. The one item that used to live here — the wording of the two consent texts — was approved
by the owner 2026-08-19; see §13.4.

## 13. Decided (2026-08-12)

1. **The balance is always visible on the counter screen.** The customer seeing it is part of
   the sales hook (RN-12): unredeemed money in sight invites a redemption. No reveal tap.
2. **Session: full login per shift.** Each cashier signs in with email and password at the start
   of their shift and signs out at the end. No short auto-lock. Known trade-off, accepted by the
   owner: if a cashier forgets to sign out, movements are stamped to whoever is logged in.
3. **A member of another branch is found and served normally.** The search returns members of
   every branch and a redemption can be registered anywhere — the program is one and the monthly
   target is global (RN-07).
4. **The wording of the two consent texts (decided 2026-08-19).** Both approved, **provisional
   until production** — to be reviewed again before the first paying client. The texts are a fixed
   template in code; the business's `Razón Social`, `CUIT` and (for `DatosPersonales`) `Domicilio`
   are resolved from that business's own `Negocio` row at render time, never written as literals.
   The asymmetry between them is substantive, not stylistic, and every write path has to respect
   it: `DatosPersonales` says alta is impossible without it — mandatory, alta rejected;
   `DatosSensibles` says membership is possible without it and that it is revocable at any time
   with no effect on the account — optional, alta accepted, and revoking it never touches the
   member or the ledger.
