using InkFlow.Modules.Developers.Domain;

namespace InkFlow.Modules.Developers.Application;

internal static class DeveloperApplicationViewMapper
{
    public static DeveloperApplicationView ToView(DeveloperApplication application) =>
        new(
            application.Id,
            application.UserId,
            application.Name,
            application.Environment,
            application.CreatedAt,
            application.RevokedAt);
}
