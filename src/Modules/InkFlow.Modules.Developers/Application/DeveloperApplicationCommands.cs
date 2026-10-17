using InkFlow.Modules.Developers.Domain;

namespace InkFlow.Modules.Developers.Application;

/// <summary>开发者应用的创建与撤销命令。</summary>
internal sealed class DeveloperApplicationCommands(
    IDeveloperApplicationRepository applications,
    IDeveloperUserStatusReader users,
    TimeProvider clock)
{
    public async Task<DeveloperOperationResult<DeveloperApplicationView>> CreateAsync(
        Guid userId,
        string? name,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || !await users.IsActiveAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return DeveloperOperationResult<DeveloperApplicationView>.Failure(
                DeveloperOperationStatus.InvalidRequest);
        }

        try
        {
            var application = DeveloperApplication.Create(userId, name ?? string.Empty, clock.GetUtcNow());
            if (!await applications.AddAsync(application, cancellationToken).ConfigureAwait(false))
            {
                return DeveloperOperationResult<DeveloperApplicationView>.Failure(
                    DeveloperOperationStatus.LimitReached);
            }

            return DeveloperOperationResult<DeveloperApplicationView>.Success(
                DeveloperApplicationViewMapper.ToView(application));
        }
        catch (ArgumentException)
        {
            return DeveloperOperationResult<DeveloperApplicationView>.Failure(
                DeveloperOperationStatus.InvalidRequest);
        }
    }

    public async Task<DeveloperOperationStatus> RevokeAsync(
        Guid userId,
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var revoked = await applications.RevokeAsync(
            userId,
            applicationId,
            clock.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);
        return revoked ? DeveloperOperationStatus.Success : DeveloperOperationStatus.NotFound;
    }
}
