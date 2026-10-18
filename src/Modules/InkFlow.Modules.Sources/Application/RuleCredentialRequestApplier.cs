namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Projects an optional resolved credential onto an already-built rule request.
/// </summary>
internal static class RuleCredentialRequestApplier
{
    public static bool TryApply(
        SourceHttpRequest request,
        SourceCredential? credential,
        out SourceHttpRequest? preparedRequest,
        out string error)
    {
        preparedRequest = request;
        error = string.Empty;
        if (credential is null)
        {
            return true;
        }

        if (!credential.TryBuildHeader(out var headerName, out var headerValue))
        {
            error = "credential: credential material is invalid.";
            return false;
        }

        if (request.Headers.Keys.Any(existingHeaderName =>
                string.Equals(existingHeaderName, headerName, StringComparison.OrdinalIgnoreCase)))
        {
            error = "credential: credential header conflicts with rule header.";
            return false;
        }

        var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase)
        {
            [headerName] = headerValue,
        };
        preparedRequest = request with { Headers = headers };
        return true;
    }
}
