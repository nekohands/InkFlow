using InkFlow.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ApiErrorHandlingTests
{
    [TestMethod]
    public async Task Unhandled_Exceptions_Return_Sanitized_ProblemDetails_Even_In_Development()
    {
        await using var app = CreateApp(Environments.Development);
        await app.StartAsync().ConfigureAwait(false);
        var client = app.GetTestClient();

        var response = await client.GetAsync("/boom").ConfigureAwait(false);

        Assert.AreEqual(
            System.Net.HttpStatusCode.InternalServerError,
            response.StatusCode);
        StringAssert.Contains(
            response.Content.Headers.ContentType!.ToString(),
            "application/problem+json");
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        StringAssert.Contains(body, "\"title\"");
        Assert.IsFalse(body.Contains("secret-exception-detail"), body);
        Assert.IsFalse(body.Contains("InvalidOperationException"), body);
        Assert.IsFalse(body.Contains("exceptionDetails"), body);
        Assert.IsFalse(body.Contains("StackTrace"), body);
        Assert.IsFalse(body.Contains("at InkFlow"), body);
    }

    [TestMethod]
    public async Task Handled_Responses_And_Route_Patterns_Are_Not_Rewritten()
    {
        await using var app = CreateApp(Environments.Production);
        await app.StartAsync().ConfigureAwait(false);
        var client = app.GetTestClient();

        var ok = await client.GetAsync("/ok").ConfigureAwait(false);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, ok.StatusCode);
        Assert.AreEqual("application/json", ok.Content.Headers.ContentType!.MediaType);
        var body = await ok.Content.ReadAsStringAsync().ConfigureAwait(false);
        StringAssert.Contains(body, "\"error\":\"already_handled\"");

        var missing = await client.GetAsync("/missing").ConfigureAwait(false);
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static WebApplication CreateApp(string environmentName)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = environmentName;
        builder.WebHost.UseTestServer();
        builder.Services.AddInkFlowProblemDetails();

        var app = builder.Build();
        app.UseInkFlowExceptionHandler();
        app.MapGet("/boom", async (HttpContext _) =>
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("secret-exception-detail");
        });
        app.MapGet("/ok", () => Microsoft.AspNetCore.Http.Results.Json(
            new { error = "already_handled" }));
        return app;
    }
}
