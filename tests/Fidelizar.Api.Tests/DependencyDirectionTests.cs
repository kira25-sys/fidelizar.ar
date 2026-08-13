using System.Reflection;
using System.Xml.Linq;

namespace Fidelizar.Api.Tests;

/// <summary>
/// Enforces the dependency direction described in ARCHITECTURE.md §3, by reflection over the
/// compiled assemblies rather than by reading project files. This test must never be deleted
/// (ARCHITECTURE §11): it is the only thing standing between "Client references only Shared"
/// and a domain rule leaking into a browser tab on a counter tablet.
/// </summary>
public class DependencyDirectionTests
{
    private const string FidelizarAssemblyPrefix = "Fidelizar.";

    /// <summary>
    /// Loads a Fidelizar assembly by name from the test project's own probing path. The assembly
    /// is guaranteed to be present there because Fidelizar.Api.Tests carries a test-only project
    /// reference to it (see the .csproj) purely so this reflection check has something to load —
    /// that reference is not part of the production dependency graph.
    /// </summary>
    private static Assembly LoadFidelizarAssembly(string simpleName) => Assembly.Load(simpleName);

    private static string[] ReferencedFidelizarAssemblies(Assembly assembly) =>
        assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(name => name is not null && name.StartsWith(FidelizarAssemblyPrefix, StringComparison.Ordinal))
            .Select(name => name!)
            .ToArray();

    [Fact]
    public void Client_references_only_Shared()
    {
        var clientAssembly = LoadFidelizarAssembly("Fidelizar.Client");

        var referenced = ReferencedFidelizarAssemblies(clientAssembly);

        // A reference to an assembly whose types are never actually used can be elided from the
        // compiled manifest, so "no Fidelizar assembly referenced at all" is also a pass here —
        // the invariant this test guards is negative ("never Domain/Application/Infrastructure/
        // Api"), not "Shared must always show up".
        Assert.All(referenced, name => Assert.Equal("Fidelizar.Shared", name));
    }

    [Fact]
    public void Domain_references_no_other_Fidelizar_project()
    {
        var domainAssembly = LoadFidelizarAssembly("Fidelizar.Domain");

        var referenced = ReferencedFidelizarAssemblies(domainAssembly);

        Assert.Empty(referenced);
    }

    /// <summary>
    /// Belt-and-suspenders on top of the reflection checks above: the C# compiler elides an
    /// AssemblyRef for a referenced project whose types are never actually used, so a bare
    /// "dotnet add reference" from Client to Domain/Application/Infrastructure/Api — added but
    /// not yet used anywhere — would not show up in <see cref="Assembly.GetReferencedAssemblies"/>
    /// and would slip past <see cref="Client_references_only_Shared"/> undetected. Reading the
    /// declared &lt;ProjectReference&gt; elements closes that gap.
    /// </summary>
    [Fact]
    public void Client_csproj_declares_only_a_Shared_project_reference()
    {
        var clientCsprojPath = FindSolutionRoot("src", "Fidelizar.Client", "Fidelizar.Client.csproj");

        var declaredReferences = XDocument.Load(clientCsprojPath)
            .Descendants("ProjectReference")
            .Select(e => ProjectNameFromInclude((string)e.Attribute("Include")!))
            .ToArray();

        Assert.Equal(["Fidelizar.Shared"], declaredReferences);
    }

    /// <summary>
    /// The other half of the §3 diagram, and the one that had already drifted: the arrows run
    /// <c>Api → Application → Domain</c> and <c>Api → Infrastructure → Domain</c>. There is no
    /// arrow from <c>Infrastructure</c> to <c>Application</c>. An unused reference is still a
    /// breach of the contract — it is exactly what lets a repository start calling a use case
    /// later, at which point the cycle is load-bearing and expensive to undo.
    /// </summary>
    [Theory]
    [InlineData("Fidelizar.Application", new[] { "Fidelizar.Domain" })]
    [InlineData("Fidelizar.Infrastructure", new[] { "Fidelizar.Domain" })]
    [InlineData("Fidelizar.Shared", new string[0])]
    public void Csproj_declares_only_the_project_references_ARCHITECTURE_allows(
        string project,
        string[] allowed)
    {
        var csprojPath = FindSolutionRoot("src", project, $"{project}.csproj");

        var declaredReferences = XDocument.Load(csprojPath)
            .Descendants("ProjectReference")
            .Select(e => ProjectNameFromInclude((string)e.Attribute("Include")!))
            .Order()
            .ToArray();

        Assert.Equal(allowed.Order().ToArray(), declaredReferences);
    }

    /// <summary>
    /// Extracts the project name from a <c>&lt;ProjectReference Include="..."&gt;</c> value.
    ///
    /// <para>
    /// <b>MSBuild always writes <c>\</c> as the path separator inside a .csproj, on every
    /// platform</b> — that is part of the file format, not a Windows artefact.
    /// <see cref="Path.GetFileNameWithoutExtension(string)"/> only treats <c>\</c> as a separator
    /// when running on Windows; on Linux it is an ordinary filename character, so the whole
    /// relative path comes back as the "file name" and the comparison fails with
    /// <c>"..\Fidelizar.Domain\Fidelizar.Domain"</c> instead of <c>"Fidelizar.Domain"</c>.
    /// </para>
    ///
    /// <para>
    /// CI runs on Linux (see .github/workflows), so the separator has to be normalised explicitly
    /// rather than left to the platform's idea of what a separator is. Found by CI on 2026-08-13,
    /// after the whole suite had been passing on a Windows dev machine.
    /// </para>
    /// </summary>
    private static string ProjectNameFromInclude(string include) =>
        Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));

    /// <summary>
    /// Regression test for the platform bug described on <see cref="ProjectNameFromInclude"/>.
    /// It uses a hard-coded MSBuild-style path instead of a real .csproj precisely so it fails on
    /// Linux the moment someone "simplifies" the helper back to a bare
    /// <see cref="Path.GetFileNameWithoutExtension(string)"/>.
    /// </summary>
    [Theory]
    [InlineData(@"..\Fidelizar.Domain\Fidelizar.Domain.csproj", "Fidelizar.Domain")]
    [InlineData(@"..\Fidelizar.Shared\Fidelizar.Shared.csproj", "Fidelizar.Shared")]
    [InlineData("../Fidelizar.Domain/Fidelizar.Domain.csproj", "Fidelizar.Domain")]
    [InlineData("Fidelizar.Domain.csproj", "Fidelizar.Domain")]
    public void ProjectNameFromInclude_reads_the_MSBuild_separator_on_every_platform(
        string include,
        string expected)
    {
        Assert.Equal(expected, ProjectNameFromInclude(include));
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
