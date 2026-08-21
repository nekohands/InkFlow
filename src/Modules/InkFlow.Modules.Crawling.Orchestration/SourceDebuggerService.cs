using System.Diagnostics;
using System.Text.Json;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Sources.Rules;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Orchestration;

public sealed record SourceDebugResult(
    bool Executed,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<RuleValidationError> ValidationErrors,
    int? StatusCode,
    string? FinalUrl,
    long? ByteLength,
    long ElapsedMilliseconds,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
    string? RawPreview);

public sealed class SourceDebuggerService(SourcesDbContext sources)
{
    public async Task<SourceDebugResult> DebugAsync(
        Guid sourceId,
        string operationName,
        string ruleJson,
        IReadOnlyDictionary<string, string>? variables,
        CancellationToken cancellationToken = default)
    {
        var source = await sources.Sources.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sourceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Source {sourceId} was not found.");

        if (!Enum.TryParse<SourceOperation>(operationName, true, out var operation))
        {
            return Invalid([new RuleValidationError(
                "RULE_DEBUG_OPERATION_INVALID",
                "operation",
                "Operation must be Search, BookInfo, Toc, Content, or Update.")]);
        }

        SourceRuleDocument rule;
        try
        {
            rule = SourceRuleJson.Deserialize(ruleJson);
        }
        catch (JsonException exception)
        {
            return Invalid([new RuleValidationError("RULE_JSON_INVALID", "$", exception.Message)]);
        }

        var errors = new SourceRuleValidator().Validate(rule).ToList();
        if (TryHttpUri(source.BaseUrl, out var sourceBase)
            && TryHttpUri(rule.BaseUrl, out var ruleBase)
            && !SameOrigin(sourceBase, ruleBase))
        {
            errors.Add(new RuleValidationError(
                "RULE_BASE_URL_SOURCE_MISMATCH",
                "baseUrl",
                "Rule baseUrl must use the same origin as its source."));
        }
        if (rule.GetOperation(operation) is null)
        {
            errors.Add(new RuleValidationError(
                "RULE_DEBUG_OPERATION_UNAVAILABLE",
                "operation",
                $"Rule does not define operation {operation}."));
        }
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var execution = await new RuleOperationExecutor().ExecuteAsync(
                rule,
                operation,
                variables ?? new Dictionary<string, string>(),
                cancellationToken);
            var elapsed = (long)Math.Ceiling(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            var rows = execution.Extraction.Rows
                .Select(row => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase))
                .ToList();
            var preview = execution.RawContent.Length <= 4096
                ? execution.RawContent
                : execution.RawContent[..4096];
            return new(
                true, null, null, [], execution.StatusCode, execution.FinalUri.AbsoluteUri,
                execution.ByteLength, elapsed, rows, preview);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var elapsed = (long)Math.Ceiling(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return new(
                false,
                "RULE_DEBUG_EXECUTION_FAILED",
                exception.Message,
                [],
                null,
                null,
                null,
                elapsed,
                [],
                null);
        }
    }

    private static SourceDebugResult Invalid(IReadOnlyList<RuleValidationError> errors) => new(
        false, "RULE_DEBUG_INVALID", "Rule cannot be executed.", errors,
        null, null, null, 0, [], null);

    private static bool TryHttpUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }
        uri = null!;
        return false;
    }

    private static bool SameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;
}
