using System.Xml.Linq;
using Fidelizar.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace Fidelizar.Infrastructure.Tests.Persistence;

/// <summary>
/// The design-time factory must resolve its connection string from the same sources the running
/// API does. On 2026-08-20 it did not: it read one environment variable and nothing else, so a
/// persistent user-level variable left over from phase 0 beat the user secrets the API was
/// configured with, and "dotnet ef database update" migrated the phase 0 gate database (the 293
/// real members) instead of the development one. These tests pin the wiring that prevents it.
///
/// None of them opens a connection or reads a secret's value, so they run in CI, which has no
/// Postgres and no user secrets store.
/// </summary>
public class FidelizarDbContextFactoryTests
{
    private const string ConnectionStringKey = "ConnectionStrings__DefaultConnection";

    [Fact]
    public void La_factory_apunta_al_mismo_UserSecretsId_que_Fidelizar_Api()
    {
        // The whole fix rests on this GUID matching. If someone regenerates the API's
        // UserSecretsId, the factory would silently read a store nobody writes to and fall back
        // to the placeholder — a confusing failure instead of a loud one.
        var apiProject = XDocument.Load(
            FindSolutionRoot("src", "Fidelizar.Api", "Fidelizar.Api.csproj"));

        var userSecretsId = apiProject
            .Descendants("UserSecretsId")
            .Select(element => element.Value.Trim())
            .SingleOrDefault();

        Assert.Equal(FidelizarDbContextFactory.ApiUserSecretsId, userSecretsId);
    }

    [Fact]
    public void La_configuracion_de_design_time_lee_user_secrets_y_despues_variables_de_entorno()
    {
        var configuration = (IConfigurationRoot)FidelizarDbContextFactory.BuildConfiguration();

        var providers = configuration.Providers.ToArray();

        // Exactly two sources, in this order. The JSON one is the user secrets store (the only
        // JSON file this configuration adds); environment variables come last so they win, which
        // is the same precedence Fidelizar.Api's host applies.
        Assert.Collection(
            providers,
            provider => Assert.IsType<JsonConfigurationProvider>(provider),
            provider => Assert.IsType<EnvironmentVariablesConfigurationProvider>(provider));

        var secretsSource = (JsonConfigurationSource)((JsonConfigurationProvider)providers[0]).Source;
        Assert.Equal("secrets.json", secretsSource.Path);
        Assert.True(secretsSource.Optional, "El store de user secrets no existe en CI ni en una máquina nueva.");
    }

    [Fact]
    public void Una_variable_de_entorno_le_gana_a_user_secrets()
    {
        // Invented value, points nowhere: the test asserts precedence, never a real credential.
        const string valorInventado =
            "Host=localhost;Database=INVENTADA_PARA_EL_TEST;Username=INVENTADA;Password=INVENTADA";

        var anterior = Environment.GetEnvironmentVariable(ConnectionStringKey);
        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringKey, valorInventado);

            var resuelta = FidelizarDbContextFactory.ResolveConnectionString(
                FidelizarDbContextFactory.BuildConfiguration());

            Assert.Equal(valorInventado, resuelta);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringKey, anterior);
        }
    }

    [Fact]
    public void Sin_nada_configurado_cae_al_placeholder_para_no_romper_migrations_add()
    {
        // "dotnet ef migrations add" never opens the connection. Throwing here would make
        // authoring a migration impossible without a database at hand.
        var vacia = new ConfigurationBuilder().Build();

        Assert.Equal(
            FidelizarDbContextFactory.PlaceholderConnectionString,
            FidelizarDbContextFactory.ResolveConnectionString(vacia));
    }

    [Fact]
    public void El_placeholder_no_esconde_una_cadena_real()
    {
        // CLAUDE.md: no connection string in the repository, not even one that looks plausible.
        Assert.All(
            new[] { "Database", "Username", "Password" },
            parte => Assert.Contains($"{parte}=CAMBIAR_ESTO", FidelizarDbContextFactory.PlaceholderConnectionString));
    }

    [Fact]
    public void Una_cadena_configurada_se_usa_tal_cual()
    {
        const string valorInventado = "Host=localhost;Port=5434;Database=INVENTADA_PARA_EL_TEST";

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = valorInventado,
            })
            .Build();

        Assert.Equal(valorInventado, FidelizarDbContextFactory.ResolveConnectionString(configuracion));
    }

    private static string FindSolutionRoot(params string[] relativePathFromRoot)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fidelizar.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Could not locate Fidelizar.sln by walking up from the test output directory.");
        }

        return Path.Combine([directory.FullName, .. relativePathFromRoot]);
    }
}
