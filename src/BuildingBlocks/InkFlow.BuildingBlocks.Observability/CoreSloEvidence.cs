namespace InkFlow.BuildingBlocks.Observability;

/// <summary>
/// The outcome of evaluating one Core SLO evidence window. A window without complete
/// evidence never evaluates to <see cref="Passed" />.
/// </summary>
public enum CoreSloEvaluationStatus
{
    Passed,
    Failed,
    InsufficientEvidence,
    InvalidEvidence,
}

/// <summary>
/// Aggregated evidence for one Core SLO surface in a bounded time window.
/// <paramref name="P95LatencyMilliseconds" /> is calculated by the telemetry backend from
/// <see cref="DurationSampleCount" /> histogram observations; this building block evaluates,
/// but does not invent, that backend result.
/// </summary>
public sealed record CoreSloSurfaceWindowEvidence(
    long RequestCount,
    long ServerErrorCount,
    long DurationSampleCount,
    double? P95LatencyMilliseconds);

/// <summary>
/// Evidence supplied by an OTLP query, synthetic probe aggregator or controlled test fixture.
/// The evaluator requires all four stable surfaces before it can produce a passing result.
/// </summary>
public sealed record CoreSloWindowEvidence(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string EvidenceSource,
    IReadOnlyDictionary<string, CoreSloSurfaceWindowEvidence> Surfaces);

