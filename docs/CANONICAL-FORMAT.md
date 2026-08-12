# Canonical sales format

The single shape every sale takes before it touches the engine.

```
POS-specific adapter  →  canonical format  →  importer  →  engine
```

**Why this document exists.** While no POS exposes an API, the owner exports by hand. That is
fine — but the importer must read a format the future API can fill without changing a line
downstream. A "temporary" format means rewriting half the product when the API arrives
(Plan §3). Every decision below is made once, here.

**This is the product's competitive advantage and its largest maintenance cost.** Whoever solves
ingestion well wins; a pretty web app differentiates nobody.

---

## 1. Shape: one row per product line

A flat CSV, one row per **line item**, with the sale-level fields repeated on each row of the
same sale.

Two files (header + lines) would be cleaner in theory and worse in practice: a shop owner
exporting by hand from a POS report produces one file, not two, and cannot be asked to keep two
of them consistent.

```csv
venta_id,cliente_id,sucursal,fecha_venta,estado,total_venta,producto_codigo,producto_nombre,cantidad,precio_unitario,subtotal
10432,C-8891,pringles,2026-08-04,pagada,15400.00,7790001,Yerba Playadito 1kg,2,4200.00,8400.00
10432,C-8891,pringles,2026-08-04,pagada,15400.00,7790055,Harina sin TACC 1kg,2,3500.00,7000.00
10433,,pringles,2026-08-04,pagada,2300.00,7790120,Azucar 1kg,1,2300.00,2300.00
```

Sale `10433` has no `cliente_id`: the cashier did not identify the customer. It is imported
anyway — it is the R1 metric's denominator (see §6).

## 2. Columns

| Column | Required | Type | Rules |
| --- | --- | --- | --- |
| `venta_id` | ✅ | text | The POS's sale id. Stable across re-exports. **Not** the number printed on the receipt if the POS keeps them as separate fields |
| `cliente_id` | — | text | The POS's customer id. Empty = unidentified sale |
| `sucursal` | — | text | Branch code. Empty allowed for single-branch businesses |
| `fecha_venta` | ✅ | date | **`YYYY-MM-DD` only.** No other format is accepted (§4) |
| `estado` | ✅ | enum | `pagada` \| `pendiente` \| `anulada`. Nothing else (§3) |
| `total_venta` | ✅ | money | The sale's invoiced total. Repeated identically on every row of the sale |
| `producto_codigo` | — | text | SKU or internal code. Empty allowed |
| `producto_nombre` | ✅ | text | As the POS writes it. Stored verbatim, never discarded |
| `cantidad` | ✅ | decimal | Decimal on purpose — goods are sold by weight (`0.750`) |
| `precio_unitario` | ✅ | money | |
| `subtotal` | ✅ | money | |

**Header row is required.** Column order is irrelevant; names are matched after accent- and
case-folding (`VipNombres`). Unknown columns are ignored and reported in the import summary —
never a silent drop.

Encoding: UTF-8. Separator: comma. Fields containing a comma are double-quoted.

## 3. `estado` belongs to the adapter, not to the engine

Octaviano hard-coded `sales.status = 1` meaning "paid". That is a fact about one specific POS and
means nothing in another system (Plan §6).

The adapter maps its POS's codes to the three canonical values. The engine only ever sees
`pagada`, `pendiente`, `anulada`. A value outside those three is a rejected row, never a guess.

Only `pagada` accrues credit (RN-03).

## 4. Dates: `YYYY-MM-DD`, no tolerance

`04/08/2026` is 4 August in Argentina and 8 April in the United States, and nothing in the file
says which. A wrong month puts a movement in the wrong period and corrupts the monthly target.

The adapter converts. The canonical format accepts one format, and a row that does not match is
rejected with a message naming the expected format.

## 5. Money: the ambiguity rule

Amounts are parsed by `MontoParser` in **export mode**. The rules, in order — ported unchanged
from Octaviano, where they were derived against real data:

1. Strip `$`, spaces (including U+00A0). A leading `-` is tolerated.
2. Both `.` and `,` present → the rightmost is the decimal separator. `1.234,50` and `1,234.50`
   both yield `1234.50`.
3. One separator type, repeated → thousands. `1.234.567` → `1234567`.
4. One separator, appearing once → **the digit count to its right decides**. Not 3 digits
   (`1234,50`, `1234.5`, `1234.5678`) → decimal separator.
