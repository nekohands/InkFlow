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
    private readonly RuleSession _policy;
    private readonly Dictionary<RuleCookieKey, RuleCookie> _cookies = [];
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

            var parsed = RuleCookieParser.Parse(header, responseUri, _policy);
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

    private bool Matches(RuleCookie cookie, Uri requestUri)
    {
        if (cookie.Secure && !requestUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = requestUri.Host;
        if (cookie.HostOnly
            ? !host.Equals(cookie.Domain, StringComparison.OrdinalIgnoreCase)
            : !RuleCookieParser.HostMatchesDomain(host, cookie.Domain))
        {
            return false;
        }

        var requestPath = string.IsNullOrEmpty(requestUri.AbsolutePath) ? "/" : requestUri.AbsolutePath;
        return requestPath.Equals(cookie.Path, StringComparison.Ordinal) ||
            (requestPath.StartsWith(cookie.Path, StringComparison.Ordinal) &&
             (cookie.Path.EndsWith("/", StringComparison.Ordinal) ||
              requestPath.Length > cookie.Path.Length && requestPath[cookie.Path.Length] == '/'));
    }

    private void Remove(RuleCookieKey key)
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

}
