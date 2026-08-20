namespace InkFlow.Modules.Sources.Rules;

public sealed class RuleAdapter
{
    private readonly SourceRuleValidator _validator;
    private readonly SourceRequestCompiler _requestCompiler;
    private readonly RuleDocumentExtractor _extractor;

    public RuleAdapter(
        SourceRuleValidator? validator = null,
        SourceRequestCompiler? requestCompiler = null,
        RuleDocumentExtractor? extractor = null)
    {
        _validator = validator ?? new SourceRuleValidator();
        _requestCompiler = requestCompiler ?? new SourceRequestCompiler();
        _extractor = extractor ?? new RuleDocumentExtractor();
    }

    public CompiledSourceRequest BuildRequest(
        SourceRuleDocument rule,
        SourceOperation operation,
        IReadOnlyDictionary<string, string> variables)
    {
        EnsureValid(rule);
        return _requestCompiler.Compile(rule, operation, variables);
    }

    public RuleExtractionResult ParseResponse(
        SourceRuleDocument rule,
        SourceOperation operation,
        string content)
    {
        EnsureValid(rule);
        var operationRule = rule.GetOperation(operation)
            ?? throw new InvalidOperationException($"Source operation {operation} is not configured.");
        return _extractor.Extract(operationRule, content, rule.Budget);
    }

    private void EnsureValid(SourceRuleDocument rule)
    {
        var errors = _validator.Validate(rule);
        if (errors.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Source rule is invalid: {string.Join("; ", errors.Select(error => $"{error.Path}: {error.Message}"))}");
    }
}
