namespace InkFlow.Api;

public static class ApiEndpoints
{
    public static void MapInkFlowEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Api" }));
        var api = app.MapGroup("/api/v1")
            .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

        AuthEndpointResults.Map(api);
        AccountEndpointResults.Map(api);
        LegadoTokenEndpointResults.Map(api);
        api.MapPrivateLibraryEndpoints();
        api.MapDeveloperManagementEndpoints();
        api.MapDeveloperAdministrationEndpoints();
        app.MapDeveloperCatalogEndpoints();
        ReadingEndpointResults.Map(api);
        OperationsEndpoints.Map(api);
        SourceHealthEndpointResults.Map(api);
        ResourcePermissionEndpointResults.Map(api);
        SourceCredentialBindingEndpointResults.Map(api);
        ContentPolicyEndpointResults.Map(api);
        CatalogEndpoints.Map(api);
        LegadoEndpoints.Map(app);
        ReaderEndpoints.Map(app);
    }
}
