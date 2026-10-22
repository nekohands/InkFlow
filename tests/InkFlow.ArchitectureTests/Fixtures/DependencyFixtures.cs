// Deliberately invalid types live only in this test assembly. Production checks
// load module assemblies explicitly and never include these fixtures.
namespace InkFlow.Modules.Reading.Domain.ArchitectureFixtures
{
    internal static class EfMethodBody
    {
        public static string Read() => typeof(Microsoft.EntityFrameworkCore.DbContext).FullName!;
    }

    internal static class PureDomain
    {
        public static string Read(DateTimeOffset value) => value.ToString("O");
    }

    internal static class AspNetAsyncBody
    {
        public static async Task<Type> ReadAsync()
        {
            await Task.Yield();
            return typeof(Microsoft.AspNetCore.Http.HttpContext);
        }
    }

    internal sealed class RedisGenericProperty
    {
        public IReadOnlyList<StackExchange.Redis.RedisKey[]> Keys => [];
    }

    internal sealed class EfGenericConstraint<T> where T : Microsoft.EntityFrameworkCore.DbContext;

    [Microsoft.AspNetCore.Authorization.Authorize]
    internal sealed class AspNetAttribute;

    internal sealed class OwnPersistenceProperty
    {
        public Infrastructure.Persistence.ReadingDbContext? Context { get; init; }
    }
}

namespace InkFlow.Modules.Reading.Application.ArchitectureFixtures
{
    internal static class HostMethodBody
    {
        public static string Read() => InkFlow.Api.ArchitectureFixtures.HostEntry.Read();
    }

    internal static class ForeignPersistence
    {
        public static Type Read() => typeof(Library.Infrastructure.Persistence.LibraryDbContext);
    }

    internal static class AllowedLibraryContract
    {
        public static Type Read() => typeof(Library.Application.ICanonicalBookRepository);
    }

    internal static class SimilarNamespace
    {
        public static Type Read() => typeof(Library.InfrastructureLookalike.ArchitectureFixture);
    }

    internal static class NestedHost
    {
        internal static class Implementation
        {
            public static string Read() => InkFlow.Api.ArchitectureFixtures.HostEntry.Read();
        }
    }
}

namespace InkFlow.Modules.Reading.Infrastructure.ArchitectureFixtures
{
    internal static class OwnPersistence
    {
        public static Type Read() => typeof(Persistence.ReadingDbContext);
    }
}

namespace InkFlow.Modules.Library.InfrastructureLookalike
{
    internal sealed class ArchitectureFixture;
}

namespace InkFlow.Modules.Sources.Application.ArchitectureFixtures
{
    internal static class ForbiddenModule
    {
        public static Type Read() => typeof(Reading.Application.ReadingStateService);
    }

    internal static class OwnDomain
    {
        public static Type Read() => typeof(Domain.Source);
    }
}

namespace InkFlow.Api.ArchitectureFixtures
{
    internal static class HostEntry
    {
        public static string Read() => "fixture";
    }
}
