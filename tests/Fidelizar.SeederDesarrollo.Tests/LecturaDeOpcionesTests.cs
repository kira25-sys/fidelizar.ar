using Fidelizar.SeederDesarrollo.Configuracion;

namespace Fidelizar.SeederDesarrollo.Tests;

/// <summary>
/// The refusals that have to happen <b>before</b> the tool opens a connection. Every environment
/// variable below is a fake supplied through <see cref="LecturaDeOpciones.Leer"/>'s own lookup
/// delegate — this file never sets, reads or prints a real one (CLAUDE.md).
/// </summary>
public class LecturaDeOpcionesTests
{
    private const string CadenaInventada =
        "Host=localhost;Port=5434;Database=fidelizar_dev;Username=usuario_inventado;Password=inventada";

    private static Func<string, string?> Entorno(string? password, string? cadena) =>
        nombre => nombre switch
        {
            LecturaDeOpciones.VariablePassword => password,
            LecturaDeOpciones.VariableCadenaConexion => cadena,
            _ => null,
        };

    [Fact]
    public void Sin_FIDELIZAR_SEED_PASSWORD_no_devuelve_opciones_y_explica_que_falta()
    {
        var (opciones, error) = LecturaDeOpciones.Leer(
            ["--base-esperada", "fidelizar_dev"], Entorno(password: null, CadenaInventada));

        Assert.Null(opciones);
        Assert.NotNull(error);
        Assert.Contains(LecturaDeOpciones.VariablePassword, error);
        Assert.Contains("No se crea nada.", error);
    }

    [Fact]
    public void Una_FIDELIZAR_SEED_PASSWORD_en_blanco_cuenta_como_ausente()
    {
        var (opciones, error) = LecturaDeOpciones.Leer(
            ["--base-esperada", "fidelizar_dev"], Entorno("   ", CadenaInventada));

        Assert.Null(opciones);
        Assert.Contains(LecturaDeOpciones.VariablePassword, error);
    }

    [Fact]
    public void La_password_se_valida_antes_que_la_cadena_de_conexion()
    {
        // Both are missing; the message has to name the password, because a run doomed to fail on
        // it should fail before it has even looked at a database.
        var (_, error) = LecturaDeOpciones.Leer([], Entorno(password: null, cadena: null));

        Assert.Contains(LecturaDeOpciones.VariablePassword, error);
        Assert.DoesNotContain(LecturaDeOpciones.VariableCadenaConexion, error);
    }

    [Fact]
    public void Sin_cadena_de_conexion_no_devuelve_opciones()
    {
        var (opciones, error) = LecturaDeOpciones.Leer(
            ["--base-esperada", "fidelizar_dev"], Entorno("inventada", cadena: null));

        Assert.Null(opciones);
        Assert.Contains(LecturaDeOpciones.VariableCadenaConexion, error);
    }

    [Fact]
    public void Sin_base_esperada_no_devuelve_opciones()
    {
        var (opciones, error) = LecturaDeOpciones.Leer([], Entorno("inventada", CadenaInventada));

        Assert.Null(opciones);
        Assert.Contains("--base-esperada", error);
    }

    [Fact]
    public void Si_la_base_esperada_no_coincide_con_la_de_la_cadena_de_conexion_se_planta()
    {
        // The exact mistake this guard exists for: the variable left over from another session
        // points somewhere else than the operator believes.
        var (opciones, error) = LecturaDeOpciones.Leer(
            ["--base-esperada", "fidelizar_dev"],
            Entorno("inventada", "Host=localhost;Port=5433;Database=otra_base;Username=u;Password=p"));

        Assert.Null(opciones);
        Assert.Contains("fidelizar_dev", error);
        Assert.Contains("otra_base", error);
        Assert.Contains("No se escribe nada.", error);
    }

    [Theory]
    [InlineData("fidelizar_gate")]
    [InlineData("FIDELIZAR_GATE")]
    [InlineData("gate_backup")]
    [InlineData("fidelizar_prod")]
    [InlineData("octaviano")]
    public void Una_base_con_nombre_prohibido_se_rechaza_aunque_coincida_con_base_esperada(string nombre)
    {
        var (opciones, error) = LecturaDeOpciones.Leer(
            ["--base-esperada", nombre, "--permitir-base-no-vacia"],
            Entorno("inventada", $"Host=localhost;Port=5433;Database={nombre};Username=u;Password=p"));

        Assert.Null(opciones);
        Assert.Contains("se niega a escribir", error);
    }

    [Fact]
    public void Con_todo_en_orden_devuelve_las_opciones_sin_error()
    {
        var (opciones, error) = LecturaDeOpciones.Leer(
            ["--base-esperada", "fidelizar_dev"], Entorno("inventada", CadenaInventada));

        Assert.Null(error);
        Assert.NotNull(opciones);
        Assert.Equal("localhost", opciones.Host);
        Assert.Equal("5434", opciones.Puerto);
        Assert.Equal("fidelizar_dev", opciones.BaseDeDatos);
        Assert.Equal("usuario_inventado", opciones.UsuarioPostgres);
        Assert.Equal("inventada", opciones.PasswordSembrada);
        Assert.Null(opciones.Corte);
        Assert.False(opciones.PermitirBaseNoVacia);
    }

    [Fact]
    public void ToString_de_las_opciones_no_filtra_ni_la_password_ni_la_cadena_de_conexion()
    {
        // OpcionesSeeder is a class and not a record precisely so a stray Console.WriteLine
        // cannot print a secret (CLAUDE.md: a secret never reaches a log).
        var (opciones, _) = LecturaDeOpciones.Leer(
            ["--base-esperada", "fidelizar_dev"], Entorno("inventada", CadenaInventada));

        var texto = opciones!.ToString()!;

        Assert.DoesNotContain("inventada", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Lee_corte_y_permitir_base_no_vacia_cuando_se_pasan()
    {
        var (opciones, error) = LecturaDeOpciones.Leer(
            ["--base-esperada", "fidelizar_dev", "--corte", "2026-02-01", "--permitir-base-no-vacia"],
            Entorno("inventada", CadenaInventada));

        Assert.Null(error);
        Assert.Equal(new DateOnly(2026, 2, 1), opciones!.Corte);
        Assert.True(opciones.PermitirBaseNoVacia);
    }

    [Fact]
    public void Un_corte_con_formato_invalido_se_rechaza()
    {
        var (opciones, error) = LecturaDeOpciones.Leer(
            ["--base-esperada", "fidelizar_dev", "--corte", "01/02/2026"],
            Entorno("inventada", CadenaInventada));

        Assert.Null(opciones);
        Assert.Contains("--corte", error);
    }
}
