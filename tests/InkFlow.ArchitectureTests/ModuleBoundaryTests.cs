using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ArchitectureTests;

[TestClass]
public sealed class ModuleBoundaryTests
{
    public static IEnumerable<object[]> Modules => ModuleDependencyPolicy.AllowedModules.Keys.Select(name => new object[] { name });

    [TestMethod]
    [DynamicData(nameof(Modules))]
    public void Production_module_respects_type_dependency_rules(string module)
    {
        var assembly = Assembly.Load(ModuleDependencyPolicy.ModulePrefix + module);
        // Nested types inherit their top-level owner's namespace. Only SDK-generated
        // global helpers and the regex generator's own namespace are exempt.
        var misplaced = assembly.GetTypes().Where(type => type.DeclaringType is null
            && !(type.Namespace == "System.Text.RegularExpressions.Generated"
                && type.GetCustomAttribute<GeneratedCodeAttribute>()?.Tool == "System.Text.RegularExpressions.Generator")
            && !(type.Namespace is null && type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            && type.Namespace != assembly.GetName().Name
            && !(type.Namespace?.StartsWith(assembly.GetName().Name + ".", StringComparison.Ordinal) ?? false)).ToArray();
        Assert.IsEmpty(misplaced, "Types outside their owning module namespace would escape namespace guards: "
            + string.Join(", ", misplaced.Select(type => type.FullName)));
        var violations = ModuleDependencyPolicy.FindViolations(assembly, module);

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));
    }

    [TestMethod]
    public void Every_module_project_is_reviewed_and_referenced_by_this_test_project()
    {
        var root = RepositoryRoot();
        var projects = Directory.GetFiles(Path.Combine(root, "src", "Modules"), "*.csproj", SearchOption.AllDirectories);
        var expected = ModuleDependencyPolicy.AllowedModules.Keys.Select(name => ModuleDependencyPolicy.ModulePrefix + name).ToArray();

        CollectionAssert.AreEquivalent(expected, projects.Select(Path.GetFileNameWithoutExtension).ToArray(),
            "New/removed module projects require an explicit dependency-policy review.");
        var references = ProjectReferences(Path.Combine(root, "tests", "InkFlow.ArchitectureTests", "InkFlow.ArchitectureTests.csproj"));
        CollectionAssert.AreEquivalent(expected,
            references.Where(name => name.StartsWith(ModuleDependencyPolicy.ModulePrefix, StringComparison.Ordinal)).ToArray(),
            "Every production module must be built and inspected, including the reserved Search module.");
    }

    [TestMethod]
    [DynamicData(nameof(Modules))]
    public void Production_module_project_references_follow_the_reviewed_graph(string module)
    {
        var name = ModuleDependencyPolicy.ModulePrefix + module;
        var project = Path.Combine(RepositoryRoot(), "src", "Modules", name, name + ".csproj");
        var forbidden = ModuleDependencyPolicy.ForbiddenProjectReferences(module, ProjectReferences(project)).ToArray();

        Assert.AreEqual(0, forbidden.Length, $"{name} has forbidden ProjectReference(s): {string.Join(", ", forbidden)}");
    }

    private static string[] ProjectReferences(string project) => XDocument.Load(project)
        .Descendants("ProjectReference")
        .Select(reference => Path.GetFileNameWithoutExtension(((string)reference.Attribute("Include")!).Replace('\\', '/')))
        .ToArray();

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "InkFlow.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Architecture project-inventory checks must run inside the InkFlow checkout.");
    }
}
