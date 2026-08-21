# Local development setup (Windows)

How to go from a fresh clone to a running API with a working database. Everything here is
development only.

**The one rule that governs this whole document:** the two values you need — the connection
string and the JWT signing key — go in **user secrets**, never in a file inside the repository.
`appsettings.json` ships `CAMBIAR_ESTO` for both on purpose, and it stays that way (CLAUDE.md).

---

## 1. What you need installed

| Tool | Why |
| --- | --- |
| .NET 10 SDK | Builds and runs everything |
| Docker Desktop | Runs the development Postgres |
| `dotnet-ef` | Only for authoring migrations by hand — see §6 |

`dotnet ef` may report *"The Entity Framework tools version '10.0.9' is older than that of the
runtime '10.0.11'"*. That warning is expected and harmless; `dotnet tool update --global
dotnet-ef` silences it.

## 2. Start the development database

`compose.dev.yml` at the repository root defines it: container `fidelizar-dev-db`, database
`fidelizar_dev`, user `postgres`, published on **port 5434**.

The password is read from `FIDELIZAR_DEV_PG_PASSWORD` and has deliberately no default, so
compose fails with a clear message rather than starting with something guessable.

```powershell
$env:FIDELIZAR_DEV_PG_PASSWORD = "CAMBIAR_ESTO"   # this session only — see §5
docker compose -f compose.dev.yml up -d
```

To throw the database away and start from an empty one:

```powershell
docker compose -f compose.dev.yml down -v
```

### Port 5434 is not port 5433

`compose.gate.yml` is a **different** database on **port 5433**: the phase 0 gate, holding the
293 real members and their balances. It is never used for development, never pointed at by a
connection string you are debugging with, and never migrated by hand. Different container,
different volume, different port, on purpose.

If a development connection string ever says `5433`, stop and fix it before running anything.

## 3. The two configuration values

Both are set against **`Fidelizar.Api`**, which owns the user secrets store
(`<UserSecretsId>` in `Fidelizar.Api.csproj`). `FidelizarDbContextFactory` reads that same store
by id, so the EF tooling and the running API can never disagree about which database they mean.

### `ConnectionStrings:DefaultConnection`

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Host=localhost;Port=5434;Database=fidelizar_dev;Username=postgres;Password=CAMBIAR_ESTO" `
  --project src\Fidelizar.Api
```

Replace `CAMBIAR_ESTO` with the same value you gave `FIDELIZAR_DEV_PG_PASSWORD` in §2.

### `Jwt:SigningKey`

HS256 needs at least 32 bytes (RFC 7518 §3.2), and the API refuses to start with a shorter one
rather than failing at the first login. Generate it and pipe it straight into user secrets, so
it is never printed to the terminal, pasted into a file, or left in scrollback:

```powershell
$bytes = [byte[]]::new(48)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($bytes)) --project src\Fidelizar.Api
Remove-Variable bytes
```

### Check what is set

```powershell
dotnet user-secrets list --project src\Fidelizar.Api
```

This prints the values. Do not paste its output anywhere.

## 4. Run it

```powershell
dotnet run --project src\Fidelizar.Api
```

The API applies every pending EF Core migration on start (ARCHITECTURE §14), and a failed
migration aborts the start. **That is normally the only way migrations get applied in
development** — you should rarely need `dotnet ef database update` at all.

## 5. The precedence trap — read this one twice

.NET configuration is layered, and **the last layer wins**:

```
appsettings.json  →  appsettings.Development.json  →  user secrets  →  environment variables
```

An environment variable named `ConnectionStrings__DefaultConnection` (double underscore) **beats
whatever is in user secrets**, silently. So does `Jwt__SigningKey`.

This is not hypothetical. On 2026-08-20 a `ConnectionStrings__DefaultConnection` variable set at
**user level** during phase 0 — back when it was the only way to make `dotnet ef` work — sent the
API to the gate database on 5433 instead of the development one, and two migrations were applied
there before anyone noticed. They were additive and nothing was lost. The trap is now disarmed at
the source: `FidelizarDbContextFactory` reads user secrets, so no persistent variable is needed
for tooling any more.

Two things to keep in mind:

- **Never set these variables at user or machine level.** If one is already there, remove it:

  ```powershell
  [Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $null, "User")
  ```

  Check what is currently set at each level, without printing the values:

  ```powershell
  "Process","User","Machine" | ForEach-Object {
      $level = $_
      [Environment]::GetEnvironmentVariables($level).Keys |
          Where-Object { $_ -like "ConnectionStrings__*" -or $_ -like "Jwt__*" } |
          ForEach-Object { "$level : $_" }
  }
  ```

- **A new terminal inside VS Code inherits the editor's environment**, which VS Code captured
  when it started. Removing a user-level variable does *not* clear it from terminals opened in an
  editor that was already running — you have to restart VS Code. A variable that "should be gone"
  but is still in a VS Code terminal is this, not a bug.

## 6. Authoring a migration

```powershell
dotnet ef migrations add NombreDeLaMigracion `
  --project src\Fidelizar.Infrastructure --startup-project src\Fidelizar.Infrastructure
```

`Fidelizar.Api` cannot be the startup project — it does not carry
`Microsoft.EntityFrameworkCore.Design` — so `Fidelizar.Infrastructure` is both, and
`FidelizarDbContextFactory` supplies the connection string.

`migrations add` never opens a connection, so it works with no database running: the factory
falls back to a `CAMBIAR_ESTO` placeholder in that case, by design.

To list migrations without touching the database:

```powershell
dotnet ef migrations list --no-connect `
  --project src\Fidelizar.Infrastructure --startup-project src\Fidelizar.Infrastructure
```

Every migration must apply **and** roll back before it is committed.

## 7. Build and test

```powershell
dotnet build
dotnet test
```

Neither needs a database: the test suite runs with no Postgres, which is why CI can run it.

## 8. The one tool that still needs an environment variable

`tools/Fidelizar.MigracionOctaviano` reads `ConnectionStrings__DefaultConnection` from the
environment and has no other source. It was not changed along with the factory.

So on the rare occasion you run it, set the variable **in that PowerShell session only** and let
it die with the window — never with `[Environment]::SetEnvironmentVariable(..., "User")`, which
is precisely how the incident in §5 happened:

```powershell
$env:ConnectionStrings__DefaultConnection = "..."   # this session only, never persisted
```
