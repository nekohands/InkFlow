using System.Net;
using System.Text.Json;
using InkFlow.Modules.Billing.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static InkFlow.UnitTests.DeveloperEndpointFixture;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class DeveloperEndpointBaselineTests
{
    private const string Catalog = "/api/developer/v1";

    [TestMethod]
    [DataRow(null, null, 401)]
    [DataRow("reader", null, 401)]
    [DataRow(null, "invalid", 401)]
    [DataRow(null, "scope-denied", 403)]
    [DataRow(null, "empty-id", 401)]
    public async Task Catalog_authentication_precedes_validation_and_quota(string? bearer, string? key, int status)
    {
        await using var f = await StartAsync();
        var response = await f.SendAsync("GET", Catalog + "/books?limit=0&apiKey=valid", bearer, key);
        Assert.AreEqual(status, (int)response.StatusCode);
        Assert.IsEmpty(f.Reservations);
        Assert.IsFalse(f.Calls.Any(call => call.StartsWith("read-")));
        var requestAudit = f.Audits.Single().Event;
        Assert.AreEqual("GET", requestAudit.Action);
        Assert.AreEqual(Catalog + "/books", requestAudit.Resource);
        Assert.AreEqual(status, requestAudit.StatusCode);
    }

    [TestMethod]
    [DataRow("/books?limit=0")]
    [DataRow("/books?limit=101")]
    [DataRow("/books?limit=invalid")]
    [DataRow("/search?limit=-1")]
    [DataRow("/search?limit=101")]
    public async Task Invalid_parameters_do_not_reserve_or_read(string route)
    {
        await using var f = await StartAsync();
        var response = await f.SendAsync("GET", Catalog + route, key: "valid");
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsEmpty(f.Reservations);
        Assert.IsNull(response.Headers.CacheControl);
        Assert.IsFalse(f.Calls.Any(call => call.StartsWith("read-")));
    }

    [TestMethod]
    public async Task Query_length_boundary_and_default_limit_are_preserved()
    {
        await using var f = await StartAsync();
        var invalid = await f.SendAsync("GET", Catalog + "/search?q=" + new string('x', 257), key: "valid");
        await AssertError(invalid, 400, "invalid_request");
        Assert.IsEmpty(f.Reservations);
        var valid = await f.SendAsync("GET", Catalog + "/search?q=" + new string('x', 256), key: "valid");
        Assert.AreEqual(HttpStatusCode.OK, valid.StatusCode);
        Assert.AreEqual(20, f.LastLimit);
        Assert.AreEqual(1, f.Reservations.Count);
    }

    [TestMethod]
    [DataRow("/search?q=Stored&limit=1", "catalog.search", 1)]
    [DataRow("/books?limit=100", "catalog.list_books", 1)]
    [DataRow("/books/{book}", "catalog.get_book", 1)]
    [DataRow("/books/{book}/chapters", "catalog.list_chapters", 1)]
    [DataRow("/chapters/{chapter}/content", "catalog.get_content", 5)]
    public async Task Successful_reads_reserve_once_before_catalog_and_preserve_payload(string route, string operation, int units)
    {
        await using var f = await StartAsync();
        var response = await f.SendAsync("GET", Catalog + Expand(route), key: "valid");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("no-store, private", response.Headers.CacheControl?.ToString());
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        var summary = $$"""{"bookId":"{{BookId}}","title":"Stored book","author":"Author","chapterCount":1}""";
        var expected = operation switch
        {
            "catalog.search" or "catalog.list_books" => "[" + summary + "]",
            "catalog.get_book" => summary,
            "catalog.list_chapters" => $$"""[{"chapterId":"{{ChapterId}}","index":0,"title":"Chapter"}]""",
            _ => $$"""{"chapterId":"{{ChapterId}}","bookId":"{{BookId}}","index":0,"title":"Chapter","paragraphs":["First","Second"]}""",
        };
        Assert.AreEqual(expected, body);
        Assert.AreEqual(1, f.Reservations.Count);
        Assert.AreEqual(new QuotaReservationRequest(UserId, DeveloperEndpointFixture.ApplicationId, KeyId, operation, units, f.LastTrace), f.Reservations.Single());
        Assert.IsTrue(f.Calls.IndexOf("authenticate") < f.Calls.IndexOf("reserve"));
        Assert.IsTrue(f.Calls.IndexOf("reserve") < f.Calls.FindIndex(call => call.StartsWith("read-")));
        Assert.AreEqual(f.LastTrace, f.Audits.Single().Event.TraceId);
    }

    [TestMethod]
    [DataRow("/books/{book}", "book_not_found", 1)]
    [DataRow("/books/{book}/chapters", "book_not_found", 1)]
    [DataRow("/chapters/{chapter}/content", "chapter_not_found", 5)]
    public async Task Missing_resources_are_charged_and_keep_current_cache_header_behavior(string route, string error, int units)
    {
        await using var f = await StartAsync();
        f.Missing = true;
        var response = await f.SendAsync("GET", Catalog + Expand(route), key: "valid");
        await AssertError(response, 404, error);
        Assert.AreEqual((long)units, f.Reservations.Single().Units);
        Assert.IsNull(response.Headers.CacheControl);
    }

    [TestMethod]
    [DataRow(QuotaReservationStatus.Forbidden, 403, "developer_api_forbidden")]
    [DataRow(QuotaReservationStatus.InvalidRequest, 400, "invalid_request")]
    [DataRow(QuotaReservationStatus.Unavailable, 503, "quota_unavailable")]
    [DataRow((QuotaReservationStatus)99, 503, "quota_unavailable")]
    public async Task Quota_failures_stop_reads_and_keep_error_contract(QuotaReservationStatus status, int code, string error)
    {
        await using var f = await StartAsync();
        f.QuotaStatus = status;
        var response = await f.SendAsync("GET", Catalog + "/books", key: "valid");
        await AssertError(response, code, error);
        Assert.AreEqual("no-store, private", response.Headers.CacheControl?.ToString());
        Assert.AreEqual(1, f.Reservations.Count);
        Assert.IsFalse(f.Calls.Any(call => call.StartsWith("read-")));
    }

    [TestMethod]
    [DataRow(false, "2")]
    [DataRow(true, "60")]
    public async Task Quota_overage_preserves_UTC_period_ceiling_and_fallback(bool missingSnapshot, string retry)
    {
        await using var f = await StartAsync();
        f.QuotaStatus = QuotaReservationStatus.Exceeded;
        f.MissingSnapshot = missingSnapshot;
        var response = await f.SendAsync("GET", Catalog + "/books", key: "valid");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.AreEqual(retry, response.Headers.GetValues("Retry-After").Single());
        Assert.AreEqual("no-store, private", response.Headers.CacheControl?.ToString());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("quota_exceeded", body.RootElement.GetProperty("error").GetString());
        Assert.AreEqual(missingSnapshot ? Now.AddMinutes(1) : f.Snapshot.PeriodEnd, body.RootElement.GetProperty("periodEnd").GetDateTimeOffset());
        Assert.AreEqual(missingSnapshot ? 0 : 3, body.RootElement.GetProperty("remainingUnits").GetInt64());
        Assert.IsFalse(f.Calls.Any(call => call.StartsWith("read-")));
    }

    [TestMethod]
    public async Task Store_exception_is_sanitized_and_trace_falls_back_without_an_activity()
    {
        await using var f = await StartAsync();
        f.QuotaFailure = new InvalidOperationException("secret-storage-details");
        f.UseActivity = false;
        var response = await f.SendAsync("GET", Catalog + "/books", key: "valid");
        await AssertError(response, 503, "quota_unavailable");
        Assert.AreEqual("rf05-trace", f.Reservations.Single().TraceId);
        Assert.IsFalse(f.Calls.Any(call => call.StartsWith("read-")));
    }

    [TestMethod]
    public async Task Rate_limit_rejection_is_audited_without_business_quota()
    {
        await using var f = await StartAsync();
        f.RateLimited = true;
        var response = await f.SendAsync("GET", Catalog + "/books", key: "valid");
        Assert.AreEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.AreEqual("7", response.Headers.GetValues("Retry-After").Single());
        Assert.IsEmpty(f.Reservations);
        Assert.AreEqual(429, f.Audits.Single().Event.StatusCode);
        Assert.AreEqual(UserId.ToString(), f.Audits.Single().Event.ActorId);
    }

    private static string Expand(string route) => route.Replace("{book}", BookId.ToString()).Replace("{chapter}", ChapterId.ToString());
    private static async Task AssertError(HttpResponseMessage response, int status, string error)
    {
        Assert.AreEqual(status, (int)response.StatusCode);
        Assert.AreEqual($$"""{"error":"{{error}}"}""", await response.Content.ReadAsStringAsync());
    }
}
