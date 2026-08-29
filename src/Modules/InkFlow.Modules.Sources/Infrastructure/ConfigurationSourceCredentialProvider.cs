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
        SourceCredentialResolutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!context.IsValid ||
            context.OwnerScope!.Kind != SourceCredentialOwnerKind.Platform)
        {
            return Task.FromResult<SourceCredential?>(null);
        }

        var section = configuration
            .GetSection(ConfigurationSectionName)
            .GetSection(context.SourceId)
            .GetSection(context.CredentialReferenceId);
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
}
