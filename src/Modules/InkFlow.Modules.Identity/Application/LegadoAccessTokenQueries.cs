using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

internal sealed class LegadoAccessTokenQueries(
    IUserRepository users,
    ILegadoAccessTokenRepository tokens,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<LegadoTokenInfo>> ListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var values = await tokens.ListForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return values.Select(LegadoAccessTokenMapper.ToInfo).ToList();
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
}
