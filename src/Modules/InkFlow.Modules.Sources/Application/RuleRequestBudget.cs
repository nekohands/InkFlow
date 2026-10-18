namespace InkFlow.Modules.Sources.Application;

internal sealed class RuleRequestBudget(int maxRequests)
{
    public const string ExceededError = "execution: request budget exceeded.";

    private readonly int _maxRequests = maxRequests;
    private int _requestsUsed;

    public bool HasCapacity => _requestsUsed < _maxRequests;

    public bool TryConsume(out string error)
    {
        if (!HasCapacity)
        {
            error = ExceededError;
            return false;
        }

        _requestsUsed++;
        error = string.Empty;
        return true;
    }
}