5. **Exactly 3 digits to the right (`1,234`, `1.234`) is genuinely ambiguous** — it can be one
   thousand two hundred thirty-four, or 1.234. Nothing in the file resolves it. **The row is
   rejected** with "ambiguous amount, re-export using a decimal point", and lands in
   `FilaRechazada` (I5).
6. No separators → plain integer.

> `NumberStyles.Any` is never used. It lets .NET accept any group size, so `1234,50` parsed as
> `123450` — a hundred times the real amount. Text is normalised by the rules above and only then
> parsed with `InvariantCulture` and a narrow `NumberStyles`.

**Best practice for the adapter:** emit amounts with a decimal point and no thousands separator
(`15400.00`). Then rule 5 can never fire.

## 6. Validation, per sale

A sale is accepted or rejected **as a whole**. Half a ticket is worse than no ticket: it produces
a total nobody can reconcile.

| Check | On failure |
| --- | --- |
| Sale-level fields (`cliente_id`, `sucursal`, `fecha_venta`, `estado`, `total_venta`) identical on every row of the same `venta_id` | Reject the sale. Contradictory rows mean a broken export |
| `SUM(subtotal)` equals `total_venta`, tolerance **± $0.01 per line** | Reject the sale, reporting both figures |
| `cantidad × precio_unitario` equals `subtotal`, same tolerance | Reject the sale |
| `fecha_venta` not in the future | Reject the sale |
| `fecha_venta` before the business's `Corte` | Skip silently — before the cutoff is not an error, it is out of scope |
| No `Corte` recorded for the business | **Abort the whole import.** Never invent a cutoff |

Everything rejected is preserved in `FilaRechazada` with the raw content and the reason. Strict
rejection is only useful if a person can see what was rejected.

## 7. Re-importing is expected, not exceptional

A sale can change after being read: pending → paid, paid → voided, or its total corrected. An
importer that only ever appends never sees those changes.

- Sales are matched on `(NegocioId, venta_id)`. A second import **updates** the sale's status,
  total and lines.
- A change to an already-credited sale produces an **`Ajuste` movement** for the difference. The
  original `Acumulacion` is never edited (I1, I3).
- A unique index on `(NegocioId, MiembroId, Tipo=Acumulacion, ReferenciaVenta)` makes double
  crediting impossible at the database level, not merely unlikely.
- Octaviano re-read a 60-day window on every sync for this reason. That window becomes
  configuration here.

## 8. The identification metric (R1)

Every import reports **`% of sales carrying a `cliente_id`**, and the product surfaces it as a
first-class program metric.

If that number is low, no amount of software helps: the problem is that cashiers are not asking
who the customer is, and the whole product depends on that one gesture at the counter. It has to
be said out loud, before selling anything (Plan §11, R1).

## 9. Adapters

| Adapter | Status |
| --- | --- |
| `PosOctaviano` | First one. Maps the current shop's export |
| `Canonico` | Pass-through, for a file already in this format |
| Second POS | **Not built until a real client asks.** Building it earlier is guesswork (Plan §12) |

An adapter may only: rename columns, map `estado` codes, convert date formats, split or join
fields, and normalise amounts. An adapter that computes credit, filters members, or decides what
counts as paid has business logic in the wrong layer.

**No adapter connects to a POS database.** Ingestion is by file or API, always (I9).

## 10. Open decisions

1. **Tolerance on the `SUM(subtotal)` check** — ±$0.01 per line is proposed. POS systems that
   round per line versus per ticket may need more.
2. **Returns / credit notes.** Whether they arrive as a fourth `estado`, as negative quantities,
   or not at all. Unknown until a real export is seen.

## 11. Decided — a sale voided after its credit was redeemed

Decided 2026-08-12. Now **RN-25**; recorded here because ingestion is where the case arises.

A sale is credited, the member redeems that credit, and the sale is then voided. The correcting
`Ajuste` pushes the balance below zero.

The importer writes the `Ajuste` anyway. The ledger records what happened — that is not
negotiable (I1, I3). The consequence is contained instead:

- The balance may go negative **only** through a system-generated `Ajuste`. A `Canje` never can
  (RN-24, I6).
- While negative, **new redemptions are blocked** for that member.
- The manager is notified, with both the voided sale and the redemption that preceded it.
- The counter screen shows *"saldo en revisión"* — never a negative number presented to the
  member as a debt.
