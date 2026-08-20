using InkFlow.Modules.Sources.Networking;

namespace InkFlow.Modules.Sources.Rules;

public sealed record SourceOperationExecution(
    int StatusCode,
    Uri FinalUri,
    string RawContent,
    long ByteLength,
    RuleExtractionResult Extraction);

public sealed class RuleOperationExecutor
{
    public async Task<SourceOperationExecution> ExecuteAsync(
        SourceRuleDocument rule,
        SourceOperation operation,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default)
    {
        var adapter = new RuleAdapter();
        var request = adapter.BuildRequest(rule, operation, variables);
        using var transport = new SafeHttpExecutor(new SafeEndpointValidator());
        var response = await transport.ExecuteAsync(request, rule.Budget, cancellationToken).ConfigureAwait(false);
        var statusCode = (int)response.StatusCode;
        if (statusCode >= 400)
        {
            throw new HttpRequestException($"Source returned HTTP {statusCode} for {response.FinalUri}.");
        }

        return new(statusCode, response.FinalUri, response.Content, response.ByteLength, adapter.ParseResponse(rule, operation, response.Content));
    }
}
