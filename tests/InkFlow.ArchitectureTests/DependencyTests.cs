using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ArchitectureTests;

[TestClass]
public sealed class DependencyTests
{
    [TestMethod]
    public void Domain_building_block_has_no_infrastructure_dependencies()
    {
        var references = typeof(InkFlow.BuildingBlocks.Domain.Uuid7).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        CollectionAssert.DoesNotContain(references, "Microsoft.EntityFrameworkCore");
        CollectionAssert.DoesNotContain(references, "Npgsql");
        CollectionAssert.DoesNotContain(references, "Microsoft.AspNetCore");
    }

    [TestMethod]
    public void Module_dependency_matrix_is_respected()
    {
        AssertInkFlowReferencesOnly(
            typeof(InkFlow.Modules.Identity.IdentityModule).Assembly,
            "InkFlow.BuildingBlocks.Domain",
            "InkFlow.BuildingBlocks.Application");

        AssertInkFlowReferencesOnly(
            typeof(InkFlow.Modules.Library.LibraryModule).Assembly,
            "InkFlow.BuildingBlocks.Domain",
            "InkFlow.BuildingBlocks.Application");

        AssertInkFlowReferencesOnly(
            typeof(InkFlow.Modules.Sources.SourcesModule).Assembly,
            "InkFlow.BuildingBlocks.Domain",
            "InkFlow.BuildingBlocks.Application");

        AssertInkFlowReferencesOnly(
            typeof(InkFlow.Modules.Crawling.CrawlingModule).Assembly,
            "InkFlow.BuildingBlocks.Domain",
            "InkFlow.BuildingBlocks.Application",
            "InkFlow.Modules.Sources");

        AssertInkFlowReferencesOnly(
            typeof(InkFlow.Modules.Content.ContentModule).Assembly,
            "InkFlow.BuildingBlocks.Domain",
            "InkFlow.BuildingBlocks.Application",
            "InkFlow.Modules.Library",
            "InkFlow.Modules.Sources");

        AssertInkFlowReferencesOnly(
            typeof(InkFlow.Modules.Reading.ReadingModule).Assembly,
            "InkFlow.BuildingBlocks.Domain",
            "InkFlow.BuildingBlocks.Application",
            "InkFlow.Modules.Library",
            "InkFlow.Modules.Content");

        AssertInkFlowReferencesOnly(
            typeof(InkFlow.Modules.Search.SearchModule).Assembly,
            "InkFlow.BuildingBlocks.Application",
            "InkFlow.Modules.Library");

        AssertInkFlowReferencesOnly(
            typeof(InkFlow.Modules.Legado.LegadoModule).Assembly,
            "InkFlow.BuildingBlocks.Application",
            "InkFlow.Modules.Library",
            "InkFlow.Modules.Content");
    }

    [TestMethod]
    public void Business_modules_do_not_reference_application_hosts_or_persistence()
    {
        var modules = new[]
        {
            typeof(InkFlow.Modules.Identity.IdentityModule).Assembly,
            typeof(InkFlow.Modules.Library.LibraryModule).Assembly,
            typeof(InkFlow.Modules.Sources.SourcesModule).Assembly,
            typeof(InkFlow.Modules.Crawling.CrawlingModule).Assembly,
            typeof(InkFlow.Modules.Content.ContentModule).Assembly,
            typeof(InkFlow.Modules.Reading.ReadingModule).Assembly,
            typeof(InkFlow.Modules.Search.SearchModule).Assembly,
            typeof(InkFlow.Modules.Legado.LegadoModule).Assembly
        };

        foreach (var module in modules)
        {
            var references = GetInkFlowReferences(module);
            Assert.IsFalse(references.Any(reference => reference.StartsWith("InkFlow.Api", StringComparison.Ordinal)));
            Assert.IsFalse(references.Any(reference => reference.StartsWith("InkFlow.Worker", StringComparison.Ordinal)));
            Assert.IsFalse(references.Any(reference => reference.StartsWith("InkFlow.Scheduler", StringComparison.Ordinal)));
            CollectionAssert.DoesNotContain(references, "InkFlow.BuildingBlocks.Persistence");
        }
    }

    private static void AssertInkFlowReferencesOnly(Assembly assembly, params string[] allowed)
    {
        var actual = GetInkFlowReferences(assembly);
        var unexpected = actual.Except(allowed, StringComparer.Ordinal).ToArray();

        Assert.HasCount(0, unexpected, $"{assembly.GetName().Name} has unexpected InkFlow references: {string.Join(", ", unexpected)}");
    }

    private static string[] GetInkFlowReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && name.StartsWith("InkFlow.", StringComparison.Ordinal))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
}
