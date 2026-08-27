using System.Globalization;
using System.Security.Cryptography;
using InkFlow.Modules.Identity.Application;

namespace InkFlow.Modules.Identity.Infrastructure.Credentials;

/// <summary>
/// PHC-like PBKDF2-SHA256 密码格式。数据库只保存带随机 salt 的派生结果。
/// 解析时限制迭代次数范围，防止被恶意哈希值制造计算型 DoS。
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string FormatPrefix = "$inkflow-pbkdf2-sha256$";
    public const int Iterations = 600_000;
    private const int SaltLength = 16;
    private const int DerivedKeyLength = 32;
    private const int MinimumIterations = 100_000;
    private const int MaximumIterations = 2_000_000;

    public string Hash(string password)
    {
        ValidatePassword(password);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var derived = Derive(password, salt, Iterations);
        return $"{FormatPrefix}{Iterations}${Encode(salt)}${Encode(derived)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            var parts = passwordHash.Split('$');
            if (parts.Length != 5 || $"${parts[1]}$" != FormatPrefix ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
                iterations is < MinimumIterations or > MaximumIterations)
            {
                return false;
            }

            var salt = Decode(parts[3]);
            var expected = Decode(parts[4]);
            if (salt.Length != SaltLength || expected.Length != DerivedKeyLength)
            {
                return false;
            }

            var actual = Derive(password, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            DerivedKeyLength);

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            password.Length is < IdentityService.MinimumPasswordLength or > IdentityService.MaximumPasswordLength)
        {
            throw new ArgumentException("password length is outside the supported range.", nameof(password));
        }
    }

    private static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Decode(string value)
    {
        var standard = value.Replace('-', '+').Replace('_', '/');
        standard += new string('=', (4 - standard.Length % 4) % 4);
        return Convert.FromBase64String(standard);
    }
}
