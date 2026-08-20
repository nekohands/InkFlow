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
    public void Library_does_not_reference_legado()
    {
        var references = typeof(InkFlow.Modules.Library.LibraryModule).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        CollectionAssert.DoesNotContain(references, "InkFlow.Modules.Legado");
    }
}
