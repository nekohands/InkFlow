using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>来源级启停；与单项能力健康状态分离。</summary>
public sealed class SourceLifecycleService(
    ISourceRepository repository,
    TimeProvider clock)
{
    public async Task<Source?> SetEnabledAsync(
        string sourceId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var source = await repository.GetAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return null;
        }

        if (source.IsEnabled != enabled)
        {
            if (enabled)
            {
                source.Enable(clock.GetUtcNow());
            }
            else
            {
                source.Disable(clock.GetUtcNow());
            }

            await repository.SaveAsync(source, cancellationToken).ConfigureAwait(false);
        }

        return source;
    }
}
