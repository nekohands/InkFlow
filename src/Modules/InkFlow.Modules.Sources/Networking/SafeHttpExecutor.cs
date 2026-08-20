using System.Net;
using System.Text;
using InkFlow.Modules.Sources.Rules;

namespace InkFlow.Modules.Sources.Networking;

public enum SourceNetworkErrorKind
{
    BlockedTarget,
    Timeout,
    ResponseTooLarge,
    TooManyRedirects,
    Network
}

public sealed class SourceNetworkException(
    SourceNetworkErrorKind kind,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public SourceNetworkErrorKind Kind { get; } = kind;
}

public sealed record SafeHttpResponse(
    HttpStatusCode StatusCode,
    Uri FinalUri,
    string Content,
    long ByteLength,
    string? MediaType);

public sealed class SafeHttpExecutor : IDisposable
{
    private readonly SafeEndpointValidator _validator;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public SafeHttpExecutor(SafeEndpointValidator validator, HttpClient? httpClient = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _httpClient = httpClient ?? SafeHttpClientFactory.Create(validator);
        _ownsClient = httpClient is null;
    }

    public async Task<SafeHttpResponse> ExecuteAsync(
        CompiledSourceRequest request,
        RuleExecutionBudget budget,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(budget);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(budget.MaxExecutionTimeMs));

        try
        {
            var currentUri = request.Uri;
            var method = new HttpMethod(request.Method);
            var redirects = 0;

            while (true)
            {
                await ValidateTargetAsync(currentUri, timeoutCts.Token).ConfigureAwait(false);

                using var message = CreateRequest(request, currentUri, method);
                using var response = await _httpClient
                    .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                    .ConfigureAwait(false);

                if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
                {
                    if (redirects >= budget.MaxRedirects)
                    {
                        throw new SourceNetworkException(
                            SourceNetworkErrorKind.TooManyRedirects,
                            $"Source request exceeded MaxRedirects {budget.MaxRedirects}.");
                    }

                    currentUri = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(currentUri, response.Headers.Location);
                    await ValidateTargetAsync(currentUri, timeoutCts.Token).ConfigureAwait(false);
                    redirects++;

                    if (response.StatusCode is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.SeeOther)
                    {
                        method = HttpMethod.Get;
                    }

                    continue;
                }

                var body = await ReadBoundedBodyAsync(response, budget.MaxBytes, timeoutCts.Token).ConfigureAwait(false);
                return new SafeHttpResponse(
                    response.StatusCode,
                    currentUri,
                    body.Text,
                    body.ByteLength,
                    response.Content.Headers.ContentType?.MediaType);
            }
        }
        catch (UnsafeEndpointException exception)
        {
            throw new SourceNetworkException(SourceNetworkErrorKind.BlockedTarget, exception.Message, exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceNetworkException(SourceNetworkErrorKind.Timeout, "Source request exceeded its execution-time budget.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new SourceNetworkException(SourceNetworkErrorKind.Network, exception.Message, exception);
        }
    }

    private async Task ValidateTargetAsync(Uri uri, CancellationToken cancellationToken) =>
        _ = await _validator.ValidateAsync(uri, cancellationToken).ConfigureAwait(false);

    private static HttpRequestMessage CreateRequest(CompiledSourceRequest request, Uri uri, HttpMethod method)
    {
        var message = new HttpRequestMessage(method, uri);
        foreach (var (name, value) in request.Headers)
        {
            _ = message.Headers.TryAddWithoutValidation(name, value);
        }

        if (method == HttpMethod.Post && request.Form.Count > 0)
        {
            message.Content = new FormUrlEncodedContent(request.Form);
        }

        return message;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MultipleChoices or
        HttpStatusCode.Moved or
        HttpStatusCode.Redirect or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private static async Task<(string Text, long ByteLength)> ReadBoundedBodyAsync(
        HttpResponseMessage response,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > maxBytes)
        {
            throw new SourceNetworkException(
                SourceNetworkErrorKind.ResponseTooLarge,
                $"Response Content-Length {declaredLength} exceeds MaxBytes {maxBytes}.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var destination = new MemoryStream();
        var buffer = new byte[16 * 1024];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new SourceNetworkException(
                    SourceNetworkErrorKind.ResponseTooLarge,
                    $"Response exceeded MaxBytes {maxBytes} while streaming.");
            }

            destination.Write(buffer, 0, read);
        }

        var encoding = ResolveEncoding(response.Content.Headers.ContentType?.CharSet);
        return (encoding.GetString(destination.GetBuffer(), 0, checked((int)destination.Length)), total);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"', '\''));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
