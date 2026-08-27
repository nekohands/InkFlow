using System.Security.Cryptography;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Modules.Identity.Infrastructure.Credentials;

public sealed class SecureOpaqueTokenGenerator : IOpaqueTokenGenerator
{
    public string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
