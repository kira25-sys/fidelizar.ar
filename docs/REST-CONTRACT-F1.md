# REST contract — Phase 1 (F1-04b)

The seam ARCHITECTURE §3 talks about: once this is agreed, backend and frontend work stop
waiting on each other. The full contract — every route, DTO and status code for S1 through S10
— lives in [api/openapi-fase1.yaml](api/openapi-fase1.yaml). This document is the narrative
version: what is real today, what is only a documented shape, and which Application service each
pending endpoint is waiting on.

Hand-written, not generated: adding `Microsoft.AspNetCore.OpenApi` would be a new dependency, and
CLAUDE.md says to ask before adding one. A static YAML file needs none.

## Endpoint table

| Screen | Route | Verb | Who | Status | Backed by |
| --- | --- | --- | --- | --- | --- |
| S1 Ingreso | `/api/auth/csrf-token` | GET | anyone | **Implemented** | — |
| S1 Ingreso | `/api/auth/login` | POST | anyone | **Implemented** | `IAuthService` |
| S1 Ingreso | `/api/auth/logout` | POST | any session | **Implemented** | — |
| S1 Ingreso | `/api/auth/me` | GET | any session | **Implemented** | — |
| S2 Buscar socio | `/api/miembros?q=` | GET | `CajeroOrAbove` | Pending | search on `IMiembroRepository` (does not exist) |
| S3 Ficha del socio | `/api/miembros/{id}/saldo` | GET | `CajeroOrAbove` | **Implemented (partial)** | `ISaldoService` + `ICorteService` |
| S3 Ficha del socio | `/api/miembros/{id}/ficha-mostrador` | GET | `CajeroOrAbove` | Pending | `Miembro` lookup by id + alert builder |
| S4 Registrar canje | `/api/miembros/{id}/canjes` | POST | `CajeroOrAbove` | **Implemented** | `ISaldoService.RegistrarCanjeAsync` |
| S5 Alta de socio | `/api/miembros` | POST | `CajeroOrAbove` | Pending | member+consent creation use case |
| S6 Ficha completa | `/api/miembros/{id}/completo` | GET | `EncargadaOrAbove` | Pending | full `Miembro` read + `RegistroAuditoria` write |
| S7 Historial de movimientos | `/api/miembros/{id}/movimientos` | GET | `EncargadaOrAbove` | Pending | use-case wrapper over `IMovimientoRepository.GetPorMiembroAsync` |
| S8 Anular movimiento | `/api/movimientos/{id}/anular` | POST | `EncargadaOrAbove` | Pending | `Ajuste`-writing use case, movement lookup by id |
| S9 Cierre diario | `/api/sucursales/{id}/cierre-diario` | GET | `EncargadaOrAbove` | Pending | daily redemption aggregation use case |
| S10 Usuarios | `/api/usuarios` | GET / POST | `DuenoOnly` | Pending | `IUsuarioService` (repository has no list) |
| S10 Sucursales | `/api/sucursales` | GET / POST | `DuenoOnly` | Pending | `ISucursalRepository` / service (neither exists) |

Every state-changing route (`POST`) requires the `X-CSRF-TOKEN` header from
`GET /api/auth/csrf-token` (`AntiforgeryTokenRequiredAttribute`, ARCHITECTURE §8). Every route
requires a valid session; the specific policy column above is what F1-04/F1-15 will verify with
negative tests per role.

## What is implemented, and why exactly this much

Only `ISaldoService`, `ICorteService` and `IAuthService` exist in `Fidelizar.Application` today.
Everything wired up here calls one of those three, unchanged — no new Application code, no new
repository method, because writing one would go beyond what this task asked for.

- **`MiembrosController.ObtenerSaldo`** (`GET /api/miembros/{id}/saldo`) calls
  `ISaldoService.ObtenerSaldoAsync` and `ICorteService.ObtenerCorteVigenteAsync`, combining them
  because ARCHITECTURE §13 R3 forbids showing a balance without its cutoff date next to it.
  **Known gap**: there is no `IMiembroRepository.GetByIdAsync`, so this endpoint cannot tell an
  unknown `miembroId` apart from a real member with a $0 balance — `SUM(Monto)` over zero rows
  is also `0`. It is still worth having: it is real, calls real services, and is exactly the
  balance sub-widget S3 needs once a Miembro lookup exists to wrap it.
- **`MiembrosController.RegistrarCanje`** (`POST /api/miembros/{id}/canjes`) calls
  `ISaldoService.RegistrarCanjeAsync` directly. `NegocioId` and the acting user's id come from
  the JWT claims (`ClaimsPrincipalExtensions`), never from the request body — a Cajero cannot
  redeem against another business by editing the URL.
