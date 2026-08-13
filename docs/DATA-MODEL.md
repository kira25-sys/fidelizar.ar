# Data model

PostgreSQL. Entity Framework Core migrations are the schema's source of truth.

Read [ARCHITECTURE.md](ARCHITECTURE.md) §4 first — the invariants below are not restated here,
they are assumed.

**Two rules that apply to every table without exception:**

- `NegocioId` is present, not nullable, and indexed — even though each business gets its own
  database (I8).
- Timestamps are `timestamptz`, stored in UTC. Dates that represent a business day and not an
  instant (a sale's date, a birthday, a cutoff) are `date`, never `timestamp`.

---

## 1. Tenancy and configuration

### `Negocio`

The client business. In a single-tenant deployment there is exactly one row, and it still exists
— everything else references it.

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int` PK | |
| `Nombre` | `text` | |
| `Cuit` | `text?` | |
| `Activo` | `bool` | |
| `CreadoEn` | `timestamptz` | |

### `ConfiguracionPrograma`

The numbers that were `const` in Octaviano (Plan §6). **Versioned, never overwritten.**

A configuration row is closed and a new one opened whenever the program changes. Without this, a
balance computed last year under a 3% rule becomes inexplicable after the owner switches to 5% —
and "inexplicable balance" is the one failure this product cannot survive.

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int` PK | |
| `NegocioId` | `int` FK | |
| `PorcentajeAcumulacion` | `numeric(5,4)` | RN-01. `0.0300` = 3% |
| `ObjetivoMensual` | `numeric(14,2)?` | RN-06. **Null = the program has no target** |
| `GraciaHabilitada` | `bool` | RN-16..RN-23. Default **false** |
| `MesesDeGracia` | `int?` | RN-16, default 3 when enabled |
| `UmbralMesMalo` | `numeric(14,2)?` | RN-17 |
| `TopeMesesCongelados` | `int?` | RN-23 — default **3** when grace is enabled (decided 2026-08-12) |
| `VigenteDesde` | `date` | |
| `VigenteHasta` | `date?` | Null = current |
| `CreadoPorUsuarioId` | `int` FK | |

Partial unique index on `(NegocioId)` where `VigenteHasta` is null — there is exactly one
current configuration per business, enforced by the database, not by discipline.

> Rounding is **not** here. It is fixed in code: 2 decimals, `AwayFromZero` (I4).

### `Sucursal`

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int` PK | |
| `NegocioId` | `int` FK | |
| `Nombre` | `text` | |
| `CodigoExterno` | `text?` | How the POS names this branch |
| `Activa` | `bool` | |

> Branches are organisational, never a calculation boundary. The monthly target sums every
> branch (RN-07). Any query that filters totals by branch is a defect.

---

## 2. Identity

### `Usuario`

Replaces Octaviano's list of Telegram chat IDs (Plan §5).

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int` PK | |
| `NegocioId` | `int` FK | |
| `NombreCompleto` | `text` | What gets stamped on a movement |
| `Email` | `citext` | Unique per `NegocioId` |
| `PasswordHash` | `text` | ASP.NET Core Identity hashing |
| `Rol` | `int` | `Cajero=0, Encargada=1, Dueno=2, Soporte=3`. **No `ñ` in the identifier** — "Dueño" is UI text only. Non-ASCII identifiers snag on URLs, grep and tooling, and the rest of the model already folds accents (`Acumulacion`) |
| `SucursalId` | `int?` FK | Required for `Cajero` and `Encargada`; null for `Dueno`/`Soporte` |
| `Activo` | `bool` | Deactivation, never deletion — movements reference this row forever |
| `CreadoEn` | `timestamptz` | |

### `RegistroAuditoria`

Support access is audited (Plan §5, R6), and so is every read of sensitive data.

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `bigint` PK | |
| `NegocioId` | `int` FK | |
| `UsuarioId` | `int` FK | |
| `Accion` | `text` | `VerFichaCompleta`, `ExportarDatos`, `AnularMovimiento`, … |
| `EntidadTipo` / `EntidadId` | `text` / `int?` | |
| `Detalle` | `jsonb?` | |
| `OcurridoEn` | `timestamptz` | |

Append-only, like the ledger.

---

## 3. Members

### `Miembro`

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int` PK | |
| `NegocioId` | `int` FK | |
| `ClienteExternoId` | `text?` | The POS customer id. **Nullable**: a member registered at the counter starts unlinked and **accrues nothing until linked** — the id is born in the POS, not in the web. `Encargada`/`Dueno` link it from a "socios sin vincular" list. `text`, not `int`: other POS systems use non-numeric ids |
| `NumeroSocio` | `text?` | Informational |
| `Nombre` | `text` | |
| `NombreNormalizado` | `text` | Accent- and case-folded, indexed. Feeds the search |
| `Telefono` | `text?` | Informational. **Never an identity key** — mixed formats and shared numbers |
| `Dni` | `text?` | Informational. **Never an identity key** — unevenly loaded in the POS |
| `FechaNacimiento` | `date?` | Only day and month matter (RN-11); the year is ignored |
| `SucursalId` | `int?` FK | Organisational (RN-07) |
| `FechaAlta` | `date` | |
| `Activo` | `bool` | Deactivates without erasing history |
| `ActualizadoEn` | `timestamptz` | |

Partial unique index on `(NegocioId, ClienteExternoId)` where `ClienteExternoId` is not null.

### `Consentimiento`

Built from phase 1, never retrofitted (Plan §7, invariant I10).

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int` PK | |
| `NegocioId` | `int` FK | |
| `MiembroId` | `int` FK | |
| `Tipo` | `int` | `DatosPersonales=0, DatosSensibles=1, Comunicaciones=2` |
| `Otorgado` | `bool` | A withdrawal is a **new row** with `false`, not an update |
| `VersionTexto` | `text` | Which wording the member agreed to |
| `Canal` | `int` | `Mostrador=0, Autogestion=1, MigracionVerbal=2` |
| `RegistradoPorUsuarioId` | `int?` FK | Null when self-service |
| `OcurridoEn` | `timestamptz` | |

Append-only. Current consent is the newest row per `(MiembroId, Tipo)`.

### `PerfilMiembro`

Diet, allergies, tastes. **A separate table on purpose**, not columns on `Miembro`: this is
health data under Law 25.326 and keeping it physically apart makes encryption at rest, access
auditing and a deletion request each a bounded operation instead of a surgical one.

| Column | Type | Notes |
| --- | --- | --- |
| `MiembroId` | `int` PK/FK | 1:1 |
| `NegocioId` | `int` FK | |
| `Dieta` | `text?` | |
| `Alergias` | `text?` | |
| `Preferencias` | `text?` | |
| `NotasCajero` | `text?` | |
| `ActualizadoEn` | `timestamptz` | |
| `ActualizadoPorUsuarioId` | `int?` FK | |

**No row may exist here without a current `Consentimiento` of type `DatosSensibles`.** Enforced
in the domain service and covered by a test (I10).

---

## 4. The ledger

### `MovimientoCredito`

The heart of the product. Append-only, no `UPDATE`, no `DELETE`, ever (I1).

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `bigint` PK | |
| `NegocioId` | `int` FK | |
| `MiembroId` | `int` FK | |
| `FechaEfectiva` | `date` | **When it happened in the real world.** A redemption written on paper during a power cut carries the paper's date |
| `RegistradoEn` | `timestamptz` | **When the system learned about it.** Always "now" |
| `Periodo` | `char(7)` | `YYYY-MM` of `FechaEfectiva` |
| `Tipo` | `int` | `SaldoInicial=0, Acumulacion=1, Canje=2, Ajuste=3`. Persisted as int — **never reorder or reuse a number; append only** |
| `Monto` | `numeric(14,2)` | Positive adds, negative subtracts. Always `decimal` (I4) |
| `ReferenciaVenta` | `text?` | The sale's external id. Null for `SaldoInicial` and `Canje` |
| `UsuarioId` | `int?` FK | Who caused it. Null only for `sistema` |
| `Motivo` | `text?` | **Mandatory** for `Canje`, `Ajuste`, and any movement with `FechaEfectiva < today` |
| `SaldoResultante` | `numeric(14,2)` | Historical evidence only. Never the source of an answer (I2). Computed inside the same transaction as the insert |
| `ConfiguracionId` | `int?` FK | Which program configuration produced this movement. Makes an old balance explainable. **Mandatory for `Acumulacion`**; null allowed for the other types |

Indexes: `(NegocioId, MiembroId)`, `(NegocioId, Periodo)`, unique on
`(NegocioId, MiembroId, Tipo, ReferenciaVenta)` where `Tipo = Acumulacion` — the same sale can
never be credited twice.

> **A negative sum is possible, and only one thing can cause it** (RN-25): a system-generated
> `Ajuste` for a sale voided after the member had already redeemed its credit. A `Canje` can never
> produce one (RN-24). While the sum is negative, redemptions for that member are blocked and the
> manager is notified. There is no "negative balance" column — as always, the balance is the sum.

> **Why `FechaEfectiva` is new.** Plan §9 says the ledger already separates a movement's date
> from its registration. In Octaviano that separation exists only at month granularity
> (`Periodo`). A retroactive redemption needs the actual day, so the distinction is made explicit
> here. It costs one column and removes the ambiguity entirely.

### `Corte`

The program's start date — from when the system counts. **One per business now**, not global
(Plan §6).

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int` PK | |
| `NegocioId` | `int` FK | Unique — the schema, not discipline, guarantees one cutoff per business |
| `Fecha` | `date` | |
| `DeclaradoPorUsuarioId` | `int` FK | |
| `DeclaradoEn` | `timestamptz` | |

Declared at import, never a constant. Octaviano learned this the hard way: a hard-coded cutoff
double-credits every purchase between the real cutoff and the import day. **With no cutoff
recorded, accrual must fail loudly rather than invent one.**

---

## 5. Sales ingestion

Everything here is written by the importer from the canonical format
([CANONICAL-FORMAT.md](CANONICAL-FORMAT.md)). No POS-specific concept reaches these tables.

### `Venta`

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `bigint` PK | |
| `NegocioId` | `int` FK | |
| `VentaExternaId` | `text` | The POS's sale id |
| `ClienteExternoId` | `text?` | **Null when the cashier did not identify the customer** — the R1 metric counts exactly these |
| `SucursalId` | `int?` FK | |
| `FechaVenta` | `date` | |
| `Estado` | `int` | Canonical: `Pagada=0, Pendiente=1, Anulada=2`. The adapter maps the POS's codes |
| `Total` | `numeric(14,2)` | |
| `LoteImportacionId` | `int` FK | |

Unique on `(NegocioId, VentaExternaId)`. A re-import updates status and total — a sale can change
after being read (paid → voided) and the accrual must be corrected with an `Ajuste`, never by
editing the original movement.

### `VentaLinea`

The reason phase 2 exists. Without these rows the product sells nothing but a balance (Plan §3).

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `bigint` PK | |
| `NegocioId` | `int` FK | |
| `VentaId` | `bigint` FK | |
| `ProductoId` | `int?` FK | Null when the product could not be matched — the line is still kept |
| `DescripcionOriginal` | `text` | Exactly as the POS wrote it. Never discarded |
| `Cantidad` | `numeric(12,3)` | Decimal: things are sold by weight |
| `PrecioUnitario` | `numeric(14,2)` | |
| `Subtotal` | `numeric(14,2)` | |

### `Producto`

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `int` PK | |
| `NegocioId` | `int` FK | |
| `CodigoExterno` | `text?` | |
| `Nombre` | `text` | |
| `NombreNormalizado` | `text` | |
| `Categoria` | `text?` | |
| `Activo` | `bool` | |

### `LoteImportacion` and `FilaRechazada`

Every import is auditable and every rejected row is preserved. This is what makes invariant I5
useful instead of merely strict: rejecting an ambiguous amount only helps if someone can see
what was rejected and why.

`LoteImportacion`: `Id`, `NegocioId`, `NombreArchivo`, `Adaptador`, `CorteDeclarado`,
`FilasLeidas`, `FilasAceptadas`, `FilasRechazadas`, `EjecutadoPorUsuarioId`, `EjecutadoEn`,
`Estado`.

`FilaRechazada`: `Id`, `NegocioId`, `LoteImportacionId`, `NumeroFila`, `Motivo`,
`ContenidoCrudo`.

### `VentaMensualCache`

Derived, disposable, rebuildable from `Venta`. Powers "how much did they buy this month" without
a scan.

`NegocioId`, `ClienteExternoId`, `Periodo`, `TotalPagado`, `CantidadCompras`, `UltimaCompra`,
`CapturadoEn`. PK `(NegocioId, ClienteExternoId, Periodo)`.

> This table is a cache and must never be read as truth about credit. The balance is the ledger
> (I2). If the two disagree, the ledger is right and the cache is stale.

---

## 6. Open decisions

None at the moment. Former open decisions are in §7.

## 7. Decided

**`Producto` matching: external code first, normalised name as fallback** (2026-08-12). A line
with a `producto_codigo` matches (or creates) the product by code; without a code it matches by
exact normalised name. A line whose product cannot be matched is kept with `ProductoId = null`
and `DescripcionOriginal` is never lost.

**`Sucursal` is configured by hand; the import is strict** (2026-08-12). The owner creates
branches in S10 with their `CodigoExterno`. An import carrying an unknown branch code rejects
those sales and reports them — a typo in an export must never create a phantom branch.

**Accrual is written per sale at import time** (2026-08-12). RN-22's "month end" wording is
superseded: each paid sale writes its `Acumulacion` when the import runs, so the balance is
current as of the last import. The manager's monthly veto is implemented as an `Ajuste`.

**`ClienteExternoId` is nullable; counter registrations start unlinked** (2026-08-12). A member
created at the counter (S5) has no POS id yet and accrues nothing until `Encargada`/`Dueno`
links one from the "socios sin vincular" list.

**The 293 existing members' consent is migrated as verbal** (2026-08-12). They consented
verbally when joining; the phase-0 migration writes `Consentimiento` rows for all types —
including `DatosSensibles` — with `Canal = MigracionVerbal` and the member's `FechaAlta`.
Known caveat, accepted by the owner: verbal consent is weaker evidence than Law 25.326 prefers
for health data; any member passing through the counter can be re-consented explicitly, which
supersedes the migrated row (consent is append-only).

**`ClienteExternoId` is `text`, not `int`** (2026-08-12). Octaviano used `int` because that one
POS used integers. As `text`, a future client whose POS issues alphanumeric ids (`A-8891`) does
not force a migration of the sales tables and the ledger. The cost is a marginally larger index.

**`ConfiguracionPrograma` is versioned, never overwritten** (2026-08-12). Each change closes the
current row and opens a new one; every movement stores the `ConfiguracionId` that produced it. A
balance accumulated at 3% stays explainable after the owner moves to 5%. This cannot be
retrofitted — once the history is written under a mutable row, the information is simply gone.
