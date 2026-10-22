using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Api;

/// <summary>把 Identity 的用户状态作为 Developers 与 Billing 的只读组合根依赖。</summary>
public sealed class DeveloperUserStatusReader(IUserRepository users)
    : IDeveloperUserStatusReader, IBillingUserStatusReader
{
    public async Task<bool> IsActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        return user?.CanAuthenticate == true;
    }

    public async Task<bool> IsActiveAdministratorAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        return user?.CanAuthenticate == true && user.Role == UserRole.Administrator;
    }
}
