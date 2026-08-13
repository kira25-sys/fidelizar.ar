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
            .Select(e => Path.GetFileNameWithoutExtension((string)e.Attribute("Include")!))
            .ToArray();

        Assert.Equal(["Fidelizar.Shared"], declaredReferences);
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
