using System.Globalization;
using System.Text;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Small, execution-local Cookie jar. It intentionally supports only the attributes
/// needed for same-origin source pagination and never persists or logs cookie values.
/// </summary>
internal sealed class RuleCookieJar
{
    private const int MaxSetCookieHeaders = 64;
    private const int MaxCookieNameLength = 128;
    private const int MaxCookieValueLength = 2_048;

    private readonly RuleSession _policy;
    private readonly Dictionary<CookieKey, CookieEntry> _cookies = [];
    private int _cookieBytes;

    public RuleCookieJar(RuleSession policy)
    {
        _policy = policy;
    }

    public string? BuildCookieHeader(Uri requestUri)
    {
        RemoveExpired();

        var matching = _cookies.Values
            .Where(cookie => Matches(cookie, requestUri))
            .OrderByDescending(cookie => cookie.Path.Length)
            .ThenBy(cookie => cookie.Name, StringComparer.Ordinal)
            .ToArray();

        return matching.Length == 0
            ? null
            : string.Join("; ", matching.Select(cookie => $"{cookie.Name}={cookie.Value}"));
    }

    /// <summary>
    /// Accept response cookies. Invalid or out-of-origin cookies are ignored; resource
    /// limit violations fail the complete rule execution before any page is exposed.
    /// </summary>
    public string? Accept(
        IReadOnlyList<string> setCookieHeaders,
        Uri responseUri)
    {
        if (setCookieHeaders.Count > MaxSetCookieHeaders)
        {
            return "session: response cookie header limit exceeded.";
        }

        foreach (var header in setCookieHeaders)
        {
            if (header is null || string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            if (Encoding.UTF8.GetByteCount(header) > _policy.MaxCookieBytes)
            {
                return "session: response cookie exceeds the configured byte limit.";
            }

            var parsed = Parse(header, responseUri);
            if (parsed.Error is not null)
            {
                return parsed.Error;
            }

            if (parsed.Cookie is null)
            {
                continue;
            }

            var cookie = parsed.Cookie;
            if (cookie.IsDeletion)
            {
                Remove(cookie.Key);
                continue;
            }

            var pairBytes = CookiePairBytes(cookie.Name, cookie.Value);
            if (pairBytes > _policy.MaxCookieBytes)
            {
                return "session: response cookie exceeds the configured byte limit.";
            }

            var key = cookie.Key;
            var hadExisting = _cookies.TryGetValue(key, out var existing);
            if (!hadExisting && _cookies.Count >= _policy.MaxCookies)
            {
                return "session: cookie limit exceeded.";
            }

            var projectedBytes = _cookieBytes - (existing?.PairBytes ?? 0) + pairBytes;
            var projectedCount = hadExisting ? _cookies.Count : _cookies.Count + 1;
            if (CookieHeaderBytes(projectedBytes, projectedCount) > _policy.MaxCookieBytes)
            {
                return "session: cookie byte limit exceeded.";
            }

            _cookies[key] = cookie with { PairBytes = pairBytes };
            _cookieBytes = projectedBytes;
        }

        return null;
    }

    private ParseResult Parse(string raw, Uri responseUri)
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
        var maxLifetime = TimeSpan.FromSeconds(_policy.MaxCookieLifetimeSeconds);
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
                    expiresAt = now.AddSeconds(Math.Min(seconds, _policy.MaxCookieLifetimeSeconds));
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
            new CookieEntry(
                new CookieKey(name, domain, path),
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

    private bool Matches(CookieEntry cookie, Uri requestUri)
    {
        if (cookie.Secure && !requestUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = requestUri.Host;
        if (cookie.HostOnly
            ? !host.Equals(cookie.Domain, StringComparison.OrdinalIgnoreCase)
            : !HostMatchesDomain(host, cookie.Domain))
        {
            return false;
        }

        var requestPath = string.IsNullOrEmpty(requestUri.AbsolutePath) ? "/" : requestUri.AbsolutePath;
        return requestPath.Equals(cookie.Path, StringComparison.Ordinal) ||
            (requestPath.StartsWith(cookie.Path, StringComparison.Ordinal) &&
             (cookie.Path.EndsWith("/", StringComparison.Ordinal) ||
              requestPath.Length > cookie.Path.Length && requestPath[cookie.Path.Length] == '/'));
    }

    private void Remove(CookieKey key)
    {
        if (_cookies.Remove(key, out var removed))
        {
            _cookieBytes -= removed.PairBytes;
        }
    }

    private void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _cookies.Where(pair => pair.Value.ExpiresAt <= now).ToArray())
        {
            Remove(pair.Key);
        }
    }

    private static int CookiePairBytes(string name, string value) =>
        Encoding.UTF8.GetByteCount($"{name}={value}");

    private static int CookieHeaderBytes(int pairBytes, int cookieCount) =>
        pairBytes + (cookieCount <= 1 ? 0 : Encoding.UTF8.GetByteCount("; ") * (cookieCount - 1));

    private static string DefaultPath(string path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('/'))
        {
            return "/";
        }

        var lastSlash = path.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : path[..lastSlash];
    }

    private static bool HostMatchesDomain(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);

    private static bool IsCookieName(string value) =>
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || "!#$%&'*+-.^_`|~".Contains(character));

    private readonly record struct CookieKey(string Name, string Domain, string Path);

    private sealed record CookieEntry(
        CookieKey Key,
        string Name,
        string Value,
        string Domain,
        bool HostOnly,
        string Path,
        bool Secure,
        DateTimeOffset ExpiresAt,
        bool IsDeletion,
        int PairBytes = 0);

    private sealed record ParseResult(CookieEntry? Cookie, string? Error);
}
