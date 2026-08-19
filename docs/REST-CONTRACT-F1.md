# REST contract — Phase 1

The seam ARCHITECTURE §3 talks about: once this is agreed, backend and frontend work stop
waiting on each other. The full contract — every route, DTO and status code for S1 through S10
— lives in [api/openapi-fase1.yaml](api/openapi-fase1.yaml). This document is the narrative
version: what is real today, what is only a documented shape, and which Application service backs
each endpoint.

Hand-written, not generated: adding `Microsoft.AspNetCore.OpenApi` would be a new dependency, and
CLAUDE.md says to ask before adding one. A static YAML file needs none.

**Originally F1-04b implemented only S1, S3's balance piece and S4.**
`feat/f1-backend-endpoints-pendientes` closed the rest of phase 1's endpoint table — S2, S3's full
counter view, S6, S7, S8, S9 and S10 — plus the "$0 fantasma" defect `ObtenerSaldo` used to
document instead of fix. **S5 Alta de socio stays pending on purpose**: it needs to compose
`Miembro` and `Consentimiento` atomically (I10), and another branch was working on
`Consentimiento` at the same time — see the note at the end of this document.

## Endpoint table

| Screen | Route | Verb | Who | Status | Backed by |
| --- | --- | --- | --- | --- | --- |
| S1 Ingreso | `/api/auth/csrf-token` | GET | anyone | **Implemented** | — |
| S1 Ingreso | `/api/auth/login` | POST | anyone | **Implemented** | `IAuthService` |
| S1 Ingreso | `/api/auth/logout` | POST | any session | **Implemented** | — |
| S1 Ingreso | `/api/auth/me` | GET | any session | **Implemented** | — |
| S2 Buscar socio | `/api/miembros?q=` | GET | `CajeroOrAbove` | **Implemented** | `IMiembroBusquedaService` |
| S3 Ficha del socio | `/api/miembros/{id}/saldo` | GET | `CajeroOrAbove` | **Implemented** | `ISaldoService` + `ICorteService` |
| S3 Ficha del socio | `/api/miembros/{id}/ficha-mostrador` | GET | `CajeroOrAbove` | **Implemented** | `IFichaMostradorService` |
| S4 Registrar canje | `/api/miembros/{id}/canjes` | POST | `CajeroOrAbove` | **Implemented** | `ISaldoService.RegistrarCanjeAsync` |
| S5 Alta de socio | `/api/miembros` | POST | `CajeroOrAbove` | Pending | member+consent creation use case — blocked on `feat/f1-08-consentimiento` |
| S6 Ficha completa | `/api/miembros/{id}/completo` | GET | `EncargadaOrAbove` | **Implemented** | `IFichaCompletaService` |
| S7 Historial de movimientos | `/api/miembros/{id}/movimientos` | GET | `EncargadaOrAbove` | **Implemented** | `IHistorialMovimientosService` |
| S8 Anular movimiento | `/api/movimientos/{id}/anular` | POST | `EncargadaOrAbove` | **Implemented** | `IAnulacionMovimientoService` |
| S9 Cierre diario | `/api/sucursales/{id}/cierre-diario` | GET | `EncargadaOrAbove` | **Implemented** | `ICierreDiarioService` |
| S10 Usuarios | `/api/usuarios` | GET / POST | `DuenoOnly` | **Implemented** | `IUsuarioService` |
| S10 Sucursales | `/api/sucursales` | GET / POST | `DuenoOnly` | **Implemented** | `ISucursalService` |

Every state-changing route (`POST`) requires the `X-CSRF-TOKEN` header from
`GET /api/auth/csrf-token` (`AntiforgeryTokenRequiredAttribute`, ARCHITECTURE §8). Every route
requires a valid session; the specific policy column above is what F1-15 will verify with negative
tests per role, run against the live HTTP pipeline. What this branch verifies (controller-level
unit tests, no HTTP, no database — ARCHITECTURE §11) is that every action declares its policy
explicitly and calls through to the real service with `NegocioId` and the acting user's id taken
from the token, never from the URL or the body.

## What each endpoint is backed by

- **`MiembrosController.ObtenerSaldo`** (`GET /api/miembros/{id}/saldo`) calls
  `ISaldoService.ObtenerSaldoAsync` and `ICorteService.ObtenerCorteVigenteAsync`, combining them
  because ARCHITECTURE §13 R3 forbids showing a balance without its cutoff date next to it.
  **The "$0 fantasma" defect is fixed**: `ISaldoService.ObtenerSaldoAsync` now calls
  `IMiembroRepository.GetByIdAsync` before computing `SUM(Monto)`, so an unknown `miembroId` — or
  one belonging to another `NegocioId` — 404s instead of returning a misleading `0`. A real member
  with a genuinely zero balance still returns `200` with `0`.