/// <summary>
/// Auditable result for one surface. Reasons are stable machine-readable codes and never
/// contain request paths, identities, exception text or other high-cardinality values.
/// </summary>
public sealed record CoreSloSurfaceEvaluation(
    string Surface,
    CoreSloEvaluationStatus Status,
    long? RequestCount,
    long? ServerErrorCount,
    long? DurationSampleCount,
    decimal? Availability,
    decimal? ErrorBudgetEvents,
    decimal? RemainingErrorBudgetEvents,
    decimal? ErrorBudgetConsumedRatio,
    double LatencyTargetMilliseconds,
    double? P95LatencyMilliseconds,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Result of applying Core SLO v1 to one explicit evidence window.
/// </summary>
public sealed record CoreSloWindowEvaluation(
    string PolicyVersion,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    string EvidenceSource,
    CoreSloEvaluationStatus Status,
    IReadOnlyList<CoreSloSurfaceEvaluation> Surfaces,
    IReadOnlyList<string> Reasons)
{
    public bool IsPassing => Status == CoreSloEvaluationStatus.Passed;

    public bool HasCompleteEvidence =>
        (Status is CoreSloEvaluationStatus.Passed or CoreSloEvaluationStatus.Failed) &&
        Surfaces.Count == CoreSloPolicy.Surfaces.Count &&
        Surfaces.All(surface => surface.Status is
            CoreSloEvaluationStatus.Passed or CoreSloEvaluationStatus.Failed);
}

/// <summary>
/// Evaluates externally aggregated Core SLO evidence and fails closed when evidence is
/// missing, inconsistent or contains an unknown surface.
/// </summary>
public static class CoreSloEvidenceEvaluator
{
    private const int MaxEvidenceSourceLength = 256;

    public static CoreSloWindowEvaluation Evaluate(CoreSloWindowEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ValidateWindow(evidence);

        var surfaceEvaluations = new List<CoreSloSurfaceEvaluation>(CoreSloPolicy.Surfaces.Count);
        foreach (var surface in CoreSloPolicy.Surfaces)
        {
            if (!evidence.Surfaces.TryGetValue(surface, out var surfaceEvidence) ||
                surfaceEvidence is null)
            {
                surfaceEvaluations.Add(CreateInsufficientSurfaceEvaluation(
                    surface,
                    "surface_evidence_missing"));
                continue;
            }

            surfaceEvaluations.Add(EvaluateSurface(surface, surfaceEvidence));
        }

        var reasons = new List<string>();
        if (evidence.Surfaces.Keys.Any(surface => !CoreSloPolicy.IsKnownSurface(surface)))
        {
            reasons.Add("unknown_surface_present");
        }

        if (surfaceEvaluations.Any(surface =>
                surface.Status == CoreSloEvaluationStatus.InvalidEvidence))
        {
            reasons.Add("invalid_surface_evidence");
        }
        else if (surfaceEvaluations.Any(surface =>
                     surface.Status == CoreSloEvaluationStatus.InsufficientEvidence))
        {
            reasons.Add("incomplete_surface_evidence");
        }

        if (surfaceEvaluations.Any(surface => surface.Status == CoreSloEvaluationStatus.Failed))
        {
            reasons.Add("surface_slo_failed");
        }

        var status = reasons.Contains("unknown_surface_present", StringComparer.Ordinal) ||
                     reasons.Contains("invalid_surface_evidence", StringComparer.Ordinal)
            ? CoreSloEvaluationStatus.InvalidEvidence
            : reasons.Contains("incomplete_surface_evidence", StringComparer.Ordinal)
                ? CoreSloEvaluationStatus.InsufficientEvidence
                : reasons.Contains("surface_slo_failed", StringComparer.Ordinal)
                    ? CoreSloEvaluationStatus.Failed
                    : CoreSloEvaluationStatus.Passed;

        return new CoreSloWindowEvaluation(
            CoreSloPolicy.Version,
            evidence.WindowStart,
            evidence.WindowEnd,
            evidence.EvidenceSource,
            status,
            surfaceEvaluations,
            reasons);
    }

    private static CoreSloSurfaceEvaluation EvaluateSurface(
        string surface,
        CoreSloSurfaceWindowEvidence evidence)
    {
        var target = CoreSloPolicy.LatencyP95TargetMilliseconds(surface);
        var invalidReasons = new List<string>();

        if (evidence.RequestCount < 0)
        {
            invalidReasons.Add("request_count_negative");
        }

        if (evidence.ServerErrorCount < 0 || evidence.ServerErrorCount > evidence.RequestCount)
        {
            invalidReasons.Add("server_error_count_invalid");
        }

        if (evidence.DurationSampleCount < 0)
        {
            invalidReasons.Add("duration_sample_count_negative");
        }

        if (evidence.P95LatencyMilliseconds is { } p95 &&
            (double.IsNaN(p95) || double.IsInfinity(p95) || p95 < 0))
        {
            invalidReasons.Add("latency_p95_invalid");
        }

        if (invalidReasons.Count > 0)
        {
            return new CoreSloSurfaceEvaluation(
                surface,
                CoreSloEvaluationStatus.InvalidEvidence,
                evidence.RequestCount,
                evidence.ServerErrorCount,
                evidence.DurationSampleCount,
                null,
                null,
                null,
                null,
                target,
                evidence.P95LatencyMilliseconds,
                invalidReasons);
        }

        if (evidence.RequestCount == 0)
        {
            var noRequestReasons = new List<string> { "no_requests" };
            if (evidence.DurationSampleCount != 0)
            {
                noRequestReasons.Add("duration_sample_count_mismatch");
            }

            return new CoreSloSurfaceEvaluation(
                surface,
                CoreSloEvaluationStatus.InsufficientEvidence,
                evidence.RequestCount,
                evidence.ServerErrorCount,
                evidence.DurationSampleCount,
                null,
                0m,
                0m,
                null,
                target,
                evidence.P95LatencyMilliseconds,
                noRequestReasons);
        }

        var availability = (decimal)(evidence.RequestCount - evidence.ServerErrorCount) /
                           evidence.RequestCount;
        var errorBudgetEvents = evidence.RequestCount *
                                (1m - CoreSloPolicy.AvailabilityTarget);
        var remainingErrorBudgetEvents = errorBudgetEvents - evidence.ServerErrorCount;
        var errorBudgetConsumedRatio = evidence.ServerErrorCount / errorBudgetEvents;
        var reasons = new List<string>();

        if (evidence.DurationSampleCount != evidence.RequestCount)
        {
            reasons.Add("duration_sample_count_mismatch");
        }

        if (evidence.P95LatencyMilliseconds is null)
        {
            reasons.Add("latency_p95_missing");
        }

        if (availability < CoreSloPolicy.AvailabilityTarget)
        {
            reasons.Add("availability_below_target");
        }

        if (evidence.P95LatencyMilliseconds is double measuredP95 && measuredP95 > target)
        {
            reasons.Add("latency_p95_above_target");
        }

        var hasInsufficientEvidence = reasons.Contains(
            "duration_sample_count_mismatch",
            StringComparer.Ordinal) || reasons.Contains(
            "latency_p95_missing",
            StringComparer.Ordinal);
        var status = hasInsufficientEvidence
            ? CoreSloEvaluationStatus.InsufficientEvidence
            : reasons.Count > 0
                ? CoreSloEvaluationStatus.Failed
                : CoreSloEvaluationStatus.Passed;

        return new CoreSloSurfaceEvaluation(
            surface,
            status,
            evidence.RequestCount,
            evidence.ServerErrorCount,
            evidence.DurationSampleCount,
            availability,
            errorBudgetEvents,
            remainingErrorBudgetEvents,
            errorBudgetConsumedRatio,
            target,
            evidence.P95LatencyMilliseconds,
            reasons);
    }

    private static CoreSloSurfaceEvaluation CreateInsufficientSurfaceEvaluation(
        string surface,
        string reason) =>
        new(
            surface,
            CoreSloEvaluationStatus.InsufficientEvidence,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            CoreSloPolicy.LatencyP95TargetMilliseconds(surface),
            null,
            [reason]);

    private static void ValidateWindow(CoreSloWindowEvidence evidence)
    {
        if (evidence.WindowEnd <= evidence.WindowStart)
        {
            throw new ArgumentException(
                "Core SLO evidence window must have a positive duration.",
                nameof(evidence));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(evidence.EvidenceSource);
        if (evidence.EvidenceSource.Length > MaxEvidenceSourceLength ||
            evidence.EvidenceSource.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Core SLO evidence source is invalid.",
                nameof(evidence));
        }

        ArgumentNullException.ThrowIfNull(evidence.Surfaces);
    }
}
