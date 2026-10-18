using System.Globalization;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Parses one untrusted Set-Cookie value into bounded execution-local state.
/// Storage and request projection remain with <see cref="RuleCookieJar"/>.
/// </summary>
internal static class RuleCookieParser
{
    private const int MaxCookieNameLength = 128;
    private const int MaxCookieValueLength = 2_048;

    public static RuleCookieParseResult Parse(
        string raw,
        Uri responseUri,
        RuleSession policy)
    {
        if (raw.Any(char.IsControl))
        {
            return new(null, "session: response cookie is invalid.");
        }

        var segments = raw.Split(';');
        var first = segments[0].Trim();
        var equals = first.IndexOf('=');
        if (equals <= 0)
        {
            return new(null, null);
        }

        var name = first[..equals].Trim();
        var value = first[(equals + 1)..].Trim();
        if (name.Length > MaxCookieNameLength ||
            value.Length > MaxCookieValueLength ||
            !IsCookieName(name) ||
            value.Any(character => character is ';' || char.IsControl(character)))
        {
            return new(null, null);
        }

        var now = DateTimeOffset.UtcNow;
        var maxLifetime = TimeSpan.FromSeconds(policy.MaxCookieLifetimeSeconds);
        var domain = responseUri.Host.ToLowerInvariant();
        var hostOnly = true;
        var path = DefaultPath(responseUri.AbsolutePath);
        var secure = false;
        var expiresAt = now.Add(maxLifetime);
        var delete = false;
        var maxAgeSeen = false;

        foreach (var segment in segments.Skip(1))
        {
            var attribute = segment.Trim();
            if (attribute.Length == 0)
            {
                continue;
            }

            var separator = attribute.IndexOf('=');
            var attributeName = (separator < 0 ? attribute : attribute[..separator]).Trim();
            var attributeValue = separator < 0 ? string.Empty : attribute[(separator + 1)..].Trim();

            if (attributeName.Equals("domain", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = attributeValue.TrimStart('.').TrimEnd('.').ToLowerInvariant();
                if (candidate.Length == 0 || !HostMatchesDomain(responseUri.Host, candidate))
                {
                    return new(null, null);
                }

                domain = candidate;
                hostOnly = false;
            }
            else if (attributeName.Equals("path", StringComparison.OrdinalIgnoreCase))
            {
                if (attributeValue.Length == 0 ||
                    !attributeValue.StartsWith('/') ||
                    attributeValue.Any(char.IsControl))
                {
                    return new(null, null);
                }

                path = attributeValue;
            }
            else if (attributeName.Equals("secure", StringComparison.OrdinalIgnoreCase))
            {
                secure = true;
            }
            else if (attributeName.Equals("max-age", StringComparison.OrdinalIgnoreCase) &&
                     long.TryParse(
                         attributeValue,
                         NumberStyles.Integer,
                         CultureInfo.InvariantCulture,
                         out var seconds))
            {
                maxAgeSeen = true;
                if (seconds <= 0)
                {
                    delete = true;
                }
                else
                {
                    expiresAt = now.AddSeconds(Math.Min(seconds, policy.MaxCookieLifetimeSeconds));
                }
            }
            else if (!maxAgeSeen &&
                     attributeName.Equals("expires", StringComparison.OrdinalIgnoreCase) &&
                     DateTimeOffset.TryParse(
                         attributeValue,
                         CultureInfo.InvariantCulture,
                         DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                         out var expires))
            {
                if (expires <= now)
                {
                    delete = true;
                }
                else
                {
                    expiresAt = expires > now.Add(maxLifetime)
                        ? now.Add(maxLifetime)
                        : expires.ToUniversalTime();
                }
            }
        }

        return new(
            new RuleCookie(
                new RuleCookieKey(name, domain, path),
                name,
                value,
                domain,
                hostOnly,
                path,
                secure,
                expiresAt,
                delete),
            null);
    }

    internal static bool HostMatchesDomain(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);

    private static string DefaultPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/'))
        {
            return "/";
        }

        var lastSlash = path.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : path[..lastSlash];
    }

    private static bool IsCookieName(string value) =>
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character));
}

internal sealed record RuleCookieParseResult(RuleCookie? Cookie, string? Error);

internal readonly record struct RuleCookieKey(string Name, string Domain, string Path);

internal sealed record RuleCookie(
    RuleCookieKey Key,
    string Name,
    string Value,
    string Domain,
    bool HostOnly,
    string Path,
    bool Secure,
    DateTimeOffset ExpiresAt,
    bool IsDeletion,
    int PairBytes = 0);
