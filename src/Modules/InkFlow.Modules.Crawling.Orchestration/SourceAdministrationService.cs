using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling;
using InkFlow.Modules.Sources.Rules;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Crawling.Orchestration;

public sealed record SourceAdminSummary(Guid Id, string Name, string BaseUrl, string Kind, string Status, double HealthScore, Guid? ActiveRuleVersionId);
public sealed record SourceRuleVersionSummary(Guid Id, Guid SourceId, int Version, int SchemaVersion, string Status, DateTimeOffset PublishedAtUtc);
public sealed record RuleValidationReport(bool IsValid, IReadOnlyList<RuleValidationError> Errors);
public sealed record PublishRuleResult(SourceRuleVersionSummary? Rule, IReadOnlyList<RuleValidationError> Errors);
public sealed record BookImportResult(Guid TaskId, bool Enqueued, string IdempotencyKey);

public sealed class SourceAdministrationService(
    SourcesDbContext sources,
    CrawlingDbContext crawling,
    TimeProvider timeProvider)
{
    private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Official", "Community", "Private"
    };

    private readonly CrawlerTaskStore _tasks = new(crawling);

    public Task<List<SourceAdminSummary>> ListSourcesAsync(CancellationToken cancellationToken = default) =>
        sources.Sources.AsNoTracking()
            .OrderBy(source => source.Name)
            .Select(source => new SourceAdminSummary(
                source.Id, source.Name, source.BaseUrl, source.Kind, source.Status,
                source.HealthScore, source.ActiveRuleVersionId))
            .ToListAsync(cancellationToken);

    public async Task<SourceAdminSummary> CreateSourceAsync(
        string name,
        string baseUrl,
        string? kind,
        CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is < 2 or > 256)
        {
            throw new ArgumentException("Source name must be between 2 and 256 characters.", nameof(name));
        }

        if (!TryHttpUri(baseUrl, out var baseUri))
        {
            throw new ArgumentException("Source baseUrl must be an absolute HTTP or HTTPS URL.", nameof(baseUrl));
        }

        var normalizedKind = string.IsNullOrWhiteSpace(kind) ? "Official" : kind.Trim();
        if (!AllowedKinds.Contains(normalizedKind))
        {
            throw new ArgumentException("Source kind must be Official, Community, or Private.", nameof(kind));
        }

        var normalizedBaseUrl = baseUri.AbsoluteUri.TrimEnd('/');
        if (await sources.Sources.AsNoTracking().AnyAsync(
                source => source.Name == name && source.BaseUrl == normalizedBaseUrl,
                cancellationToken))
        {
            throw new InvalidOperationException("A source with the same name and base URL already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var record = new SourceRecord
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            BaseUrl = normalizedBaseUrl,
            Kind = AllowedKinds.First(value => string.Equals(value, normalizedKind, StringComparison.OrdinalIgnoreCase)),
            Status = "Active",
            CapabilitiesJson = "[]",
            HealthScore = 100,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        sources.Sources.Add(record);
        await sources.SaveChangesAsync(cancellationToken);
        return ToSummary(record);
    }

    public RuleValidationReport ValidateRule(string ruleJson)
    {
        try
        {
            var rule = SourceRuleJson.Deserialize(ruleJson);
            var errors = new SourceRuleValidator().Validate(rule);
            return new(errors.Count == 0, errors);
        }
        catch (JsonException exception)
        {
            return new(false, [new RuleValidationError("RULE_JSON_INVALID", "$", exception.Message)]);
        }
    }

    public async Task<PublishRuleResult> PublishRuleAsync(
        Guid sourceId,
        string ruleJson,
        CancellationToken cancellationToken = default)
    {
        SourceRuleDocument rule;
        try
        {
            rule = SourceRuleJson.Deserialize(ruleJson);
        }
        catch (JsonException exception)
        {
            return new(null, [new RuleValidationError("RULE_JSON_INVALID", "$", exception.Message)]);
        }

        var errors = new SourceRuleValidator().Validate(rule).ToList();
        var source = await sources.Sources.SingleOrDefaultAsync(item => item.Id == sourceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Source {sourceId} was not found.");

        if (TryHttpUri(source.BaseUrl, out var sourceBase)
            && TryHttpUri(rule.BaseUrl, out var ruleBase)
            && !SameOrigin(sourceBase, ruleBase))
        {
            errors.Add(new RuleValidationError(
                "RULE_BASE_URL_SOURCE_MISMATCH",
                "baseUrl",
                "Rule baseUrl must use the same origin as its source."));
        }

        if (errors.Count > 0)
        {
            return new(null, errors);
        }

        await using var transaction = await sources.Database.BeginTransactionAsync(cancellationToken);
        var existing = await sources.RuleVersions
            .Where(version => version.SourceId == sourceId)
            .OrderByDescending(version => version.Version)
            .ToListAsync(cancellationToken);
        var nextVersion = existing.Count == 0 ? 1 : existing[0].Version + 1;
        foreach (var published in existing.Where(version => version.Status == "Published"))
        {
            published.Status = "Superseded";
        }

        var now = timeProvider.GetUtcNow();
        var record = new SourceRuleVersionRecord
        {
            Id = Guid.CreateVersion7(),
            SourceId = sourceId,
            Version = nextVersion,
            SchemaVersion = rule.SchemaVersion,
            Status = "Published",
            RuleJson = SourceRuleJson.Serialize(rule),
            CreatedAtUtc = now,
            PublishedAtUtc = now
        };
        sources.RuleVersions.Add(record);
        source.ActiveRuleVersionId = record.Id;
        source.CapabilitiesJson = JsonSerializer.Serialize(
            Enum.GetValues<SourceCapability>()
                .Where(capability => capability != SourceCapability.None && rule.Capabilities.HasFlag(capability))
                .Select(capability => capability.ToString()));
        source.Status = "Active";
        source.UpdatedAtUtc = now;
        await sources.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(new SourceRuleVersionSummary(
            record.Id, record.SourceId, record.Version, record.SchemaVersion,
            record.Status, record.PublishedAtUtc.Value), []);
    }

    public async Task<BookImportResult> EnqueueBookImportAsync(
        Guid sourceId,
        string bookUrl,
        string? externalId,
        CancellationToken cancellationToken = default)
    {
        if (!TryHttpUri(bookUrl, out var bookUri))
        {
            throw new ArgumentException("bookUrl must be an absolute HTTP or HTTPS URL.", nameof(bookUrl));
        }

        var source = await sources.Sources.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sourceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Source {sourceId} was not found.");
        if (source.ActiveRuleVersionId is null)
        {
            throw new InvalidOperationException("Source does not have an active published rule.");
        }

        var ruleVersion = await sources.RuleVersions.AsNoTracking().SingleOrDefaultAsync(
            version => version.Id == source.ActiveRuleVersionId.Value
                && version.SourceId == sourceId
                && version.Status == "Published",
            cancellationToken)
            ?? throw new InvalidOperationException("Source active rule is not published.");
        var rule = SourceRuleJson.Deserialize(ruleVersion.RuleJson);
        if (!rule.Capabilities.HasFlag(SourceCapability.BookInfo) || rule.BookInfo is null)
        {
            throw new InvalidOperationException("Source active rule does not support BookInfo imports.");
        }

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bookUrl"] = bookUri.AbsoluteUri
        };
        if (!string.IsNullOrWhiteSpace(externalId))
        {
            variables["externalId"] = externalId.Trim();
        }

        var payload = new RuleCrawlerTaskPayload(
            sourceId,
            ruleVersion.Id,
            SourceOperation.BookInfo,
            null,
            null,
            variables);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{sourceId:N}|{ruleVersion.Id:N}|{bookUri.AbsoluteUri}"))).ToLowerInvariant();
        var idempotencyKey = $"import:{fingerprint}";
        var now = timeProvider.GetUtcNow();
        var task = new CrawlerTaskRecord
        {
            Id = Guid.CreateVersion7(),
            Type = RuleCrawlerTaskPayload.TaskType,
            SourceId = sourceId,
            Payload = payload.Serialize(),
            IdempotencyKey = idempotencyKey,
            Priority = 90,
            MaxAttempts = 5,
            ScheduledAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var enqueued = await _tasks.TryEnqueueAsync(task, cancellationToken);
        if (!enqueued)
        {
            var existing = await crawling.CrawlerTasks.AsNoTracking()
                .SingleAsync(item => item.IdempotencyKey == idempotencyKey, cancellationToken);
            return new(existing.Id, false, idempotencyKey);
        }

        return new(task.Id, true, idempotencyKey);
    }

    private static SourceAdminSummary ToSummary(SourceRecord source) => new(
        source.Id, source.Name, source.BaseUrl, source.Kind, source.Status,
        source.HealthScore, source.ActiveRuleVersionId);

    private static bool TryHttpUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private static bool SameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;
}
