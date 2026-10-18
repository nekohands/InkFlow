using InkFlow.Modules.Sources.Domain;
using Microsoft.EntityFrameworkCore;

namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

internal sealed class SourceWriteStore(SourcesDbContext db)
{
    public async Task AddAsync(Source source, CancellationToken cancellationToken = default)
    {
        db.Sources.Add(ToEntity(source));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(Source source, CancellationToken cancellationToken = default)
    {
        var entity = await db.Sources.FindAsync([source.Id], cancellationToken).ConfigureAwait(false)
                     ?? throw new InvalidOperationException(
                         $"source '{source.Id}' does not exist; use AddAsync first.");

        ApplyDomain(source, entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SourceEntity ToEntity(Source source) => new()
    {
        Id = source.Id,
        DisplayName = source.DisplayName,
        BaseUrl = source.BaseUrl,
        RuleDslJson = source.RuleDsl is null ? null : SourcesDbContext.SerializeRuleDsl(source.RuleDsl),
        DefaultCredentialReferenceId = source.DefaultCredentialReferenceId,
        IsEnabled = source.IsEnabled,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
    };

    private static void ApplyDomain(Source source, SourceEntity entity)
    {
        var fresh = ToEntity(source);
        entity.DisplayName = fresh.DisplayName;
        entity.BaseUrl = fresh.BaseUrl;
        entity.RuleDslJson = fresh.RuleDslJson;
        entity.DefaultCredentialReferenceId = fresh.DefaultCredentialReferenceId;
        entity.IsEnabled = fresh.IsEnabled;
        entity.UpdatedAt = fresh.UpdatedAt;
    }
}
