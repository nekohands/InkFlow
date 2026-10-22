using InkFlow.Api;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Legado.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ApiCompositionTests
{
    [TestMethod]
    public async Task Feature_Composition_Preserves_Route_Binding_And_Access_Policies()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.AddInkFlowApiServices();
        await using var app = builder.Build();
        app.MapInkFlowEndpoints();

        // Building endpoint delegates also validates parameter/service inference.
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var routes = endpoints.SelectMany(endpoint =>
            (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
            .Select(method => (Key: $"{method} {endpoint.RoutePattern.RawText}", Endpoint: endpoint)))
            .ToArray();
        Assert.AreEqual(routes.Length, routes.Select(route => route.Key).Distinct().Count());

        (string Route, string? Policy, string? RateLimit)[] expected =
        [
            ("GET /health", null, null),
            ("POST /api/v1/auth/register", null, ApiRateLimitPolicies.PublicPolicyName),
            ("POST /api/v1/auth/logout", "", ApiRateLimitPolicies.PublicPolicyName),
            ("PUT /api/v1/me/profile/avatar", "", ApiRateLimitPolicies.PublicPolicyName),
            ("POST /api/v1/me/legado/tokens", "", ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/v1/me/reading/shelf", "", ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/v1/admin/audit/events", IdentityPolicies.AuditRead, ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/v1/admin/operations/overview", IdentityPolicies.OperationsSnapshotRead, ApiRateLimitPolicies.PublicPolicyName),
            ("POST /api/v1/admin/sources/{sourceId}/health/{rawCapability}/disable", IdentityPolicies.SourceOperations, ApiRateLimitPolicies.PublicPolicyName),
            ("POST /api/v1/admin/sources/{sourceId}/permissions", IdentityPolicies.PermissionManagement, ApiRateLimitPolicies.PublicPolicyName),
            ("PUT /api/v1/admin/sources/{sourceId}/credential-binding", IdentityPolicies.SourceCredentialManagement, ApiRateLimitPolicies.PublicPolicyName),
            ("POST /api/v1/admin/content/takedowns", IdentityPolicies.ContentModeration, ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/v1/books", null, ApiRateLimitPolicies.PublicPolicyName),
            ("POST /api/v1/me/developer-applications/", "", ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/v1/me/developer-applications/", "", ApiRateLimitPolicies.PublicPolicyName),
            ("DELETE /api/v1/me/developer-applications/{applicationId:guid}", "", ApiRateLimitPolicies.PublicPolicyName),
            ("POST /api/v1/me/developer-applications/{applicationId:guid}/keys", "", ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/v1/me/developer-applications/{applicationId:guid}/keys", "", ApiRateLimitPolicies.PublicPolicyName),
            ("POST /api/v1/me/developer-applications/{applicationId:guid}/keys/{keyId:guid}/rotate", "", ApiRateLimitPolicies.PublicPolicyName),
            ("DELETE /api/v1/me/developer-applications/{applicationId:guid}/keys/{keyId:guid}", "", ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/v1/me/entitlement/", "", ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/v1/admin/plans", IdentityPolicies.CommercialManagement, ApiRateLimitPolicies.PublicPolicyName),
            ("PUT /api/v1/admin/users/{userId:guid}/entitlement", IdentityPolicies.CommercialManagement, ApiRateLimitPolicies.PublicPolicyName),
            ("GET /api/developer/v1/search", DeveloperEndpointPolicies.CatalogRead, ApiRateLimitPolicies.DeveloperPolicyName),
            ("GET /api/developer/v1/books", DeveloperEndpointPolicies.CatalogRead, ApiRateLimitPolicies.DeveloperPolicyName),
            ("GET /api/developer/v1/books/{bookId:guid}", DeveloperEndpointPolicies.CatalogRead, ApiRateLimitPolicies.DeveloperPolicyName),
            ("GET /api/developer/v1/books/{bookId:guid}/chapters", DeveloperEndpointPolicies.CatalogRead, ApiRateLimitPolicies.DeveloperPolicyName),
            ("GET /api/developer/v1/chapters/{chapterId:guid}/content", DeveloperEndpointPolicies.CatalogRead, ApiRateLimitPolicies.DeveloperPolicyName),
            ("GET /api/legado/v1/search", null, ApiRateLimitPolicies.LegadoPolicyName),
            ($"GET {LegadoRoutePrefixes.Personal}/search", IdentityPolicies.LegadoRead, ApiRateLimitPolicies.LegadoPolicyName),
            ("GET /reader", null, ApiRateLimitPolicies.PublicPolicyName),
            ("GET /legado/book-source.json", null, ApiRateLimitPolicies.LegadoPolicyName),
        ];
        foreach (var (route, policy, rateLimit) in expected)
        {
            var endpoint = routes.Single(item => item.Key == route).Endpoint;
            var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            if (policy is null)
            {
                Assert.AreEqual(0, authorization.Count, route);
            }
            else
            {
                Assert.IsTrue(authorization.Any(item => (item.Policy ?? "") == policy), route);
            }
            Assert.AreEqual(rateLimit,
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName, route);
        }
    }
}
