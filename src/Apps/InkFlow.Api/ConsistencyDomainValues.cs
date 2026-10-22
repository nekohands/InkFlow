using System.Collections.Frozen;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Api;

/// <summary>Derive validity from owning domains, never duplicate their numeric enum definitions.</summary>
public static class ConsistencyDomainValues
{
    public static ConsistencyValueDefinitions Current { get; } = new(
        Enum.GetValues<SourceCapability>().Select(value => (int)value).ToFrozenSet(),
        Enum.GetValues<SourceHealthStatus>().Select(value => (int)value).ToFrozenSet(),
        Enum.GetValues<MatchCandidateStatus>().Select(value => (int)value).ToFrozenSet(),
        Enum.GetValues<CrawlerTaskStatus>().Select(value => (int)value).ToFrozenSet(),
        (int)CrawlerTaskStatus.DeadLettered);
}
