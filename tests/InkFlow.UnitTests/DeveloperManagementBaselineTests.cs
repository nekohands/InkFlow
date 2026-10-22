using System.Net;
using System.Text.Json;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Developers.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static InkFlow.UnitTests.DeveloperEndpointFixture;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class DeveloperManagementBaselineTests
{
    private const string Applications = "/api/v1/me/developer-applications";
    private static string AppPath => $"{Applications}/{DeveloperEndpointFixture.ApplicationId}";
    private static string KeyPath => $"{AppPath}/keys/{KeyId}";

    [TestMethod]
    [DataRow("GET", "/api/v1/me/developer-applications")]
    [DataRow("POST", "/api/v1/me/developer-applications/")]
    [DataRow("DELETE", "/api/v1/me/developer-applications/{app}")]
    [DataRow("GET", "/api/v1/me/developer-applications/{app}/keys")]
    [DataRow("POST", "/api/v1/me/developer-applications/{app}/keys")]
    [DataRow("DELETE", "/api/v1/me/developer-applications/{app}/keys/{key}")]
    [DataRow("POST", "/api/v1/me/developer-applications/{app}/keys/{key}/rotate")]
    [DataRow("GET", "/api/v1/me/entitlement")]
    [DataRow("GET", "/api/v1/admin/plans")]
    [DataRow("PUT", "/api/v1/admin/users/{user}/entitlement")]
    public async Task Management_requires_Web_bearer_not_Developer_key(string method, string path)
    {
        await using var f = await StartAsync();
        path = path.Replace("{app}", DeveloperEndpointFixture.ApplicationId.ToString()).Replace("{key}", KeyId.ToString()).Replace("{user}", UserId.ToString());
        var response = await f.SendAsync(method, path, key: "valid", body: method is "POST" or "PUT" ? new { } : null);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.IsEmpty(f.Calls);
        Assert.AreEqual(401, f.Audits.Single().Event.StatusCode);
    }

    [TestMethod]
    [DataRow("create", 201, "developer.application.create")]
    [DataRow("issue", 201, "developer.api_key.create")]
    [DataRow("rotate", 200, "developer.api_key.rotate")]
    [DataRow("revoke-app", 204, "developer.application.revoke")]
    [DataRow("revoke-key", 204, "developer.api_key.revoke")]
    public async Task Lifecycle_response_secret_and_command_audit_are_frozen(string operation, int status, string action)
    {
        await using var f = await StartAsync();
        var (method, path) = Route(operation);
        var response = await f.SendAsync(method, path, bearer: "reader", body: method == "POST" ? new { name = "Fixture", expiresInDays = 30 } : null);
        Assert.AreEqual(status, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.AreEqual(operation is "issue" or "rotate", body.Contains(RawKey));
        Assert.IsFalse(body.Contains("secretHash", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(operation switch { "create" => AppPath, "issue" => KeyPath, _ => null }, response.Headers.Location?.ToString());
        if (status == 204) Assert.AreEqual("", body);
        var audit = f.Audits.Single(item => item.Event.Action == action);
        Assert.AreEqual(CancellationToken.None, audit.Token);
        Assert.AreEqual(200, audit.Event.StatusCode);
        Assert.AreEqual("success", audit.Event.Outcome);
        Assert.AreEqual(UserId.ToString(), audit.Event.ActorId);
        Assert.AreEqual("authenticated", audit.Event.ActorType);
        Assert.AreEqual(path.TrimEnd('/'), audit.Event.Resource);
        Assert.AreEqual(f.LastTrace, audit.Event.TraceId);
        Assert.AreEqual(Now, audit.Event.OccurredAt);
        Assert.IsFalse(JsonSerializer.Serialize(audit.Event).Contains(RawKey));
        Assert.AreEqual(operation switch
        {
            "create" => $"application:{DeveloperEndpointFixture.ApplicationId}",
            "issue" => $"application:{DeveloperEndpointFixture.ApplicationId};key:{KeyId}",
            "rotate" => $"application:{DeveloperEndpointFixture.ApplicationId};old-key:{KeyId};new-key:{KeyId}",
            "revoke-app" => $"application:{DeveloperEndpointFixture.ApplicationId};status:Success",
            _ => $"application:{DeveloperEndpointFixture.ApplicationId};key:{KeyId};status:Success"
        }, audit.Event.Reference);
        CollectionAssert.AreEqual(new[] { operation, "command-audit" }, f.Calls.ToArray());
        Assert.AreEqual(status, f.Audits.Single(item => item.Event.Action == method).Event.StatusCode);
    }

    [TestMethod]
    [DataRow(DeveloperOperationStatus.InvalidRequest, 400, "invalid_request")]
    [DataRow(DeveloperOperationStatus.NotFound, 404, "developer_resource_not_found")]
    [DataRow(DeveloperOperationStatus.LimitReached, 409, "developer_limit_reached")]
    [DataRow(DeveloperOperationStatus.Conflict, 409, "developer_operation_conflict")]
    public async Task Lifecycle_errors_are_identical_for_every_command(DeveloperOperationStatus status, int code, string error)
    {
        await using var f = await StartAsync();
        f.CommandStatus = status;
        foreach (var operation in new[] { "create", "issue", "rotate", "revoke-app", "revoke-key" })
        {
            f.Audits.Clear();
            var (method, path) = Route(operation);
            var response = await f.SendAsync(method, path, bearer: "reader", body: method == "POST" ? new { } : null);
            Assert.AreEqual(code, (int)response.StatusCode);
            Assert.AreEqual($$"""{"error":"{{error}}"}""", await response.Content.ReadAsStringAsync());
            var audit = f.Audits.Single(item => item.Event.Action.Contains('.')).Event;
            Assert.AreEqual(code, audit.StatusCode);
            Assert.AreEqual("client_error", audit.Outcome);
            StringAssert.EndsWith(audit.Reference!, "status:" + status);
        }
    }

    [TestMethod]
    public async Task Lists_return_metadata_without_secrets_or_command_audits()
    {
        await using var f = await StartAsync();
        foreach (var path in new[] { Applications, AppPath + "/keys", "/api/v1/admin/plans" })
        {
            var response = await f.SendAsync("GET", path, bearer: "admin");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.IsFalse(body.Contains(RawKey));
            Assert.IsFalse(body.Contains("secretHash", StringComparison.OrdinalIgnoreCase));
            using var json = JsonDocument.Parse(body);
            Assert.AreEqual(1, json.RootElement.GetArrayLength());
        }
        Assert.IsTrue(f.Audits.All(item => item.Event.Action == "GET"));
    }

    [TestMethod]
    [DataRow("reader", 403)]
    [DataRow(null, 401)]
    public async Task Administrator_routes_reject_non_administrators(string? bearer, int status)
    {
        await using var f = await StartAsync();
        foreach (var (method, path) in new[] { ("GET", "/api/v1/admin/plans"), ("PUT", $"/api/v1/admin/users/{UserId}/entitlement") })
        {
            var response = await f.SendAsync(method, path, bearer, body: method == "PUT" ? new { planCode = "free" } : null);
            Assert.AreEqual(status, (int)response.StatusCode);
        }
        Assert.IsEmpty(f.Calls);
    }

    [TestMethod]
    [DataRow(EntitlementOperationStatus.Success, 200, null)]
    [DataRow(EntitlementOperationStatus.PlanNotFound, 404, "plan_not_found")]
    [DataRow(EntitlementOperationStatus.UserNotFound, 404, "user_not_found")]
    [DataRow(EntitlementOperationStatus.ActorNotAllowed, 403, "entitlement_management_forbidden")]
    [DataRow(EntitlementOperationStatus.InvalidRequest, 400, "invalid_request")]
    public async Task Assignment_preserves_status_reason_and_reference(EntitlementOperationStatus status, int code, string? error)
    {
        await using var f = await StartAsync();
        f.AssignmentStatus = status;
        var response = await f.SendAsync("PUT", $"/api/v1/admin/users/{UserId}/entitlement", "admin", body: new { planCode = " free ", reason = " approved\nchange " });
        Assert.AreEqual(code, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        if (error is not null) Assert.AreEqual($$"""{"error":"{{error}}"}""", body);
        else StringAssert.Contains(body, "\"quota\":null");
        var audit = f.Audits.Single(item => item.Event.Action == "billing.entitlement.assign");
        Assert.AreEqual("approved change", audit.Event.Reason);
        Assert.AreEqual($"user:{UserId};plan:free;status:{status}", audit.Event.Reference);
        Assert.AreEqual(code, audit.Event.StatusCode);
        Assert.AreEqual(CancellationToken.None, audit.Token);
        CollectionAssert.AreEqual(new[] { "assign", "command-audit" }, f.Calls.ToArray());
    }

    [TestMethod]
    [DataRow("present", 200)]
    [DataRow("missing", 404)]
    [DataRow("missing-snapshot", 200)]
    [DataRow("unavailable", 503)]
    public async Task Own_entitlement_preserves_lookup_order_nullable_quota_and_errors(string shape, int code)
    {
        await using var f = await StartAsync();
        f.MissingEntitlement = shape == "missing";
        f.MissingSnapshot = shape == "missing-snapshot";
        if (shape == "unavailable") f.QuotaFailure = new InvalidOperationException("private details");
        var response = await f.SendAsync("GET", "/api/v1/me/entitlement", "reader");
        Assert.AreEqual(code, (int)response.StatusCode);
        Assert.IsNull(response.Headers.CacheControl);
        CollectionAssert.AreEqual(shape == "missing" ? new[] { "entitlement" } : new[] { "entitlement", "snapshot" }, f.Calls.ToArray());
        var body = await response.Content.ReadAsStringAsync();
        if (shape == "missing") Assert.AreEqual("{\"error\":\"entitlement_not_found\"}", body);
        if (shape == "unavailable") Assert.AreEqual("{\"error\":\"quota_unavailable\"}", body);
        if (shape == "missing-snapshot") StringAssert.Contains(body, "\"quota\":null");
        if (shape == "present")
        {
            using var json = JsonDocument.Parse(body);
            var quota = json.RootElement.GetProperty("quota");
            Assert.AreEqual(f.Snapshot.PeriodStart, quota.GetProperty("periodStart").GetDateTimeOffset());
            Assert.AreEqual(997L, quota.GetProperty("usedUnits").GetInt64());
            Assert.AreEqual("quota-v1", quota.GetProperty("algorithmVersion").GetString());
        }
    }

    [TestMethod]
    public async Task Ordinary_audit_failure_does_not_replace_completed_command_response()
    {
        await using var f = await StartAsync();
        f.AuditFailure = new InvalidOperationException("audit unavailable");
        var response = await f.SendAsync("POST", AppPath + "/keys", "reader", body: new { });
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), RawKey);
        Assert.AreEqual(201, f.Audits.Single(item => item.Event.Action == "POST").Event.StatusCode);
    }

    [TestMethod]
    public async Task Command_audit_ignores_request_cancellation_but_propagates_sink_cancellation()
    {
        await using var f = await StartAsync();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await f.InvokeAsync(Applications + "/", "POST", cancelled.Token);
        Assert.AreEqual(CancellationToken.None, f.Audits.Single().Token);
        f.AuditFailure = new OperationCanceledException("sink cancellation");
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => f.InvokeAsync(Applications + "/", "POST", CancellationToken.None));
        Assert.AreSame(f.AuditFailure, error);
    }

    [TestMethod]
    [DataRow("/api/developer/v1/books", true)]
    [DataRow("/api/v1/me/entitlement", false)]
    public async Task Quota_cancellation_propagates_only_when_request_is_cancelled(string path, bool noStore)
    {
        await using var f = await StartAsync();
        f.QuotaFailure = new OperationCanceledException("store cancellation");
        var response = await f.InvokeAsync(path, "GET", CancellationToken.None);
        Assert.AreEqual(503, response.Response.StatusCode);
        Assert.AreEqual(noStore, response.Response.Headers.ContainsKey("Cache-Control"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => f.InvokeAsync(path, "GET", cancelled.Token));
        Assert.AreSame(f.QuotaFailure, error);
        Assert.IsFalse(f.Calls.Any(call => call.StartsWith("read-")));
    }

    private static (string Method, string Path) Route(string operation) => operation switch
    {
        "create" => ("POST", Applications + "/"),
        "issue" => ("POST", AppPath + "/keys"),
        "rotate" => ("POST", KeyPath + "/rotate"),
        "revoke-app" => ("DELETE", AppPath),
        _ => ("DELETE", KeyPath)
    };
}
