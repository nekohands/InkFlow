using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Owns URL trust-boundary checks shared by initial, paginated and redirected
/// rule requests. Request rendering remains with <see cref="RuleRequestBuilder"/>.
/// </summary>
internal static class RuleRequestUrlPolicy
{
    private const int MaxPaginationLinkLength = 2_048;

    public static bool TryValidateInitialRequest(
        SourceHttpRequest request,
        Uri sourceOrigin,
        out string error)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target))
        {
            error = "request: built URL is not a permitted URL.";
            return false;
        }

        return TryValidateSameOriginUrl(
            target,
            sourceOrigin,
            "request: built URL is not a permitted URL.",
            "request: built URL must stay on the source origin.",
            out error);
    }

    public static bool TryValidateContinuationRequest(
        SourceHttpRequest request,
        Uri sourceOrigin,
        out string error)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target))
        {
            error = "pagination: generated continuation request is not a permitted URL.";
            return false;
        }

        return TryValidateSameOriginUrl(
            target,
            sourceOrigin,
            "pagination: generated continuation request is not a permitted URL.",
            "pagination: generated continuation request must stay on the source origin.",
            out error);
    }

    public static bool TryBuildNextRequest(
        SourceHttpRequest currentRequest,
        string rawNextLink,
        Uri sourceOrigin,
        out SourceHttpRequest? nextRequest,
        out string error)
    {
        nextRequest = null;
        error = string.Empty;
        var value = rawNextLink.Trim();

        if (value.Length == 0)
        {
            error = "pagination: next link must not be empty.";
            return false;
        }

        if (value.Length > MaxPaginationLinkLength || value.Any(char.IsControl))
        {
            error = "pagination: next link is invalid or too long.";
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var parsedLink))
        {
            error = "pagination: next link is not a valid URL.";
            return false;
        }

        Uri? target;
        if (parsedLink.IsAbsoluteUri)
        {
            target = parsedLink;
        }
        else if (!Uri.TryCreate(new Uri(currentRequest.Url), parsedLink, out target))
        {
            error = "pagination: next link is not a valid URL.";
            return false;
        }

        if (!TryValidateSameOriginUrl(
                target,
                sourceOrigin,
                "pagination: next link is not a permitted URL.",
                "pagination: next link must stay on the source origin.",
                out error))
        {
            return false;
        }

        nextRequest = currentRequest with
        {
            Method = RuleHttpMethod.Get,
            Url = target.AbsoluteUri,
            FormBody = null,
        };
        return true;
    }

    internal static bool IsSameOrigin(Uri expected, Uri actual) =>
        string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase) &&
        expected.Port == actual.Port;

    private static bool TryValidateSameOriginUrl(
        Uri? target,
        Uri sourceOrigin,
        string invalidUrlError,
        string crossOriginError,
        out string error)
    {
        if (target is null ||
            !target.IsAbsoluteUri ||
            target.Fragment.Length > 0 ||
            target.UserInfo.Length > 0)
        {
            error = invalidUrlError;
            return false;
        }

        var ssrfErrors = SsrfGuard.InspectLiteral(target);
        if (ssrfErrors.Count > 0)
        {
            error = $"ssrf: {string.Join("; ", ssrfErrors)}";
            return false;
        }

        if (!IsSameOrigin(sourceOrigin, target))
        {
            error = crossOriginError;
            return false;
        }

        error = string.Empty;
        return true;
    }
}
