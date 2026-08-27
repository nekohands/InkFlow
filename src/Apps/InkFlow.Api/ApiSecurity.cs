using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using InkFlow.BuildingBlocks.Security;
using InkFlow.BuildingBlocks.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InkFlow.Api;

/// <summary>
/// API v1 的单实例限流默认值。未来接入 Redis 时保留同一个 policy/key seam，
/// 不让业务端点直接依赖具体限流存储。
/// </summary>
public sealed class ApiRateLimitOptions
{
    public const string ConfigurationSectionName = "RateLimiting";

    public int PublicPermitLimit { get; init; } = 120;
    public int PublicWindowSeconds { get; init; } = 60;
    public int LegadoPermitLimit { get; init; } = 60;
    public int LegadoWindowSeconds { get; init; } = 60;
    public int QueueLimit { get; init; }

    public TimeSpan PublicWindow => TimeSpan.FromSeconds(PublicWindowSeconds);
    public TimeSpan LegadoWindow => TimeSpan.FromSeconds(LegadoWindowSeconds);

    public static ApiRateLimitOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new ApiRateLimitOptions
        {
            PublicPermitLimit = ReadInt(section, nameof(PublicPermitLimit), 120),
            PublicWindowSeconds = ReadInt(section, nameof(PublicWindowSeconds), 60),
            LegadoPermitLimit = ReadInt(section, nameof(LegadoPermitLimit), 60),
            LegadoWindowSeconds = ReadInt(section, nameof(LegadoWindowSeconds), 60),
            QueueLimit = ReadInt(section, nameof(QueueLimit), 0),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        ValidateRange(PublicPermitLimit, 1, 100_000, nameof(PublicPermitLimit));
        ValidateRange(PublicWindowSeconds, 1, 3_600, nameof(PublicWindowSeconds));
        ValidateRange(LegadoPermitLimit, 1, 100_000, nameof(LegadoPermitLimit));
        ValidateRange(LegadoWindowSeconds, 1, 3_600, nameof(LegadoWindowSeconds));
        ValidateRange(QueueLimit, 0, 100, nameof(QueueLimit));
    }

    private static int ReadInt(IConfiguration section, string key, int defaultValue)
    {
        var raw = section[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException(
                $"RateLimiting:{key} must be an integer.");
        }

        return value;
    }

    private static void ValidateRange(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                $"RateLimiting:{name} must be between {minimum} and {maximum}.");
        }
    }
}

public static class ApiRateLimitPolicies
{
    public const string PublicPolicyName = "api-public";
    public const string LegadoPolicyName = "legado";

    public static IServiceCollection AddInkFlowApiRateLimiting(
        this IServiceCollection services,
        ApiRateLimitOptions options)
    {
        options.Validate();
        services.AddSingleton(options);
        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiterOptions.OnRejected = (context, _) =>
            {
                var retryAfterSeconds = Math.Max(
                    options.PublicWindowSeconds,
                    options.LegadoWindowSeconds);
                context.HttpContext.Response.Headers["Retry-After"] =
                    retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            rateLimiterOptions.AddPolicy(
                PublicPolicyName,
                context => CreatePartition(
                    context,
                    PublicPolicyName,
                    options.PublicPermitLimit,
                    options.PublicWindow,
                    options.QueueLimit));
            rateLimiterOptions.AddPolicy(
                LegadoPolicyName,
                context => CreatePartition(
                    context,
                    LegadoPolicyName,
                    options.LegadoPermitLimit,
                    options.LegadoWindow,
                    options.QueueLimit));
        });

