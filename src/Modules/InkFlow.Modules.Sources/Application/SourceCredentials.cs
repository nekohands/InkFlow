using System.Text;

namespace InkFlow.Modules.Sources.Application;

/// <summary>允许由 SecretProvider 解析并转换为一次性请求头的凭据形态。</summary>
public enum SourceCredentialKind
{
    BearerToken,
    BasicAuthentication,
    ApiKeyHeader,
}

/// <summary>
/// 来源凭据的短生命周期内存表示。
/// 原文没有公开属性，且 ToString 不输出任何 secret；调用方只能通过受控请求头投影使用它。
/// </summary>
public sealed class SourceCredential
{
    public const int MaxSecretLength = 4_096;
    public const int MaxUsernameLength = 256;
    public const int MaxHeaderNameLength = 128;
    public const int MaxHeaderValueLength = 16 * 1024;

    private static readonly HashSet<string> ForbiddenHeaderNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "authorization",
            "cookie",
            "set-cookie",
            "connection",
            "content-length",
            "host",
            "proxy-authenticate",
            "proxy-authorization",
            "te",
            "trailer",
            "transfer-encoding",
            "upgrade",
        };

    private SourceCredential(
        SourceCredentialKind kind,
        string secret,
        string? username = null,
        string? headerName = null)
    {
        Kind = kind;
        Secret = secret;
        Username = username;
        HeaderName = headerName;
    }

    public SourceCredentialKind Kind { get; }

    // These values stay internal to the Sources assembly. They are never serialized or logged.
    internal string Secret { get; }
    internal string? Username { get; }
    internal string? HeaderName { get; }

    public static SourceCredential BearerToken(string token) =>
        TryCreateBearerToken(token, out var credential)
            ? credential!
            : throw new ArgumentException("bearer token is invalid or exceeds the credential limit.", nameof(token));

    public static SourceCredential BasicAuthentication(string username, string password) =>
        TryCreateBasicAuthentication(username, password, out var credential)
            ? credential!
            : throw new ArgumentException("basic authentication material is invalid or exceeds the credential limit.", nameof(username));

    public static SourceCredential ApiKeyHeader(string headerName, string token) =>
        TryCreateApiKeyHeader(headerName, token, out var credential)
            ? credential!
            : throw new ArgumentException("API key header material is invalid or exceeds the credential limit.", nameof(headerName));

    internal static bool TryCreateBearerToken(
        string? token,
        out SourceCredential? credential)
    {
        credential = null;
        if (!IsNonEmptyBoundedToken(token, MaxSecretLength))
        {
            return false;
        }

        credential = new SourceCredential(SourceCredentialKind.BearerToken, token!);
        return true;
    }

    internal static bool TryCreateBasicAuthentication(
        string? username,
        string? password,
        out SourceCredential? credential)
    {
        credential = null;
        if (string.IsNullOrWhiteSpace(username) ||
            username.Length > MaxUsernameLength ||
            username.Any(char.IsControl) ||
            username.Contains(':') ||
            !IsNonEmptySecret(password, MaxSecretLength))
        {
            return false;
        }

        credential = new SourceCredential(
            SourceCredentialKind.BasicAuthentication,
            password!,
            username);
        return true;
    }

    internal static bool TryCreateApiKeyHeader(
        string? headerName,
        string? token,
        out SourceCredential? credential)
    {
        credential = null;
        if (!IsSafeHeaderName(headerName) || !IsNonEmptyBoundedToken(token, MaxSecretLength))
        {
            return false;
        }

        credential = new SourceCredential(
            SourceCredentialKind.ApiKeyHeader,
            token!,
            headerName: headerName);
        return true;
    }

    /// <summary>把凭据投影为单一安全请求头；原文不离开本程序集的执行路径。</summary>
    internal bool TryBuildHeader(out string headerName, out string headerValue)
    {
        switch (Kind)
        {
            case SourceCredentialKind.BearerToken:
                headerName = "Authorization";
                headerValue = $"Bearer {Secret}";
                return IsSafeHeaderValue(headerValue);

            case SourceCredentialKind.BasicAuthentication:
                headerName = "Authorization";
                var basicBytes = Encoding.UTF8.GetBytes($"{Username}:{Secret}");
                headerValue = $"Basic {Convert.ToBase64String(basicBytes)}";
                return IsSafeHeaderValue(headerValue);

            case SourceCredentialKind.ApiKeyHeader:
                headerName = HeaderName!;
                headerValue = Secret;
                return IsSafeHeaderValue(headerValue);

            default:
                headerName = string.Empty;
                headerValue = string.Empty;
                return false;
        }
    }

    public override string ToString() => $"SourceCredential({Kind})";

    private static bool IsNonEmptyBoundedToken(string? value, int maximumLength) =>
        IsNonEmptySecret(value, maximumLength) && !value!.Any(char.IsWhiteSpace);

    private static bool IsNonEmptySecret(string? value, int maximumLength) =>
        !string.IsNullOrEmpty(value) &&
        value.Length <= maximumLength &&
        !value.Any(char.IsControl);

    private static bool IsSafeHeaderValue(string value) =>
        value.Length <= MaxHeaderValueLength && !value.Any(char.IsControl);

    private static bool IsSafeHeaderName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > MaxHeaderNameLength ||
            ForbiddenHeaderNames.Contains(value) ||
            value.StartsWith("proxy-", StringComparison.OrdinalIgnoreCase) ||
            value[0] == '-' ||
            value[^1] == '-')
        {
            return false;
        }

        return value.All(character =>
            character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or '-');
    }
}

/// <summary>
/// 一次来源操作的非敏感上下文。凭据只携带引用 ID，解析后的 secret 不进入此对象。
/// </summary>
public sealed record SourceExecutionContext(
    string SourceId,
    string? CredentialReferenceId = null)
{
    public bool HasCredentialReference => !string.IsNullOrEmpty(CredentialReferenceId);
}

/// <summary>来源凭据引用 ID 的 fail-closed 语法边界，亦防止配置节路径注入。</summary>
public static class SourceCredentialReference
{
    public const int MaxLength = 256;

    public static bool IsValid(string? referenceId)
    {
        if (string.IsNullOrEmpty(referenceId) || referenceId.Length > MaxLength)
        {
            return false;
        }

        for (var index = 0; index < referenceId.Length; index++)
        {
            var character = referenceId[index];
            var isAlphaNumeric = character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9';
            if (index == 0 && !isAlphaNumeric)
            {
                return false;
            }

            if (!isAlphaNumeric && character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// CredentialReference 的 SecretProvider seam。
/// 实现方负责 owner/source 权限和安全存储；返回值仅用于当前受控请求执行。
/// </summary>
public interface ISourceCredentialProvider
{
    Task<SourceCredential?> ResolveAsync(
        string sourceId,
        string credentialReferenceId,
        CancellationToken cancellationToken = default);
}
