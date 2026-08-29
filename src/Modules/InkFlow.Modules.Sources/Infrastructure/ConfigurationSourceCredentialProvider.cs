using InkFlow.Modules.Sources.Application;
using Microsoft.Extensions.Configuration;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>
/// 读取宿主配置中的来源凭据引用，作为本地/容器 SecretProvider 的最小适配器。
/// 生产环境应把该配置节接到 Docker Secret、Vault 或云 Secret Manager，不能把明文写入仓库。
/// </summary>
public sealed class ConfigurationSourceCredentialProvider(IConfiguration configuration)
    : ISourceCredentialProvider
{
    public const string ConfigurationSectionName = "SourceCredentials";

    public Task<SourceCredential?> ResolveAsync(
        string sourceId,
        string credentialReferenceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 只允许安全的配置节路径片段；拒绝 ':'、'/' 等路径注入字符。
        if (!IsSafeConfigurationKey(sourceId) ||
            !SourceCredentialReference.IsValid(credentialReferenceId))
        {
            return Task.FromResult<SourceCredential?>(null);
        }

        var section = configuration
            .GetSection(ConfigurationSectionName)
            .GetSection(sourceId)
            .GetSection(credentialReferenceId);
        var type = section["Type"]?.Trim();

        SourceCredential? credential = null;
        if (string.Equals(type, "bearer", StringComparison.OrdinalIgnoreCase))
        {
            SourceCredential.TryCreateBearerToken(section["Secret"], out credential);
        }
        else if (string.Equals(type, "basic", StringComparison.OrdinalIgnoreCase))
        {
            SourceCredential.TryCreateBasicAuthentication(
                section["Username"],
                section["Password"],
                out credential);
        }
        else if (string.Equals(type, "apiKeyHeader", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "api-key-header", StringComparison.OrdinalIgnoreCase))
        {
            SourceCredential.TryCreateApiKeyHeader(
                section["HeaderName"],
                section["Secret"],
                out credential);
        }

        return Task.FromResult(credential);
    }

    private static bool IsSafeConfigurationKey(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128)
        {
            return false;
        }

        return value.All(character =>
            character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or '.' or '_' or '-');
    }
}
