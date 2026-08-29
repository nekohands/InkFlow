using System.Diagnostics.Metrics;
using InkFlow.BuildingBlocks.Observability;
using Microsoft.AspNetCore.Http;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CoreSloObservabilityTests
{
    [TestMethod]
    public void Surface_mapping_is_bounded_and_excludes_operational_paths()
    {
        Assert.IsTrue(CoreSloPolicy.TryGetSurface("/api/v1/books/book-1", out var publicSurface));
        Assert.AreEqual(CoreSloPolicy.PublicApiSurface, publicSurface);

        Assert.IsTrue(CoreSloPolicy.TryGetSurface("/api/legado/v1/books/book-1", out var legadoSurface));
        Assert.AreEqual(CoreSloPolicy.LegadoApiSurface, legadoSurface);

        Assert.IsTrue(CoreSloPolicy.TryGetSurface("/api/developer/v1/books", out var developerSurface));
        Assert.AreEqual(CoreSloPolicy.DeveloperApiSurface, developerSurface);

        Assert.IsTrue(CoreSloPolicy.TryGetSurface("/reader/read/chapter-1", out var readerSurface));
        Assert.AreEqual(CoreSloPolicy.ReaderSurface, readerSurface);

        Assert.IsFalse(CoreSloPolicy.TryGetSurface("/api/v10/books", out _));
        Assert.IsFalse(CoreSloPolicy.TryGetSurface("/health", out _));
        Assert.IsFalse(CoreSloPolicy.TryGetSurface("/admin/operations", out _));
    }

    [TestMethod]
    public void Availability_policy_only_counts_server_failures_as_bad_events()
    {
        Assert.IsTrue(CoreSloPolicy.IsGoodAvailabilityStatus(200));
        Assert.IsTrue(CoreSloPolicy.IsGoodAvailabilityStatus(401));
        Assert.IsTrue(CoreSloPolicy.IsGoodAvailabilityStatus(429));
        Assert.IsFalse(CoreSloPolicy.IsGoodAvailabilityStatus(500));
        Assert.IsFalse(CoreSloPolicy.IsGoodAvailabilityStatus(503));

        Assert.AreEqual(750, CoreSloPolicy.LatencyP95TargetMilliseconds(CoreSloPolicy.PublicApiSurface));
        Assert.AreEqual(1_000, CoreSloPolicy.LatencyP95TargetMilliseconds(CoreSloPolicy.LegadoApiSurface));
    }

    [TestMethod]
    public async Task Middleware_records_surface_outcome_duration_and_server_error_without_path_tags()
    {
        var requests = new List<(string Surface, string Outcome)>();
        var errors = new List<string>();
        var durations = new List<double>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == CoreSloMetrics.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var surface = Tag(tags, "inkflow.slo.surface");
            Assert.IsFalse(HasPathTag(tags));
            if (instrument.Name == CoreSloMetrics.RequestsName)
            {
                requests.Add((surface!, Tag(tags, "inkflow.slo.outcome")!));
            }
            else if (instrument.Name == CoreSloMetrics.ServerErrorsName)
            {
                errors.Add(surface!);
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == CoreSloMetrics.RequestDurationName)
            {
                durations.Add(measurement);
                Assert.IsFalse(HasPathTag(tags));
            }
        });
        listener.Start();

        var successContext = new DefaultHttpContext();
        successContext.Request.Path = "/api/v1/books/book-1";
        await new CoreSloMetricsMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(successContext);

        var errorContext = new DefaultHttpContext();
        errorContext.Request.Path = "/api/legado/v1/search";
        var threw = false;
        try
        {
            await new CoreSloMetricsMiddleware(_ =>
                    throw new InvalidOperationException("fixture failure"))
                .InvokeAsync(errorContext);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert.IsTrue(threw);

        CollectionAssert.Contains(requests.Select(request => request.Surface).ToList(), CoreSloPolicy.PublicApiSurface);
        CollectionAssert.Contains(requests.Select(request => request.Surface).ToList(), CoreSloPolicy.LegadoApiSurface);
        CollectionAssert.Contains(requests.Select(request => request.Outcome).ToList(), "good");
        CollectionAssert.Contains(requests.Select(request => request.Outcome).ToList(), "bad");
        CollectionAssert.Contains(errors, CoreSloPolicy.LegadoApiSurface);
        Assert.AreEqual(2, durations.Count);
        Assert.IsTrue(durations.All(duration => duration >= 0));
    }

    [TestMethod]
    public void Window_evaluator_passes_complete_evidence_and_reports_error_budget()
    {
        var evaluation = CoreSloEvidenceEvaluator.Evaluate(
            CompleteEvidence(
                requestCount: 1_000,
                serverErrorCount: 0,
                p95Milliseconds: 750));

        Assert.AreEqual(CoreSloEvaluationStatus.Passed, evaluation.Status);
        Assert.IsTrue(evaluation.IsPassing);
        Assert.IsTrue(evaluation.HasCompleteEvidence);
        Assert.AreEqual(4, evaluation.Surfaces.Count);

        var publicApi = evaluation.Surfaces.Single(
            surface => surface.Surface == CoreSloPolicy.PublicApiSurface);
        Assert.AreEqual(5m, publicApi.ErrorBudgetEvents);
        Assert.AreEqual(5m, publicApi.RemainingErrorBudgetEvents);
        Assert.AreEqual(0m, publicApi.ErrorBudgetConsumedRatio);
    }

    [TestMethod]
    public void Window_evaluator_counts_only_server_errors_and_fails_latency_or_availability()
    {
        var evidence = CompleteEvidence(
            requestCount: 1_000,
            serverErrorCount: 6,
            p95Milliseconds: 751);
        var surfaces = evidence.Surfaces.ToDictionary(pair => pair.Key, pair => pair.Value);
        surfaces[CoreSloPolicy.PublicApiSurface] =
            new CoreSloSurfaceWindowEvidence(1_000, 6, 1_000, 751);
        evidence = evidence with { Surfaces = surfaces };

        var evaluation = CoreSloEvidenceEvaluator.Evaluate(evidence);
        var publicApi = evaluation.Surfaces.Single(
            surface => surface.Surface == CoreSloPolicy.PublicApiSurface);

        Assert.AreEqual(CoreSloEvaluationStatus.Failed, evaluation.Status);
        Assert.AreEqual(CoreSloEvaluationStatus.Failed, publicApi.Status);
        CollectionAssert.Contains(publicApi.Reasons.ToList(), "availability_below_target");
        CollectionAssert.Contains(publicApi.Reasons.ToList(), "latency_p95_above_target");
        Assert.AreEqual(0.994m, publicApi.Availability);
        Assert.AreEqual(-1m, publicApi.RemainingErrorBudgetEvents);
    }

    [TestMethod]
    public void Window_evaluator_fails_closed_for_missing_or_mismatched_latency_evidence()
    {
        var evidence = CompleteEvidence(
            requestCount: 10,
            serverErrorCount: 0,
            p95Milliseconds: null);
        var surfaces = evidence.Surfaces.ToDictionary(pair => pair.Key, pair => pair.Value);
        surfaces[CoreSloPolicy.ReaderSurface] =
            new CoreSloSurfaceWindowEvidence(10, 0, 9, 500);
        evidence = evidence with { Surfaces = surfaces };

        var evaluation = CoreSloEvidenceEvaluator.Evaluate(evidence);
        var publicApi = evaluation.Surfaces.Single(
            surface => surface.Surface == CoreSloPolicy.PublicApiSurface);
        var reader = evaluation.Surfaces.Single(
            surface => surface.Surface == CoreSloPolicy.ReaderSurface);

        Assert.AreEqual(CoreSloEvaluationStatus.InsufficientEvidence, evaluation.Status);
        Assert.AreEqual(CoreSloEvaluationStatus.InsufficientEvidence, publicApi.Status);
        Assert.AreEqual(CoreSloEvaluationStatus.InsufficientEvidence, reader.Status);
        CollectionAssert.Contains(publicApi.Reasons.ToList(), "latency_p95_missing");
        CollectionAssert.Contains(reader.Reasons.ToList(), "duration_sample_count_mismatch");
        Assert.IsFalse(evaluation.IsPassing);
    }

    [TestMethod]
    public void Window_evaluator_rejects_unknown_surfaces_and_invalid_aggregates()
    {
        var evidence = CompleteEvidence(
            requestCount: 10,
            serverErrorCount: 0,
            p95Milliseconds: 100);
        var surfaces = evidence.Surfaces.ToDictionary(pair => pair.Key, pair => pair.Value);
        surfaces["/api/v1/books/book-1"] =
            new CoreSloSurfaceWindowEvidence(10, 0, 10, 100);
        surfaces[CoreSloPolicy.DeveloperApiSurface] =
            new CoreSloSurfaceWindowEvidence(10, 11, 10, 100);
        evidence = evidence with { Surfaces = surfaces };

        var evaluation = CoreSloEvidenceEvaluator.Evaluate(evidence);
        var developerApi = evaluation.Surfaces.Single(
            surface => surface.Surface == CoreSloPolicy.DeveloperApiSurface);

        Assert.AreEqual(CoreSloEvaluationStatus.InvalidEvidence, evaluation.Status);
        Assert.AreEqual(CoreSloEvaluationStatus.InvalidEvidence, developerApi.Status);
        CollectionAssert.Contains(evaluation.Reasons.ToList(), "unknown_surface_present");
        CollectionAssert.Contains(evaluation.Reasons.ToList(), "invalid_surface_evidence");
        CollectionAssert.Contains(developerApi.Reasons.ToList(), "server_error_count_invalid");
        Assert.IsFalse(evaluation.IsPassing);
    }

    private static CoreSloWindowEvidence CompleteEvidence(
        long requestCount,
        long serverErrorCount,
        double? p95Milliseconds)
    {
        var surfaces = CoreSloPolicy.Surfaces.ToDictionary(
            surface => surface,
            surface => new CoreSloSurfaceWindowEvidence(
                requestCount,
                serverErrorCount,
                requestCount,
                p95Milliseconds));

        return new CoreSloWindowEvidence(
            new DateTimeOffset(2026, 8, 29, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 30, 0, 0, 0, TimeSpan.Zero),
            "unit-test-fixture",
            surfaces);
    }

    private static string? Tag(
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        string key)
    {
        foreach (var tag in tags)
        {
            if (string.Equals(tag.Key, key, StringComparison.Ordinal))
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }

    private static bool HasPathTag(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in tags)
        {
            if (tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
