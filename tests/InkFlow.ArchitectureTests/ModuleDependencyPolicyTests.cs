using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ArchitectureTests;

[TestClass]
public sealed class ModuleDependencyPolicyTests
{
    [TestMethod]
    [DataRow("Reading", "DomainInfrastructure", typeof(Modules.Reading.Domain.ArchitectureFixtures.EfMethodBody))]
    [DataRow("Reading", "DomainInfrastructure", typeof(Modules.Reading.Domain.ArchitectureFixtures.AspNetAsyncBody))]
    [DataRow("Reading", "DomainInfrastructure", typeof(Modules.Reading.Domain.ArchitectureFixtures.RedisGenericProperty))]
    [DataRow("Reading", "DomainInfrastructure", typeof(Modules.Reading.Domain.ArchitectureFixtures.EfGenericConstraint<>))]
    [DataRow("Reading", "DomainInfrastructure", typeof(Modules.Reading.Domain.ArchitectureFixtures.AspNetAttribute))]
    [DataRow("Reading", "DomainInfrastructure", typeof(Modules.Reading.Domain.ArchitectureFixtures.OwnPersistenceProperty))]
    [DataRow("Reading", "ModuleHost", typeof(Modules.Reading.Application.ArchitectureFixtures.HostMethodBody))]
    [DataRow("Reading", "ForeignInfrastructure", typeof(Modules.Reading.Application.ArchitectureFixtures.ForeignPersistence))]
    [DataRow("Sources", "ModuleGraph", typeof(Modules.Sources.Application.ArchitectureFixtures.ForbiddenModule))]
    public void Forbidden_dependencies_are_detected_from_compiled_types(
        string module,
        string rule,
        Type fixture)
    {
        var violations = ModuleDependencyPolicy.FindViolations(typeof(ModuleDependencyPolicyTests).Assembly, module);

        Assert.IsTrue(
            violations.Any(violation => violation.Rule == rule && violation.TypeName == fixture.FullName),
            $"Expected {rule} to reject {fixture.FullName}; got {string.Join(", ", violations)}");
    }

    [TestMethod]
    [DataRow("Reading", typeof(Modules.Reading.Domain.ArchitectureFixtures.PureDomain))]
    [DataRow("Reading", typeof(Modules.Reading.Application.ArchitectureFixtures.AllowedLibraryContract))]
    [DataRow("Reading", typeof(Modules.Reading.Application.ArchitectureFixtures.SimilarNamespace))]
    [DataRow("Reading", typeof(Modules.Reading.Infrastructure.ArchitectureFixtures.OwnPersistence))]
    [DataRow("Sources", typeof(Modules.Sources.Application.ArchitectureFixtures.OwnDomain))]
    public void Legal_dependencies_are_not_rejected(string module, Type fixture)
    {
        var violations = ModuleDependencyPolicy.FindViolations(typeof(ModuleDependencyPolicyTests).Assembly, module);

        Assert.IsFalse(violations.Any(violation => violation.TypeName == fixture.FullName));
    }

    [TestMethod]
    public void Nested_implementation_cannot_hide_a_host_dependency()
    {
        var violations = ModuleDependencyPolicy.FindViolations(typeof(ModuleDependencyPolicyTests).Assembly, "Reading");
        var container = typeof(Modules.Reading.Application.ArchitectureFixtures.NestedHost).FullName!;

        Assert.IsTrue(violations.Any(violation => violation.Rule == "ModuleHost"
            && (violation.TypeName == container || violation.TypeName.StartsWith(container + "+", StringComparison.Ordinal))),
            $"Expected ModuleHost to reject a nested type in {container}; got {string.Join(", ", violations)}");
    }

    [TestMethod]
    [DataRow("Sources", "InkFlow.Modules.Reading")]
    [DataRow("Reading", "InkFlow.Modules.Future")]
    [DataRow("Reading", "InkFlow.Api")]
    [DataRow("Reading", "InkFlow.Worker")]
    [DataRow("Reading", "InkFlow.Scheduler")]
    [DataRow("Reading", "InkFlow.Migrations")]
    public void Forbidden_project_references_are_rejected_even_when_no_type_uses_them(string module, string reference)
    {
        CollectionAssert.AreEqual(new[] { reference },
            ModuleDependencyPolicy.ForbiddenProjectReferences(module, [reference]).ToArray());
    }

    [TestMethod]
    public void Unknown_source_module_cannot_silently_escape_type_checks()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ModuleDependencyPolicy.FindViolations(typeof(ModuleDependencyPolicyTests).Assembly, "Future"));
    }

    [TestMethod]
    [DataRow("Reading", "InkFlow.Modules.Library")]
    [DataRow("Reading", "InkFlow.Modules.Content")]
    [DataRow("Sources", "InkFlow.BuildingBlocks.Security")]
    [DataRow("Sources", "InkFlow.BuildingBlocks.Domain")]
    public void Legal_project_references_are_not_rejected(string module, string reference)
    {
        Assert.IsEmpty(ModuleDependencyPolicy.ForbiddenProjectReferences(module, [reference]).ToArray());
    }

    [TestMethod]
    public void Unknown_source_module_cannot_silently_escape_project_checks()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ModuleDependencyPolicy.ForbiddenProjectReferences("Future", []));
    }
}
