# Seeder de desarrollo

Siembra un `Negocio`, sucursales, usuarios y socios **enteramente inventados** en una base de
datos de desarrollo, para poder entrar a la aplicación y ver algo en las pantallas.

Existe porque hay un huevo y gallina: `POST /usuarios` está protegido con
`[Authorize(Policy = Policies.DuenoOnly)]`, así que hace falta ser Dueño para crear un Dueño. Y
sin una fila de `Negocio` con razón social, CUIT y domicilio, los textos de consentimiento de S5
tampoco pueden renderizarse — la redacción es una plantilla en código y los datos identificatorios
salen de esa fila (FUNCTIONAL-SPEC §7).

**Está en `tools/` y no en `Fidelizar.Api` a propósito** (decisión del dueño): ni una línea de
código de seeding entra en algo que algún día corre en producción. Nada de `src/` sabe que este
proyecto existe.

---

## Qué siembra

Todo inventado, y deliberadamente inventado de una forma que *se nota* — el CUIT es todo ceros,
los domicilios son "Calle Falsa 123", los emails terminan en `.invalid` (RFC 2606, no resuelve
nunca) y los DNI están en el rango 99.000.000 que jamás se emitió. Ver
[`Datos/DatosInventados.cs`](Datos/DatosInventados.cs).

| Qué | Cuánto |
| --- | --- |
| `Negocio` | 1, con razón social, CUIT y domicilio |
| `Sucursal` | 2 |
| `Usuario` | 3: **Dueño**, Encargada y Cajero |
| `Corte` | 1 |
| `Miembro` | 6, con `NombreNormalizado` para que S2 los encuentre |
| `Consentimiento` | 1 por socio, `DatosPersonales`, otorgado |
| `MovimientoCredito` | 11: 6 `SaldoInicial` (uno por socio), 4 `Canje` y 1 `Ajuste` |

Los saldos resultantes van de $0 a $10.800, así que S2 y S3 tienen casos distintos que mostrar
(incluido un socio en $0, que es el que S4 tiene que negarse a canjear — RN-24).

**No siembra `ConfiguracionPrograma`.** Su `PorcentajeAcumulacion` es RN-01 y su `ObjetivoMensual`
es RN-06: números por negocio que ARCHITECTURE §6 prohíbe escribir como literales en código y que
esta herramienta no tiene por qué inventar. Ninguna pantalla de la fase 1 la necesita (la
acumulación llega con la importación de la fase 2). Cuando haga falta, la declara
`Fidelizar.MigracionOctaviano --porcentaje-acumulacion`, que pide el número real.

## Cómo se corre

Las dos variables son obligatorias, no tienen valor por defecto, **no se imprimen nunca** y **no
van a ningún archivo del repositorio**. Se ponen **solo en la sesión donde se corre el seeder y se
borran al terminar**.

```powershell
# 1. La contraseña de las tres cuentas sembradas. Elegila vos; no hay default.
$env:FIDELIZAR_SEED_PASSWORD = '...'

# 2. La cadena de conexión a la base de DESARROLLO.
$env:ConnectionStrings__DefaultConnection = '...'

# 3. Correr, diciendo explícitamente contra qué base creés estar corriendo.
dotnet run --project tools/Fidelizar.SeederDesarrollo -- --base-esperada fidelizar_dev

# 4. Borrar las variables de esta sesión.
Remove-Item Env:FIDELIZAR_SEED_PASSWORD
Remove-Item Env:ConnectionStrings__DefaultConnection
```

Las tres cuentas quedan con esa misma contraseña. Entrás con la de Dueño
(`dueno@ejemplo.invalid`) por `POST /auth/login` y desde ahí creás los usuarios reales.

> La variable `ConnectionStrings__DefaultConnection` pisa cualquier otra fuente de configuración
> de la API en esa misma consola. Si después vas a levantar la API desde la misma sesión, borrala:
> ya pasó una vez que la API terminó conectada a la base equivocada por una variable que había
> quedado puesta.

### Parámetros