- **`MiembrosController.BuscarMiembros`** (`GET /api/miembros?q=`, S2) calls
  `IMiembroBusquedaService.BuscarAsync`, which queries `NombreNormalizado` — every word of the
  (normalised) query must appear as a substring, so "gomez ana" only matches a name containing
  both words, not either alone — and rejects anything under 3 normalised characters with
  `400 BUSQUEDA_MUY_CORTA` rather than falling back to a listing (FUNCTIONAL-SPEC §4: "buscar,
  nunca listar"). Results are capped at 25 rows in `MiembroRepository.BuscarAsync`, and each
  carries the same `corteFecha` `ObtenerSaldo` does — a search result row is still a rendered
  balance, and R3 exempts none of them.
- **`MiembrosController.FichaMostrador`** (`GET /api/miembros/{id}/ficha-mostrador`, S3) calls
  `IFichaMostradorService.ObtenerAsync`, which 404s an unknown/another-business `miembroId`
  (`IMiembroRepository.GetByIdAsync`) and builds the alert strip. Phase 1 can only ever produce
  the `Cumpleanos` alert — `Fidelizar.Domain.Reglas.AvisoCumpleanos` implements RN-11 (notice from
  2 days before, day/month only, year ignored) as a pure Domain rule so it is testable with no
  database. `AlergiaODieta` needs `PerfilMiembro` (phase 3, F3-01) and `ComprasHabituales` needs
  phase-4 aggregation — the enum values exist in `Fidelizar.Application.Services.TipoAlertaMiembro`
  for forward compatibility, but nothing produces either kind yet.
- **`MiembrosController.FichaCompleta`** (`GET /api/miembros/{id}/completo`, S6,
  `EncargadaOrAbove`) calls `IFichaCompletaService.ObtenerAsync`, the only place `Telefono`/`Dni`
  ever leave the server. Every single read writes a `RegistroAuditoria` row
  (`Accion = "VerFichaCompleta"`, DATA-MODEL §2) before returning — not only the first read of a
  session, every one.
- **`MiembrosController.HistorialMovimientos`** (`GET /api/miembros/{id}/movimientos`, S7,
  `EncargadaOrAbove`) calls `IHistorialMovimientosService.ObtenerAsync`, a thin wrapper over the
  `IMovimientoRepository.GetPorMiembroAsync` call that already existed — the only thing actually
  missing was the Application-level use case a controller is allowed to talk to (ARCHITECTURE §3).
  It also resolves each movement's `UsuarioNombre` with one bulk fetch of the business's staff
  list, not one lookup per row.
- **`MovimientosController.Anular`** (`POST /api/movimientos/{id}/anular`, S8,
  `EncargadaOrAbove`) calls `IAnulacionMovimientoService.AnularAsync`: looks the original movement
  up by id (`IMovimientoRepository.GetByIdAsync`, new — 404s for an unknown id or one belonging to
  another `NegocioId`, exactly like every other lookup, I8) and appends a new `Ajuste` of
  `-Monto`, dated the day the void happens, with the mandatory reason and the acting user from the
  JWT. `MovimientoCredito.Crear` is the only thing that ever validates `Motivo` — this service does
  not duplicate that check, it just triggers it (I1, I3).
- **`SucursalesController.CierreDiario`** (`GET /api/sucursales/{id}/cierre-diario`, S9,
  `EncargadaOrAbove`) calls `ICierreDiarioService.ObtenerAsync`. A `Canje` carries no `SucursalId`
  of its own (DATA-MODEL §4 — branch is organisational, RN-07), so "this branch's redemptions"
  means the ones registered by a cashier stationed at it (`Usuario.SucursalId`), not the member's
  own branch — a member from another branch is still served normally (RN-07/FUNCTIONAL-SPEC), so
  filtering by the member's branch would silently drop exactly the cross-branch redemptions this
  report exists to show. The controller enforces the branch axis before calling the service: an
  `Encargada` tied to one branch gets `403` for any `sucursalId` that is not her own
  (`ClaimsPrincipalExtensions.PuedeOperarSucursal`); `Dueño` has no branch claim and can ask for
  any.
- **`UsuariosController`** (`/api/usuarios`, S10, `DuenoOnly`) calls `IUsuarioService`.
  `ListarAsync` is a straight passthrough; `CrearAsync` hashes the password
  (`IPasswordHasher.Hash`, never stored in plain text even in memory beyond the call), rejects a
  duplicate email within the business (`409 USUARIO_EMAIL_DUPLICADO`) and validates
  `SucursalId` exists when supplied (`400 SUCURSAL_INEXISTENTE`) before delegating to
  `Usuario.Crear`, which is what actually enforces the role/branch combination (DATA-MODEL §2).
  `Rol` travels as text on the wire (`Shared` cannot reference `Domain`, ARCHITECTURE §3) and is
  parsed in `Api` — `Sistema` is rejected explicitly even though it parses cleanly as a CLR enum
  member, because no account may ever be created under it.
- **`SucursalesController`** (`/api/sucursales`, S10, `DuenoOnly`) calls `ISucursalService`, which
  is new alongside `ISucursalRepository` — nothing built a branch before this task.
- **`MiembrosController.RegistrarCanje`** (`POST /api/miembros/{id}/canjes`) — unchanged from
  F1-04b, not touched by this task (README decision #6, idempotency, is still open).
- **Auth (S1)** — unchanged from F1-03/F1-04b.

## S5 Alta de socio — still pending, deliberately

`AltaMiembroRequest`'s shape in the OpenAPI document is unchanged. It needs a use case that
creates `Miembro` and writes the mandatory `DatosPersonales` `Consentimiento` in the same
transaction (I10) — `IMiembroRepository.AddAsync` alone does not compose that, and
`feat/f1-08-consentimiento` was working on the `Consentimiento` entity and service at the same
time this branch ran. No file under that entity's ownership was touched here.

## DTOs added to `Fidelizar.Shared`

| Namespace | Type | Screen |
| --- | --- | --- |
| `Fidelizar.Shared.Miembros` | `MiembroResumen` | S2 |
| `Fidelizar.Shared.Miembros` | `FichaMostradorResponse` / `AlertaMiembro` | S3 |
| `Fidelizar.Shared.Miembros` | `FichaCompletaResponse` | S6 |
| `Fidelizar.Shared.Movimientos` | `MovimientoResponse` | S7, S8 |
| `Fidelizar.Shared.Movimientos` | `AnularMovimientoRequest` | S8 |
| `Fidelizar.Shared.Sucursales` | `CierreDiarioResponse` / `CierreDiarioMovimiento` | S9 |
| `Fidelizar.Shared.Sucursales` | `SucursalResponse` / `CrearSucursalRequest` | S10 |
| `Fidelizar.Shared.Usuarios` | `UsuarioResponse` / `CrearUsuarioRequest` | S10 |

All of them carry only data and, where relevant, `System.ComponentModel.DataAnnotations`
attributes — no entities, no EF, no business rules (ARCHITECTURE §3). The attributes exist to give
staff a fast Spanish message; the real enforcement is server-side in `Domain`/`Application`
regardless. Every Application-level command/result type that pairs with one of these (e.g.
`Fidelizar.Application.Services.AnularMovimientoRequest`, distinct from the `Shared` one of the
same name) lives in `Fidelizar.Application`, never in `Shared`, because `Fidelizar.Application`
cannot reference `Fidelizar.Shared` (ARCHITECTURE §3) — the same pattern `RegistrarCanjeRequest`
already established in F1-04b. Where a name collides, the controller aliases one of the two
(`using AnularRequest = Fidelizar.Shared.Movimientos.AnularMovimientoRequest;` in
`MovimientosController`), following `CanjeRequest`'s precedent in `MiembrosController`.

## A documentation discrepancy found while writing F1-04b, since resolved

FUNCTIONAL-SPEC §6 (S4) used to say `Motivo` was "optional when the date is today, mandatory when
the date is in the past", while `MovimientoCredito.Crear` and DATA-MODEL §4 both require it for
**every** `Canje`. The owner resolved it on 2026-08-18 in favour of the code: `Motivo` is
mandatory always, and FUNCTIONAL-SPEC §6 was corrected in that branch. `RegistrarCanjeRequest`
in `Shared` keeps `[Required]`, which now matches all three.

## The error shape

Unchanged: `Fidelizar.Shared.Errors.ErrorResponse` (`ErrorCode`, `Message`, `Details[]`), produced
only by `ExceptionHandlingMiddleware`. Every `AppException` subtype maps to a fixed HTTP status
(`ValidationException` → 400, `EntityNotFoundException` → 404, `ConflictException` → 409,
`AuthenticationException` → 401, `AuthorizationException` → 403); an unhandled exception maps to
500 with the generic `UNHANDLED_ERROR` code, never a stack trace.