        return services;
    }

    public static RateLimitPartition<string> CreatePartition(
        HttpContext context,
        string policyName,
        int permitLimit,
        TimeSpan window,
        int queueLimit)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException("policy name must not be empty.", nameof(policyName));
        }

        if (permitLimit < 1 || window <= TimeSpan.Zero || queueLimit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permitLimit), "rate-limit settings must be positive and queue must not be negative.");
        }

        var partitionKey = $"{policyName}:{ResolveClientKey(context)}";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = queueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });
    }

    /// <summary>
    /// 未配置可信代理前只读取连接层 RemoteIpAddress，不信任可伪造的 X-Forwarded-For。
    /// 认证主体存在时按主体分桶；匿名请求按 IP 分桶。
    /// </summary>
    public static string ResolveClientKey(HttpContext context)
    {
        var identity = context.User.Identity;
        if (identity?.IsAuthenticated == true)
        {
            var subject = context.User.FindFirst("sub")?.Value
                ?? context.User.FindFirst("client_id")?.Value;
            if (!string.IsNullOrWhiteSpace(subject))
            {
                return $"principal:{Hash(subject)}";
            }
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}

/// <summary>
/// 对公共 API 和 Legado API 记录结构化请求审计；不记录 query string，避免把搜索词或秘密带入日志。
/// </summary>
public sealed class RequestAuditMiddleware(
    RequestDelegate next,
    ILogger<RequestAuditMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IAuditEventSink auditSink,
        TimeProvider clock)
    {
        if (!ShouldAudit(context.Request.Path))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        Exception? unhandled = null;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            unhandled = exception;
            throw;
        }
        finally
        {
            var statusCode = unhandled is null
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;
            var actor = ResolveActor(context);
            var auditEvent = AuditEvent.Create(
                action: context.Request.Method,
                resource: context.Request.Path.Value ?? "/",
                outcome: ClassifyOutcome(statusCode),
                statusCode: statusCode,
                occurredAt: clock.GetUtcNow(),
                actorType: actor.Type,
                actorId: actor.Id,
                reason: unhandled is not null
                    ? "unhandled-exception"
                    : statusCode >= 400 ? $"http-{statusCode}" : null,
                traceId: System.Diagnostics.Activity.Current?.TraceId.ToString()
                    ?? context.TraceIdentifier);

            try
            {
                await auditSink.AppendAsync(auditEvent, context.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (Exception sinkException)
            {
                // 审计 sink 暂时不可用不能改变用户请求的结果；错误本身进入宿主日志告警。
                logger.LogError(
                    sinkException,
                    "audit sink failed for {AuditEventId} ({Action} {Resource})",
                    auditEvent.Id,
                    auditEvent.Action,
                    auditEvent.Resource);
            }
        }
    }

    private static bool ShouldAudit(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/legado");

    private static string ClassifyOutcome(int statusCode) => statusCode switch
    {
        >= 500 => "server_error",
        >= 400 => "client_error",
        _ => "success",
    };

    private static (string Type, string? Id) ResolveActor(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return ("anonymous", null);
        }

        return (
            "authenticated",
            context.User.FindFirst("sub")?.Value
                ?? context.User.Identity.Name);
    }
}

public sealed class LoggingAuditEventSink(
    ILogger<LoggingAuditEventSink> logger) : IAuditEventSink
{
    public ValueTask AppendAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "audit_event id={AuditEventId} occurred_at={OccurredAt} actor_type={ActorType} actor_id={ActorId} action={Action} resource={Resource} outcome={Outcome} status_code={StatusCode} reason={Reason} trace_id={TraceId} reference={Reference}",
            auditEvent.Id,
            auditEvent.OccurredAt,
            auditEvent.ActorType,
            auditEvent.ActorId,
            auditEvent.Action,
            auditEvent.Resource,
            auditEvent.Outcome,
            auditEvent.StatusCode,
            auditEvent.Reason,
            auditEvent.TraceId,
            auditEvent.Reference);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// API 审计双写组合器：数据库提供持久化事实，结构化日志保留即时运维可见性。
/// 持久化失败不改变请求结果，但会被记录并触发既有 sink 错误处理路径。
/// </summary>
public sealed class CompositeAuditEventSink(
    PersistentAuditEventSink persistentSink,
    LoggingAuditEventSink loggingSink,
    ILogger<CompositeAuditEventSink> logger) : IAuditEventSink
{
    public async ValueTask AppendAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await persistentSink.AppendAsync(auditEvent, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "persistent audit sink failed for {AuditEventId} ({Action} {Resource})",
                auditEvent.Id,
                auditEvent.Action,
                auditEvent.Resource);
        }

        await loggingSink.AppendAsync(auditEvent, cancellationToken)
            .ConfigureAwait(false);
    }
}
