using System.Text;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed record RuleHttpExecutionResult(
    SourceHttpResponse? Response,
    string? Error);

/// <summary>
/// Executes one request within a rule's shared byte budget and same-origin session.
/// Request counting and pagination decisions remain in the orchestration layer.
/// </summary>
internal sealed class RuleHttpExecutor
{
    private readonly ISourceHttpClient _httpClient;
    private readonly SourceRuleExecutionLimits _limits;
    private readonly Uri _sourceOrigin;
    private readonly RuleCookieJar? _cookieJar;
    private readonly CancellationToken _callerCancellationToken;
    private readonly CancellationToken _executionCancellationToken;
    private long _responseBytes;

    public RuleHttpExecutor(
        ISourceHttpClient httpClient,
        SourceRuleExecutionLimits limits,
        Uri sourceOrigin,
        RuleCookieJar? cookieJar,
        CancellationToken callerCancellationToken,
        CancellationToken executionCancellationToken)
    {
        _httpClient = httpClient;
        _limits = limits;
        _sourceOrigin = sourceOrigin;
        _cookieJar = cookieJar;
        _callerCancellationToken = callerCancellationToken;
        _executionCancellationToken = executionCancellationToken;
    }

    public async Task<RuleHttpExecutionResult> SendAsync(
        SourceHttpRequest request,
        string requestContext)
    {
        if (request.FormBody is not null &&
            Encoding.UTF8.GetByteCount(request.FormBody) > _limits.MaxBytes)
        {
            return new(null, "execution: request exceeded byte budget.");
        }

        SourceHttpResponse response;
        try
        {
            response = await _httpClient
                .SendAsync(AttachCookies(request), _executionCancellationToken)
                .WaitAsync(_executionCancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_callerCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (_executionCancellationToken.IsCancellationRequested)
        {
            return new(null, "execution: time budget exceeded.");
        }
        catch (SourceResponseTooLargeException)
        {
            return new(null, "execution: response exceeded byte budget.");
        }
        catch
        {
            return new(null, "http: transport failure.");
        }

        if (!response.IsSuccess)
        {
            return new(null, $"http: upstream returned status {response.StatusCode}.");
        }

        if (!TryValidateResponseOrigin(request, response, requestContext, out var originError))
        {
            return new(null, originError);
        }

        _responseBytes += Encoding.UTF8.GetByteCount(response.Body);
        if (_responseBytes > _limits.MaxBytes)
        {
            return new(null, "execution: response exceeded byte budget.");
        }

        if (!TryAcceptResponseCookies(request, response, out var sessionError))
        {
            return new(null, sessionError);
        }

        return new(response, null);
    }

    private SourceHttpRequest AttachCookies(SourceHttpRequest request)
    {
        if (_cookieJar is null ||
            !Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUri))
        {
            return request;
        }

        return request with
        {
            CookieHeader = _cookieJar.BuildCookieHeader(requestUri),
        };
    }

    private bool TryValidateResponseOrigin(
        SourceHttpRequest request,
        SourceHttpResponse response,
        string requestContext,
        out string error)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUri))
        {
            error = $"{requestContext}: request URI is invalid.";
            return false;
        }

        var responseUri = response.ResponseUri ?? requestUri;
        if (!responseUri.IsAbsoluteUri ||
            responseUri.Fragment.Length > 0 ||
            responseUri.UserInfo.Length > 0 ||
            !RuleRequestBuilder.IsSameOrigin(_sourceOrigin, responseUri))
        {
            error = $"{requestContext}: response origin changed during redirect.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryAcceptResponseCookies(
        SourceHttpRequest request,
        SourceHttpResponse response,
        out string error)
    {
        if (_cookieJar is null)
        {
            error = string.Empty;
            return true;
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUri))
        {
            error = "session: request URI is invalid.";
            return false;
        }

        var responseUri = response.ResponseUri ?? requestUri;
        if (!responseUri.IsAbsoluteUri || !RuleRequestBuilder.IsSameOrigin(_sourceOrigin, responseUri))
        {
            error = "session: response origin changed during redirect.";
            return false;
        }

        var sessionError = _cookieJar.Accept(response.SetCookieHeaders, responseUri);
        if (sessionError is not null)
        {
            error = sessionError;
            return false;
        }

        error = string.Empty;
        return true;
    }
}
