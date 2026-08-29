using System.Security.Cryptography;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;

namespace InkFlow.Modules.Developers.Infrastructure.Credentials;

public sealed class DeveloperApiKeySecretGenerator : IDeveloperApiKeySecretGenerator
{
    public DeveloperApiKeySecret Generate()
    {
        var random = RandomNumberGenerator.GetBytes(32);
        var encoded = Convert.ToBase64String(random)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var raw = $"lf_dev_{encoded}";
        var prefix = raw[..Math.Min(raw.Length, 15)];
        return new DeveloperApiKeySecret(
            raw,
            prefix,
            DeveloperApiKey.HashSecret(raw));
    }
}
