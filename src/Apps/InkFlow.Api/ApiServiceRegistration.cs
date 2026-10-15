using InkFlow.BuildingBlocks.Application;
using InkFlow.BuildingBlocks.Messaging;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Billing.Infrastructure;
using InkFlow.Modules.Billing.Infrastructure.Persistence;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using InkFlow.Modules.Developers.Infrastructure.Authentication;
using InkFlow.Modules.Developers.Infrastructure.Credentials;
using InkFlow.Modules.Developers.Infrastructure.Persistence;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Identity.Infrastructure.Authentication;
using InkFlow.Modules.Identity.Infrastructure.Credentials;
using InkFlow.Modules.Identity.Infrastructure.Persistence;
using InkFlow.Modules.Legado.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Operations.Infrastructure.Persistence;
using InkFlow.Modules.Reading.Application;
using InkFlow.Modules.Reading.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Api;

public static class ApiServiceRegistration
{
    public static void AddInkFlowApiServices(this WebApplicationBuilder builder)
    {
        builder.AddInkFlowObservability("InkFlow.Api");
        builder.Services.AddInkFlowApiRateLimiting(
            ApiRateLimitOptions.FromConfiguration(builder.Configuration));
        builder.Services.AddInkFlowProblemDetails();

        // 来源发现按需使用老站编码(kanunu8 GB18030 等)。
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        var databaseConnectionString =
            builder.Configuration.GetConnectionString("Database")
            ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

        builder.Services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddDbContext<MessagingDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddDbContextFactory<OperationsDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddDbContext<DeveloperDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddDbContext<CrawlingDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddScoped<ITransactionalOutboxWriter, EfTransactionalOutboxWriter>();
        builder.Services.AddScoped<LoggingAuditEventSink>();
        builder.Services.AddScoped<PersistentAuditEventSink>();
        builder.Services.AddScoped<IAuditEventSink, CompositeAuditEventSink>();
        builder.Services.AddScoped<IAuditEventReader, EfAuditEventReader>();
        builder.Services.AddScoped<EfMessagingMessageStore>();
        builder.Services.AddScoped<IInboxDeadLetterReader>(sp =>
            sp.GetRequiredService<EfMessagingMessageStore>());

        builder.Services.AddScoped<IUserRepository, EfUserRepository>();
        builder.Services.AddScoped<IUserAvatarRepository, EfUserAvatarRepository>();
        builder.Services.AddScoped<IIdentitySessionRepository, EfIdentitySessionRepository>();
        builder.Services.AddScoped<ILegadoAccessTokenRepository, EfLegadoAccessTokenRepository>();
        builder.Services.AddScoped<IResourcePermissionRepository, EfResourcePermissionRepository>();
        builder.Services.AddSingleton(IdentityOptions.FromConfiguration(builder.Configuration));
        builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        builder.Services.AddSingleton<IOpaqueTokenGenerator, SecureOpaqueTokenGenerator>();
        builder.Services.AddScoped<IIdentitySecurityEventSink, AuditIdentitySecurityEventSink>();
        builder.Services.AddScoped<IIdentityService, IdentityService>();
        builder.Services.AddScoped<ILegadoAccessTokenService, LegadoAccessTokenService>();
        builder.Services.AddScoped<IResourcePermissionService, ResourcePermissionService>();
        builder.Services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();
        builder.Services.AddScoped<DeveloperUserStatusReader>();
        builder.Services.AddScoped<IDeveloperUserStatusReader>(sp =>
            sp.GetRequiredService<DeveloperUserStatusReader>());
        builder.Services.AddScoped<IBillingUserStatusReader>(sp =>
            sp.GetRequiredService<DeveloperUserStatusReader>());
        builder.Services.AddScoped<EfDeveloperApplicationRepository>();
        builder.Services.AddScoped<IDeveloperApplicationRepository>(sp =>
            sp.GetRequiredService<EfDeveloperApplicationRepository>());
        builder.Services.AddScoped<EfDeveloperApiKeyRepository>();
        builder.Services.AddScoped<IDeveloperApiKeyRepository>(sp =>
            sp.GetRequiredService<EfDeveloperApiKeyRepository>());
        builder.Services.AddSingleton<IDeveloperApiKeySecretGenerator, DeveloperApiKeySecretGenerator>();
        builder.Services.AddScoped<DeveloperApplicationService>();
        builder.Services.AddScoped<IDeveloperApplicationService>(sp =>
            sp.GetRequiredService<DeveloperApplicationService>());
        builder.Services.AddScoped<IDeveloperApiKeyValidator>(sp =>
            sp.GetRequiredService<DeveloperApplicationService>());
        builder.Services.AddScoped<IPlanRepository, EfPlanRepository>();
        builder.Services.AddScoped<IEntitlementAssignmentRepository, EfEntitlementAssignmentRepository>();
        builder.Services.AddScoped<IEntitlementService, EntitlementService>();
        builder.Services.AddScoped<IQuotaSnapshotCache, RedisQuotaSnapshotCache>();
        builder.Services.AddScoped<IQuotaService, QuotaService>();
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityAuthenticationDefaults.Scheme;
                options.DefaultChallengeScheme = IdentityAuthenticationDefaults.Scheme;
            })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                OpaqueBearerAuthenticationHandler>(IdentityAuthenticationDefaults.Scheme, _ => { })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                LegadoTokenAuthenticationHandler>(IdentityAuthenticationDefaults.LegadoScheme, _ => { })
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                DeveloperApiKeyAuthenticationHandler>(DeveloperAuthenticationDefaults.Scheme, _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(
                IdentityPolicies.CrawlerRepair,
                policy => policy.RequireRole(
                    UserRole.Operator.ToString(),
                    UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.OperationsSnapshotRead,
                policy => policy.RequireRole(
                    UserRole.Reader.ToString(),
                    UserRole.Operator.ToString(),
                    UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.CollectionUse,
                policy => policy.RequireRole(
                    UserRole.Reader.ToString(),
                    UserRole.Operator.ToString(),
                    UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.BookPackageUse,
                policy => policy.RequireRole(
                    UserRole.Reader.ToString(),
                    UserRole.Operator.ToString(),
                    UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.OperationsRead,
                policy => policy.RequireRole(
                    UserRole.Operator.ToString(),
                    UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.AuditRead,
                policy => policy.RequireRole(
                    UserRole.Operator.ToString(),
                    UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.ContentModeration,
                policy => policy.RequireRole(UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.SourceOperations,
                policy => policy.RequireRole(
                    UserRole.Operator.ToString(),
                    UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.PermissionManagement,
                policy => policy.RequireRole(UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.SourceCredentialManagement,
                policy => policy.RequireRole(UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.CommercialManagement,
                policy => policy.RequireRole(UserRole.Administrator.ToString()));
            options.AddPolicy(
                IdentityPolicies.LegadoRead,
                policy => policy
                    .AddAuthenticationSchemes(IdentityAuthenticationDefaults.LegadoScheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(IdentityAuthenticationDefaults.LegadoScopeClaim, "read"));
            options.AddPolicy(
                DeveloperEndpointPolicies.CatalogRead,
                policy => policy
                    .AddAuthenticationSchemes(DeveloperAuthenticationDefaults.Scheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(DeveloperAuthenticationDefaults.ScopeClaim, DeveloperApiScopes.CatalogRead)
                    .RequireClaim(DeveloperAuthenticationDefaults.EnvironmentClaim, "production"));
        });

        builder.Services.AddScoped<EfCrawlerTaskRepository>();
        builder.Services.AddScoped<ICrawlerTaskRepository>(sp =>
            sp.GetRequiredService<EfCrawlerTaskRepository>());
        builder.Services.AddScoped<ICrawlerTaskRepairRepository>(sp =>
            sp.GetRequiredService<EfCrawlerTaskRepository>());
        builder.Services.AddScoped<ICollectionRunRepository, EfCollectionRunRepository>();

        builder.Services.AddDbContext<LibraryDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddDbContext<ReadingDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));
        builder.Services.AddDbContext<InkFlow.Modules.Sources.Infrastructure.Persistence.SourcesDbContext>(options =>
            options.UseNpgsql(databaseConnectionString));

        builder.Services.AddScoped<ICanonicalBookRepository, EfCanonicalBookRepository>();
        builder.Services.AddScoped<IPrivateBookRepository, EfPrivateBookRepository>();
        builder.Services.AddScoped<IPrivateLibraryService, PrivateLibraryService>();
        builder.Services.AddScoped<IPrivateBookImportParser, PrivateBookImportParser>();
        builder.Services.AddScoped<IPrivateLibraryContentService, PrivateLibraryContentService>();
        builder.Services.AddScoped<IMatchCandidateRepository, InkFlow.Modules.Library.Infrastructure.Persistence.EfMatchCandidateRepository>();
        builder.Services.AddScoped<ISourceRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceRepository>();
        builder.Services.AddScoped<ISourceBookRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceBookRepository>();
        builder.Services.AddScoped<ISourceHealthRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceHealthRepository>();
        builder.Services.AddSingleton(TimeProvider.System);
        var sourceHealthOptions = SourceHealthOptions.FromConfiguration(builder.Configuration);
        builder.Services.AddSingleton(sourceHealthOptions);
        SourceHealthPolicy.Configure(sourceHealthOptions.ToParameters());
        builder.Services.AddSingleton(SourceRuleExecutionLimits.Default);
        builder.Services.AddScoped<SourceHealthService>();
        builder.Services.AddScoped<ISourceHealthReader>(sp => sp.GetRequiredService<SourceHealthService>());
        builder.Services.AddScoped<ISourceHealthOperations>(sp => sp.GetRequiredService<SourceHealthService>());
        builder.Services.AddScoped<SourceCredentialBindingService>();
        builder.Services.AddScoped<ISourceCredentialBindingService>(sp =>
            sp.GetRequiredService<SourceCredentialBindingService>());

        // 规则型/代码型适配器组合根(与 Worker 同源):健康感知由 BookDiscoveryService 内部执行。
        builder.Services.AddSingleton<IIpAddressResolver, DnsIpAddressResolver>();
        builder.Services.AddSingleton<ISourceCredentialProvider, ConfigurationSourceCredentialProvider>();
        builder.Services.AddHttpClient<ISourceHttpClient, ProductionSafeSourceHttpClient>()
            .ConfigurePrimaryHttpMessageHandler(sp =>
                new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
        builder.Services.AddHttpClient<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>()
            .ConfigurePrimaryHttpMessageHandler(sp =>
                new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
        builder.Services.AddHttpClient<InkFlow.Sources.Adapters.SeventeenK.SeventeenKSourceAdapter>()
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(sp =>
                new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
        builder.Services.AddScoped<ISelectorEvaluator, RuleSelectorEvaluator>();
        builder.Services.AddScoped<RuleAdapter>();
        builder.Services.AddScoped<ISourceAdapterFactory>(sp => new SourceAdapterFactory(
            sp.GetRequiredService<ISourceRepository>(),
            sp.GetRequiredService<RuleAdapter>(),
            sp.GetRequiredService<ISelectorEvaluator>(),
            [
                sp.GetRequiredService<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>(),
                sp.GetRequiredService<InkFlow.Sources.Adapters.SeventeenK.SeventeenKSourceAdapter>(),
            ]));
        builder.Services.AddScoped<SourceCatalogService>();
        builder.Services.AddScoped<CanonicalBookMatchingService>();
        builder.Services.AddScoped<BookDiscoveryService>();
        builder.Services.AddScoped<SourceBookUrlResolver>();
        builder.Services.AddScoped<CollectionRunService>();
        builder.Services.AddScoped<SourceLifecycleService>();

        var connectionStringForContent =
            builder.Configuration.GetConnectionString("Database")
            ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";
        builder.Services.AddScoped<InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext>(_ =>
            new InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext(
                new DbContextOptionsBuilder<InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext>()
                    .UseNpgsql(connectionStringForContent)
                    .Options));
        builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentVersionRepository,
            InkFlow.Modules.Content.Infrastructure.Persistence.EfContentVersionRepository>();
        builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentSelectionDecisionRepository,
            InkFlow.Modules.Content.Infrastructure.Persistence.EfContentSelectionDecisionRepository>();
        builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentSelectionService,
            InkFlow.Modules.Content.Application.ContentSelectionService>();
        builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentPolicyRepository,
            InkFlow.Modules.Content.Infrastructure.Persistence.EfContentPolicyRepository>();
        builder.Services.AddScoped<InkFlow.Modules.Content.Application.ContentPolicyService>();
        builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentPolicyService>(sp =>
            sp.GetRequiredService<InkFlow.Modules.Content.Application.ContentPolicyService>());
        builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentPolicyReader>(sp =>
            sp.GetRequiredService<InkFlow.Modules.Content.Application.ContentPolicyService>());
        builder.Services.AddSingleton(BookPackageOptions.FromEnvironment());
        builder.Services.AddSingleton<IBookPackageBuilder, BookPackageBuilder>();
        builder.Services.AddSingleton<IBookPackageArtifactStore, FileBookPackageArtifactStore>();
        builder.Services.AddScoped<IBookPackageJobRepository, EfBookPackageJobRepository>();
        builder.Services.AddScoped<BookPackageService>();
        builder.Services.AddScoped<IConsistencySnapshotReader, EfConsistencySnapshotReader>();
        builder.Services.AddScoped<IConsistencyCheckService, ConsistencyCheckService>();
        builder.Services.AddScoped<IOperationsCenterReader, OperationsCenterReader>();
        builder.Services.AddSingleton(
            OperationsAlertOptions.FromConfiguration(builder.Configuration));
        builder.Services.AddScoped<IOperationsAlertReader, OperationsAlertReader>();
        builder.Services.AddScoped<IOperationsAlertHistoryRepository, EfOperationsAlertHistoryRepository>();
        builder.Services.AddScoped<IReadingStateRepository, EfReadingStateRepository>();
        builder.Services.AddScoped<IReadingStateService, ReadingStateService>();
        builder.Services.AddScoped<CatalogQueryService>();
        builder.Services.AddScoped<LegadoContractService>();
        builder.Services.AddSingleton<ILegadoRuleGenerator, LegadoRuleGenerator>();
    }
}
