using System.Reflection;
using NetArchTest.Rules;

namespace InkFlow.ArchitectureTests;

internal static class ModuleDependencyPolicy
{
    internal const string ModulePrefix = "InkFlow.Modules.";

    // Reviewed module edges, not an automatically accepted snapshot of references.
    internal static readonly IReadOnlyDictionary<string, string[]> AllowedModules =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Identity"] = [],
            ["Library"] = ["Sources"],
            ["Sources"] = [],
            ["Crawling"] = ["Library", "Sources"],
            ["Content"] = ["Library", "Sources"],
            ["Reading"] = ["Library", "Content"],
            ["Search"] = ["Library"],
            ["Legado"] = ["Library", "Content"],
            ["Developers"] = [],
            ["Billing"] = [],
            ["Operations"] = [],
        };

    internal static readonly string[] Hosts =
        ["InkFlow.Api", "InkFlow.Worker", "InkFlow.Scheduler", "InkFlow.Migrations"];

    internal static IEnumerable<string> ForbiddenProjectReferences(string module, IEnumerable<string> references)
    {
        var allowed = AllowedDependencies(module);
        return references.Where(reference => Hosts.Contains(reference, StringComparer.Ordinal)
            || (reference.StartsWith(ModulePrefix, StringComparison.Ordinal)
                && !allowed.Contains(reference[ModulePrefix.Length..], StringComparer.Ordinal)));
    }

    internal static IReadOnlyList<DependencyViolation> FindViolations(Assembly assembly, string module)
    {
        var allowed = AllowedDependencies(module);
        var types = Types.InAssembly(assembly);
        var moduleNamespace = ModulePrefix + module;
        var violations = new List<DependencyViolation>();
        Check("DomainInfrastructure", moduleNamespace + ".Domain",
        [
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "StackExchange.Redis",
            "Npgsql", "InkFlow.BuildingBlocks.Persistence",
            .. AllowedModules.Keys.Select(name => ModulePrefix + name + ".Infrastructure"),
        ]);
        Check("ModuleHost", moduleNamespace, Hosts);
        Check("ForeignInfrastructure", moduleNamespace,
            AllowedModules.Keys.Where(name => name != module)
                .Select(name => ModulePrefix + name + ".Infrastructure").ToArray());
        Check("ModuleGraph", moduleNamespace,
            AllowedModules.Keys.Where(name => name != module && !allowed.Contains(name, StringComparer.Ordinal))
                .Select(name => ModulePrefix + name).ToArray());
        return violations;

        void Check(string rule, string sourceNamespace, string[] forbidden)
        {
            var result = types.That()
                // Full-name prefix includes internal nested types; the library's
                // namespace-regex selector omits them. The dot prevents lookalike matches.
                .ResideInNamespace(sourceNamespace + ".")
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();
            if (!result.IsSuccessful)
            {
                violations.AddRange(result.FailingTypeNames.Select(name =>
                    new DependencyViolation(rule, name.Replace('/', '+'))));
            }
        }
    }

    private static string[] AllowedDependencies(string module) => AllowedModules.TryGetValue(module, out var allowed)
        ? allowed
        : throw new ArgumentOutOfRangeException(nameof(module), module, "Module is not in the reviewed dependency graph.");
}

internal sealed record DependencyViolation(string Rule, string TypeName);
