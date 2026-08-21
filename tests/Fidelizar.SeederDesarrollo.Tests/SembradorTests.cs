using Fidelizar.Domain.Consentimientos;
using Fidelizar.Domain.Entities;
using Fidelizar.Domain.Texto;
using Fidelizar.SeederDesarrollo.Datos;
using Fidelizar.SeederDesarrollo.Sembrado;
using Fidelizar.SeederDesarrollo.Tests.Fakes;

namespace Fidelizar.SeederDesarrollo.Tests;

/// <summary>
/// The seeder's own behaviour, against fakes — no Postgres, no connection string, no environment
/// variable. Everything asserted here is invented data by construction: the fixture under test
/// <i>is</i> <see cref="DatosInventados"/>, which never touched a real database (CLAUDE.md).
/// </summary>
public class SembradorTests
{
    private static readonly DateTime AhoraUtc = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 8, 20);
    private static readonly DateOnly Corte = new(2026, 2, 1);

    private const string PasswordInventada = "contrasena-inventada-de-prueba";

    private sealed record Contexto(
        Sembrador Sembrador,
        FakeNegocioSeederRepository Negocios,
        FakeSucursalRepository Sucursales,
        FakeUsuarioRepository Usuarios,
        FakeCorteRepository Cortes,
        FakeMiembroRepository Miembros,
        FakeMovimientoRepository Movimientos,
        FakeConsentimientoRepository Consentimientos,
        FakePasswordHasher Hasher);

    private static Contexto CrearContexto()
    {
        var negocios = new FakeNegocioSeederRepository();
        var sucursales = new FakeSucursalRepository();
        var usuarios = new FakeUsuarioRepository();
        var cortes = new FakeCorteRepository();
        var miembros = new FakeMiembroRepository();
        var movimientos = new FakeMovimientoRepository();
        var consentimientos = new FakeConsentimientoRepository();
        var hasher = new FakePasswordHasher();

        var sembrador = new Sembrador(
            negocios, sucursales, usuarios, cortes, miembros, movimientos, consentimientos, hasher);

        return new Contexto(
            sembrador, negocios, sucursales, usuarios, cortes, miembros, movimientos, consentimientos, hasher);
    }

    private static Task<ResultadoSembrado> EjecutarAsync(Contexto contexto) =>
        contexto.Sembrador.EjecutarAsync(PasswordInventada, Corte, Hoy, AhoraUtc);

    [Fact]
    public async Task Siembra_el_negocio_con_razon_social_CUIT_y_domicilio()
    {
        // Without these three, S5 cannot render the DatosPersonales consent text at all: the
        // wording is a template and the identifying data comes from this row (FUNCTIONAL-SPEC §7).
        var contexto = CrearContexto();

        var resultado = await EjecutarAsync(contexto);

        Assert.True(resultado.NegocioCreado);
        var negocio = await contexto.Negocios.ObtenerPrimeroAsync();
        Assert.NotNull(negocio);
        Assert.Equal(DatosInventados.NegocioNombre, negocio.Nombre);
        Assert.Equal(DatosInventados.NegocioCuit, negocio.Cuit);
        Assert.Equal(DatosInventados.NegocioDomicilio, negocio.Domicilio);

        var (_, texto) = TextosConsentimiento.DatosPersonalesPara(negocio);
        Assert.DoesNotContain("(a completar)", texto);
    }

    [Fact]
    public async Task Siembra_al_menos_una_sucursal()
    {
        var contexto = CrearContexto();

        var resultado = await EjecutarAsync(contexto);

        Assert.True(resultado.SucursalesCreadas >= 1);
        Assert.Equal(DatosInventados.Sucursales.Count, contexto.Sucursales.Sucursales.Count);
        Assert.All(contexto.Sucursales.Sucursales, s => Assert.NotNull(s.CodigoExterno));
    }

    [Fact]
    public async Task Siembra_un_usuario_Dueno_sin_sucursal_y_con_la_password_hasheada()
    {
        // The whole reason this tool exists: POST /usuarios is Dueño-only, so without this row
        // there is no way to create the first account.
        var contexto = CrearContexto();

        await EjecutarAsync(contexto);

        var dueno = Assert.Single(contexto.Usuarios.Usuarios, u => u.Rol == RolUsuario.Dueno);
        Assert.Equal(DatosInventados.EmailDueno, dueno.Email);
        Assert.Null(dueno.SucursalId);
        Assert.True(dueno.Activo);

        // The plain password is never stored — it went through IPasswordHasher, the same
        // abstraction AuthService verifies against, and nothing here hashes anything by hand.
        Assert.DoesNotContain(PasswordInventada, dueno.PasswordHash, StringComparison.Ordinal);
        Assert.All(contexto.Hasher.PasswordsHasheadas, p => Assert.Equal(PasswordInventada, p));
        Assert.Equal(DatosInventados.Usuarios.Count, contexto.Hasher.PasswordsHasheadas.Count);
    }

    [Fact]
    public async Task El_cajero_y_la_encargada_quedan_asignados_a_una_sucursal()
    {
        // DATA-MODEL §2: SucursalId is mandatory for these two roles and forbidden for Dueño.
        // Usuario.Crear enforces it; this asserts the fixture actually satisfies it.
        var contexto = CrearContexto();

        await EjecutarAsync(contexto);

        foreach (var usuario in contexto.Usuarios.Usuarios.Where(
            u => u.Rol is RolUsuario.Cajero or RolUsuario.Encargada))
        {
            Assert.NotNull(usuario.SucursalId);
            Assert.NotNull(await contexto.Sucursales.GetByIdAsync(usuario.NegocioId, usuario.SucursalId!.Value));
        }
    }

    [Fact]
    public async Task Declara_el_corte_porque_sin_el_la_ficha_de_mostrador_no_puede_renderizarse()
    {
        // CorteService throws CORTE_NO_DECLARADO when there is none, and S3 reads it to show the
        // as-of date next to the balance (R3). A seeded database without a cutoff has screens
        // that fail on open.
        var contexto = CrearContexto();

        var resultado = await EjecutarAsync(contexto);

        Assert.True(resultado.CorteDeclarado);
        var corte = await contexto.Cortes.ObtenerAsync(resultado.NegocioId);
        Assert.NotNull(corte);
        Assert.Equal(Corte, corte.Fecha);
    }

    [Fact]
    public async Task Siembra_socios_con_saldo_para_que_S2_y_S3_no_salgan_vacias()
    {
        var contexto = CrearContexto();

        var resultado = await EjecutarAsync(contexto);

        Assert.Equal(DatosInventados.Miembros(Corte, Hoy).Count, resultado.MiembrosCreados);
        Assert.Contains(resultado.Saldos, s => s.Saldo > 0);

        // S2 searches on NombreNormalizado; a seeded member the search cannot find is useless.
        var miembro = contexto.Miembros.Miembros[0];
        Assert.Equal(VipNombres.Normalizar(miembro.Nombre), miembro.NombreNormalizado);
        var encontrados = await contexto.Miembros.BuscarAsync(
            miembro.NegocioId, [VipNombres.Normalizar(miembro.Nombre).Split(' ')[0]]);
        Assert.NotEmpty(encontrados);
    }

    [Fact]
    public async Task El_saldo_reportado_es_SUM_de_los_movimientos_y_ninguno_queda_negativo()
    {
        // I2 and I6: the printed balance is read back from the ledger, and no seeded member is
        // left owing the business money.
        var contexto = CrearContexto();

        var resultado = await EjecutarAsync(contexto);

        foreach (var saldo in resultado.Saldos)
        {
            var miembro = contexto.Miembros.Miembros.Single(m => m.ClienteExternoId == saldo.ClienteExternoId);
            var suma = contexto.Movimientos.Movimientos
                .Where(m => m.MiembroId == miembro.Id)
                .Sum(m => m.Monto);

            Assert.Equal(suma, saldo.Saldo);
            Assert.True(saldo.Saldo >= 0m, $"{saldo.ClienteExternoId} quedó con saldo negativo.");
        }
    }

    [Fact]
    public async Task Cada_socio_queda_con_su_consentimiento_de_DatosPersonales_otorgado()
    {
        // FUNCTIONAL-SPEC §7: an alta is impossible without it, so a member seeded without one
        // would be a member the product's own alta path could never have produced.
        var contexto = CrearContexto();

        await EjecutarAsync(contexto);

        foreach (var miembro in contexto.Miembros.Miembros)
        {
            var vigente = await contexto.Consentimientos.GetVigenteAsync(
                miembro.NegocioId, miembro.Id, TipoConsentimiento.DatosPersonales);

            Assert.NotNull(vigente);
            Assert.True(vigente.Otorgado);
            Assert.Equal(TextosConsentimiento.DatosPersonalesVersion, vigente.VersionTexto);
        }
    }

    [Fact]
    public async Task No_siembra_consentimiento_de_DatosSensibles()
    {
        // I10: sensitive data is optional and gates health fields. Granting it on behalf of six
        // invented members would model a consent nobody gave.
        var contexto = CrearContexto();

        await EjecutarAsync(contexto);

        Assert.DoesNotContain(
            contexto.Consentimientos.Consentimientos, c => c.Tipo == TipoConsentimiento.DatosSensibles);
    }

    [Fact]
    public async Task Correrlo_dos_veces_no_duplica_nada()
    {
        // The documented contract: idempotent, never destructive. The second run reports zeros on
        // the "created" side and leaves every count exactly where the first run left it.
        var contexto = CrearContexto();

        await EjecutarAsync(contexto);

        var negociosDespuesDelPrimero = contexto.Negocios.Creaciones;
        var sucursales = contexto.Sucursales.Sucursales.Count;
        var usuarios = contexto.Usuarios.Usuarios.Count;
        var miembros = contexto.Miembros.Miembros.Count;
        var movimientos = contexto.Movimientos.Movimientos.Count;
        var consentimientos = contexto.Consentimientos.Consentimientos.Count;

        var segundo = await EjecutarAsync(contexto);

        Assert.False(segundo.NegocioCreado);
        Assert.False(segundo.CorteDeclarado);
        Assert.Equal(0, segundo.SucursalesCreadas);
        Assert.Equal(0, segundo.UsuariosCreados);
        Assert.Equal(0, segundo.MiembrosCreados);
        Assert.Equal(0, segundo.ConsentimientosCreados);
        Assert.Equal(0, segundo.MovimientosCreados);
        Assert.Equal(miembros, segundo.MiembrosConHistorialPrevio);

        Assert.Equal(negociosDespuesDelPrimero, contexto.Negocios.Creaciones);
        Assert.Equal(sucursales, contexto.Sucursales.Sucursales.Count);
        Assert.Equal(usuarios, contexto.Usuarios.Usuarios.Count);
        Assert.Equal(miembros, contexto.Miembros.Miembros.Count);
        Assert.Equal(movimientos, contexto.Movimientos.Movimientos.Count);
        Assert.Equal(consentimientos, contexto.Consentimientos.Consentimientos.Count);
    }

    [Fact]
    public async Task Los_saldos_de_la_segunda_corrida_son_los_mismos_que_los_de_la_primera()
    {
        var contexto = CrearContexto();

        var primero = await EjecutarAsync(contexto);
        var segundo = await EjecutarAsync(contexto);

        Assert.Equal(primero.Saldos, segundo.Saldos);
    }

    [Fact]
    public async Task Todo_movimiento_sembrado_lleva_motivo_y_queda_estampado_con_el_Dueno()
    {
        // DATA-MODEL §4: a retroactive movement requires a Motivo whatever its type, and
        // ARCHITECTURE §8 wants a real person behind every movement — never a placeholder id.
        var contexto = CrearContexto();

        await EjecutarAsync(contexto);

        var dueno = contexto.Usuarios.Usuarios.Single(u => u.Rol == RolUsuario.Dueno);

        Assert.NotEmpty(contexto.Movimientos.Movimientos);
        Assert.All(contexto.Movimientos.Movimientos, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Motivo));
            Assert.Equal(dueno.Id, m.UsuarioId);
            Assert.True(m.FechaEfectiva <= Hoy);
            Assert.True(m.FechaEfectiva >= Corte);
        });
    }

    [Fact]
    public async Task No_siembra_ninguna_ConfiguracionPrograma()
    {
        // ARCHITECTURE §6: PorcentajeAcumulacion is RN-01 and ObjetivoMensual is RN-06 —
        // per-business numbers this tool refuses to invent. The Sembrador has no repository for
        // that table at all, which is what makes the refusal structural instead of a promise.
        var tipos = typeof(Sembrador)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name);

        Assert.DoesNotContain(tipos, t => t.Contains("Configuracion", StringComparison.Ordinal));
    }

    [Fact]
    public void Ningun_dato_inventado_se_parece_a_un_dato_real()
    {
        // CLAUDE.md: invented, and visibly invented. A CUIT of all zeros cannot be a real one,
        // .invalid can never resolve (RFC 2606), and DNIs in the 99,000,000 range were never
        // issued. If someone ever "improves" the fixture with plausible-looking data, this fails.
        Assert.Equal("30-00000000-0", DatosInventados.NegocioCuit);
        Assert.Contains("Calle Falsa", DatosInventados.NegocioDomicilio);
        Assert.All(DatosInventados.Usuarios, u => Assert.EndsWith("@ejemplo.invalid", u.Email));
        Assert.All(DatosInventados.Miembros(Corte, Hoy), m =>
        {
            Assert.StartsWith("99", m.Dni);
            Assert.StartsWith("DEMO-", m.ClienteExternoId);
            Assert.Contains("0000-", m.Telefono);
        });
    }
}
