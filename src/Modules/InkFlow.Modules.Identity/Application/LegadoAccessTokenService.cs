using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

/// <summary>
/// 编排 Personal Legado Token 的签发、撤销和验证。
/// 原始令牌只在签发结果中返回；验证和持久化路径只处理摘要。
/// </summary>
public sealed class LegadoAccessTokenService(
    IUserRepository users,
    ILegadoAccessTokenRepository tokens,
    IOpaqueTokenGenerator tokenGenerator,
    TimeProvider clock,
    IdentityOptions options) : ILegadoAccessTokenService
{
    private readonly IdentityOptions _options = ValidateOptions(options);

    public async Task<LegadoTokenOperationResult> IssueAsync(
        Guid userId,
        string? name,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return LegadoTokenOperationResult.Failure(LegadoTokenResultStatus.InvalidRequest);
        }

        var user = await users.GetAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !user.CanAuthenticate)
        {
            return LegadoTokenOperationResult.Failure(LegadoTokenResultStatus.NotFound);
        }

        var normalizedName = NormalizeName(name);
        if (normalizedName.Length > LegadoAccessToken.MaxNameLength)
        {
            return LegadoTokenOperationResult.Failure(LegadoTokenResultStatus.InvalidRequest);
        }

        var generatedToken = tokenGenerator.CreateToken();
        if (string.IsNullOrWhiteSpace(generatedToken) ||
            generatedToken.Length > 500 ||
            generatedToken.Any(char.IsWhiteSpace))
        {
            return LegadoTokenOperationResult.Failure(LegadoTokenResultStatus.InvalidRequest);
        }

        var now = clock.GetUtcNow();
        var rawToken = LegadoAccessToken.TokenPrefix + generatedToken;
        var prefixLength = Math.Min(
            rawToken.Length,
            LegadoAccessToken.TokenPrefix.Length + 8);
        var prefix = rawToken[..prefixLength];
        var token = LegadoAccessToken.Create(
            user.Id,
            normalizedName,
            prefix,
            OpaqueTokenHashing.Hash(rawToken),
            LegadoTokenScope.Read,
            now,
            now + options.LegadoTokenLifetime);

        await tokens.AddAsync(token, cancellationToken).ConfigureAwait(false);
        return LegadoTokenOperationResult.Success(new LegadoTokenIssue(Map(token), rawToken));
    }

    public async Task<IReadOnlyList<LegadoTokenInfo>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var values = await tokens.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return values.Select(Map).ToList();
    }

    public async Task<LegadoTokenResultStatus> RevokeAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || tokenId == Guid.Empty)
        {
            return LegadoTokenResultStatus.InvalidRequest;
        }

        var revoked = await tokens
            .RevokeAsync(userId, tokenId, clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        return revoked
            ? LegadoTokenResultStatus.Success
            : LegadoTokenResultStatus.NotFound;
    }

    public async Task<AuthenticatedLegadoToken?> ValidateAsync(
        string rawToken,
        LegadoTokenScope requiredScope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken) ||
            rawToken.Length > 512 ||
            rawToken.Any(char.IsWhiteSpace) ||
            requiredScope == LegadoTokenScope.None)
        {
            return null;
        }

        var token = await tokens
            .FindByHashAsync(OpaqueTokenHashing.Hash(rawToken), cancellationToken)
            .ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (token is null || !token.IsActive(now) || !token.HasScope(requiredScope))
        {
            return null;
        }

        var user = await users.GetAsync(token.UserId, cancellationToken).ConfigureAwait(false);
        return user is null || !user.CanAuthenticate
            ? null
            : new AuthenticatedLegadoToken(token.UserId, token.Id, token.Scope);
    }

    private static string NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Reading 3.0" : name.Trim();

    private static LegadoTokenInfo Map(LegadoAccessToken token) =>
        new(
            token.Id,
            token.UserId,
            token.Name,
            token.Prefix,
            token.Scope,
            token.CreatedAt,
            token.ExpiresAt,
            token.RevokedAt);

    private static IdentityOptions ValidateOptions(IdentityOptions value)
    {
        value.Validate();
        return value;
    }
}
