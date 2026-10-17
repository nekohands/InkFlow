using InkFlow.Modules.Developers.Domain;

namespace InkFlow.Modules.Developers.Application;

/// <summary>开发者应用的用户范围读取。</summary>
internal sealed class DeveloperApplicationQueries(IDeveloperApplicationRepository applications)
{
    public async Task<IReadOnlyList<DeveloperApplicationView>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var items = await applications
            .ListForUserAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        return items.Select(DeveloperApplicationViewMapper.ToView).ToList();
    }
}
