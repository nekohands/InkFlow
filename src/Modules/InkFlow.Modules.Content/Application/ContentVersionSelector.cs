using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

internal static class ContentVersionSelector
{
    public static ContentVersion? SelectBest(IReadOnlyList<ContentVersion> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates[0];
        for (var i = 1; i < candidates.Count; i++)
        {
            best = ContentVersion.SelectCurrent(best, candidates[i]);
        }

        return best;
    }
}
