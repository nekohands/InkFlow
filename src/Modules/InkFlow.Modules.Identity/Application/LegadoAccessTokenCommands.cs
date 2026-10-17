using InkFlow.Modules.Identity.Domain;

namespace InkFlow.Modules.Identity.Application;

internal static class LegadoAccessTokenMapper
{
    internal static LegadoTokenInfo ToInfo(LegadoAccessToken token) =>
        new(
            token.Id,
            token.UserId,
            token.Name,
            token.Prefix,
            token.Scope,
            token.CreatedAt,
            token.ExpiresAt,
            token.RevokedAt);
}

internal sealed class LegadoAccessTokenCommands(
    IUserRepository users,
    ILegadoAccessTokenRepository tokens,
    IOpaqueTokenGenerator tokenGenerator,
    TimeProvider clock,
    IdentityOptions options)
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
            now + _options.LegadoTokenLifetime);

        await tokens.AddAsync(token, cancellationToken).ConfigureAwait(false);
        return LegadoTokenOperationResult.Success(
            new LegadoTokenIssue(LegadoAccessTokenMapper.ToInfo(token), rawToken));
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
            .RevokeAsync(userId, tokenId, cancellationToken)
            .ConfigureAwait(false);
        return revoked
            ? LegadoTokenResultStatus.Success
            : LegadoTokenResultStatus.NotFound;
    }

    private static string NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Reading 3.0" : name.Trim();

    private static IdentityOptions ValidateOptions(IdentityOptions value)
    {
        value.Validate();
        return value;
    }
}