| Parámetro | |
| --- | --- |
| `--base-esperada <nombre>` | **Obligatorio.** El nombre de la base contra la que creés estar corriendo. Tiene que coincidir con el que dice la cadena de conexión. |
| `--corte <yyyy-MM-dd>` | Opcional. Fecha de corte a declarar. Por defecto, el día 1 de seis meses atrás. |
| `--permitir-base-no-vacia` | Opcional. Sigue aunque la base ya tenga datos que no sembró esta herramienta. Nunca borra ni modifica nada. |
| `--ayuda` | Esto, en la consola. |

## Correrlo dos veces: es idempotente, y nunca destructivo

Esa es la elección, y es explícita. **No hay ningún borrado ni ninguna modificación en toda la
herramienta.** Cada cosa se busca primero por su clave natural — el `Negocio` por ser la única
fila, la `Sucursal` por `CodigoExterno`, el `Usuario` por email, el `Miembro` por
`ClienteExternoId`, el `Corte` por negocio, el historial de un socio por "¿tiene algún movimiento?"
— y se crea solo si falta.

Correrla dos veces completa lo que falte y no duplica nada. Correrla contra una base que pobló
otra cosa agrega sin tocar una sola fila existente. El ledger sigue siendo append-only acá igual
que en todos lados (I1): esta herramienta no tiene más poder sobre él que un cajero.

**El único hueco, dicho y no escondido:** el historial de un socio se siembra solo si ese socio
tiene *cero* movimientos — la misma compuerta que usan `MigradorOctaviano` y `VipPadronImporter`.
Una corrida interrumpida a la mitad de los movimientos de un socio no lo retoma en la siguiente.
En una base de desarrollo la respuesta es borrarla y sembrar de nuevo.

## Por qué se niega a correr en tantos casos

En esta máquina hay un Postgres de desarrollo y, en otro puerto, **otro con los saldos reales de
293 socios**. Hace unas horas la API se conectó a la base equivocada por una variable de entorno
mal puesta. El ledger es append-only: lo que se escribe ahí no se deshace. Así que las guardas
están escritas asumiendo que ese error se va a volver a cometer.

En orden, y todas antes de abrir una sola conexión salvo la última:

1. **Sin `FIDELIZAR_SEED_PASSWORD`, falla y no crea nada.** Es lo primero que se valida: una
   corrida condenada a fallar por la contraseña tiene que fallar antes de haber mirado una base.
   Nunca hay contraseña por defecto, ni escrita en un archivo, ni inventada "para que corra sola".
2. **Sin `ConnectionStrings__DefaultConnection`, falla.** No hay cadena de conexión por defecto:
   CLAUDE.md prohíbe que una real quede en el repositorio, incluso como placeholder.
3. **`--base-esperada` es obligatorio y se compara contra la base que la cadena de conexión nombra
   de verdad.** Vos decís contra qué base creés estar corriendo; si no coincide, no se escribe
   nada. Esta es la guarda contra la variable de entorno que quedó de otra sesión.
4. **Cualquier nombre de base que contenga `gate`, `prod` u `octaviano` se rechaza, sin bandera que
   lo habilite.** `fidelizar_gate` es la base con los saldos reales.
5. **Si la base no está vacía y no es una que esta herramienta haya sembrado, se planta** e imprime
   los recuentos que encontró, para que se vea de una que no es una base de desarrollo vacía. Sigue
   solo con `--permitir-base-no-vacia`. Reconoce su propia base por el CUIT de todo ceros, así que
   la segunda corrida normal no necesita ninguna bandera.
6. **Las migraciones se aplican después de todo eso**, nunca antes: aplicar una migración ya es una
   escritura.

Ni la contraseña ni la cadena de conexión se imprimen nunca. `OpcionesSeeder` es una clase y no un
`record` justamente por eso: el `ToString()` que el compilador genera para un `record` imprimiría
todos sus miembros, y un solo `Console.WriteLine` de más filtraría las dos. Hay un test que lo
verifica.

## Tests

`tests/Fidelizar.SeederDesarrollo.Tests` — 44 tests contra fakes en memoria: sin Postgres, sin
cadena de conexión, sin ninguna variable de entorno real. Cubren cada negativa de arriba, la
idempotencia de la segunda corrida, y que los datos inventados sigan pareciendo inventados.
