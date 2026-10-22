using System.Collections.Frozen;

namespace InkFlow.Modules.Operations.Application;

/// <summary>Host-projected enum metadata; Operations has no dependency on foreign domain types.</summary>
public sealed record ConsistencyValueDefinitions(
    FrozenSet<int> SourceCapabilities,
    FrozenSet<int> SourceHealthStatuses,
    FrozenSet<int> MatchCandidateStatuses,
    FrozenSet<int> CrawlerTaskStatuses,
    int DeadLetteredTaskStatus);
