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