- **Auth (S1)** was already done in F1-03's `AuthController`. This task's only change there is
  moving `LoginRequest` and the session DTO into `Fidelizar.Shared` (as `Auth.LoginRequest` /
  `Auth.SesionResponse`), because a `Client` built against this contract (F1-04c) can only see
  `Shared` — the old versions lived in `Fidelizar.Api.Auth`, unreachable from `Client`. The
  mapping from `Usuario`/`ClaimsPrincipal` to `SesionResponse` stays in Api
  (`SesionResponseMapper`), since `Shared` cannot reference `Domain` or claims-handling code tied
  to `JwtTokenService`.

## What is pending, endpoint by endpoint

For each of these, the shape is fully specified in the OpenAPI document (`x-status: pending`,
with an `x-pending-service` note) so frontend work can be designed against it — but no
controller exists, because implementing one would mean either inventing the Application service
or writing a fake response. Both are explicitly against this task's instructions.

| Endpoint | Missing piece |
| --- | --- |
| S2 search | No search method on `IMiembroRepository` (only `GetByClienteExternoIdAsync` and `AddAsync` exist), and no Application service to enforce "search, never browse" (no query → no results, never a listing). |
| S3 full ficha | No `Miembro` lookup by id at all. Alerts beyond the balance need `Consentimiento` (exists) + `PerfilMiembro` (phase 3, F3-01) for diet/allergy, and RN-11's birthday math over a `Miembro` this layer cannot fetch yet. |
| S5 alta de socio | Needs a use case that creates `Miembro` and writes the mandatory `DatosPersonales` `Consentimiento` atomically (I10) — `IMiembroRepository.AddAsync` alone does not compose that. |
| S6 ficha completa | Same lookup gap as S3, plus `Telefono`/`Dni` exposure gated to `Encargada`/`Dueño`, plus a `RegistroAuditoria` write (`VerFichaCompleta`) that nothing calls today. |
| S7 historial | The repository call already exists (`IMovimientoRepository.GetPorMiembroAsync`) — what is missing is the Application-level use case. A controller must talk to `Application`, never to a repository directly (ARCHITECTURE §3), so this is one line of real work away, not a redesign, but it is not one of the three services this task was scoped to use. |
| S8 anular movimiento | Needs a lookup-by-id on the ledger (none exists — `IMovimientoRepository` only offers per-member/per-period queries) and a use case that writes the correcting `Ajuste` (I1, I3). |
| S9 cierre diario | Needs an aggregation use case over a branch's `Canje` movements for one day; nothing computes that today. |
| S10 usuarios/sucursales | `IUsuarioRepository` has no list method (only email lookup, create, deactivate — enough for `AuthService`, not for a CRUD screen); there is no `ISucursalRepository` or `ISucursalService` at all. |

None of these were implemented as stubs returning empty lists or fabricated data — an endpoint
that lies about having a backing service is worse than a 404 (task instructions, F1-04b).

## DTOs added to `Fidelizar.Shared`

Only for the four implemented endpoints — adding compiled types for the pending ones risked
drifting from whatever their real Application service ends up shaping, and the OpenAPI document
already gives the frontend a firm shape to code against without a C# type backing it yet.

| Namespace | Type | Screen |
| --- | --- | --- |
| `Fidelizar.Shared.Auth` | `LoginRequest` | S1 |
| `Fidelizar.Shared.Auth` | `SesionResponse` | S1 |
| `Fidelizar.Shared.Miembros` | `SaldoMiembroResponse` | S3 (partial) |
| `Fidelizar.Shared.Movimientos` | `RegistrarCanjeRequest` | S4 |
| `Fidelizar.Shared.Movimientos` | `CanjeResponse` | S4 |

All five carry only data and `System.ComponentModel.DataAnnotations` attributes — no entities, no
EF, no business rules (ARCHITECTURE §3). The attributes exist to give the cashier a fast Spanish
message; the real enforcement is server-side in `Domain`/`Application` regardless.

## A documentation discrepancy found while writing this, since resolved

FUNCTIONAL-SPEC §6 (S4) used to say `Motivo` was "optional when the date is today, mandatory when
the date is in the past", while `MovimientoCredito.Crear` and DATA-MODEL §4 both require it for
**every** `Canje`. The owner resolved it on 2026-08-18 in favour of the code: `Motivo` is
mandatory always, and FUNCTIONAL-SPEC §6 was corrected in this same branch. `RegistrarCanjeRequest`
in `Shared` keeps `[Required]`, which now matches all three.

## The error shape

Unchanged, as instructed: `Fidelizar.Shared.Errors.ErrorResponse` (`ErrorCode`, `Message`,
`Details[]`), produced only by `ExceptionHandlingMiddleware`. Every `AppException` subtype maps
to a fixed HTTP status (`ValidationException` → 400, `EntityNotFoundException` → 404,
`ConflictException` → 409, `AuthenticationException` → 401, `AuthorizationException` → 403); an
unhandled exception maps to 500 with the generic `UNHANDLED_ERROR` code, never a stack trace.
