using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

/// <summary>
/// Personal Legado Token 的稳定公开 facade。
/// 原始令牌只在签发结果中返回；验证和持久化路径只处理摘要。
/// </summary>
public sealed class LegadoAccessTokenService : ILegadoAccessTokenService
{
    private readonly LegadoAccessTokenCommands commands;
    private readonly LegadoAccessTokenQueries queries;

    public LegadoAccessTokenService(
        IUserRepository users,
        ILegadoAccessTokenRepository tokens,
        IOpaqueTokenGenerator tokenGenerator,
        TimeProvider clock,
        IdentityOptions options)
    {
        commands = new(users, tokens, tokenGenerator, clock, options);
        queries = new(users, tokens, clock);
    }

    public Task<LegadoTokenOperationResult> IssueAsync(
        Guid userId,
        string? name,
        CancellationToken cancellationToken = default) =>
        commands.IssueAsync(userId, name, cancellationToken);

    public Task<IReadOnlyList<LegadoTokenInfo>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        queries.ListAsync(userId, cancellationToken);

    public Task<LegadoTokenResultStatus> RevokeAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default) =>
        commands.RevokeAsync(userId, tokenId, cancellationToken);

    public Task<AuthenticatedLegadoToken?> ValidateAsync(
        string rawToken,
        LegadoTokenScope requiredScope,
        CancellationToken cancellationToken = default) =>
        queries.ValidateAsync(rawToken, requiredScope, cancellationToken);
}
