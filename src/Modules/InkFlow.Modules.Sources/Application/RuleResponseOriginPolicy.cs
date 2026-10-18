namespace InkFlow.Modules.Sources.Application;

/// <summary>Resolves and validates the effective URI for one rule response.</summary>
internal static class RuleResponseOriginPolicy
{
    public static bool TryResolve(
        SourceHttpRequest request,
        SourceHttpResponse response,
        Uri sourceOrigin,
        string requestContext,
        out Uri? responseUri,
        out string error)
    {
        responseUri = null;
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUri))
        {
            error = $"{requestContext}: request URI is invalid.";
            return false;
        }

        var candidate = response.ResponseUri ?? requestUri;
        if (!candidate.IsAbsoluteUri ||
            candidate.Fragment.Length > 0 ||
            candidate.UserInfo.Length > 0 ||
            !RuleRequestUrlPolicy.IsSameOrigin(sourceOrigin, candidate))
        {
            error = $"{requestContext}: response origin changed during redirect.";
            return false;
        }

        responseUri = candidate;
        error = string.Empty;
        return true;
    }
}
