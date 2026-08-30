namespace InkFlow.BuildingBlocks.Messaging;

public sealed record InboxConsumeBatchResult(
    int ClaimedCount,
    int ProcessedCount,
    int FailedCount,
    int SkippedCount,
    int DeadLetteredCount = 0);

public interface IInboxConsumerPump
{
    Task<InboxConsumeBatchResult> ConsumeOnceAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 从持久 Inbox 领取一批已注册类型的消息，并在 Handler 成功后确认消费。
/// 没有注册 Handler 时不访问 Inbox，避免未知消息被无界失败重试。
/// </summary>
public sealed class InboxConsumerPump : IInboxConsumerPump
{
    private readonly IInboxStore _store;
    private readonly IIntegrationMessageConsumer _consumer;
    private readonly IIntegrationMessageHandlerResolver _handlerResolver;
    private readonly TimeProvider _clock;
    private readonly InboxConsumerOptions _options;

    public InboxConsumerPump(
        IInboxStore store,
        IIntegrationMessageConsumer consumer,
        IIntegrationMessageHandlerResolver handlerResolver,
        TimeProvider clock,
        InboxConsumerOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
        _handlerResolver = handlerResolver ?? throw new ArgumentNullException(nameof(handlerResolver));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<InboxConsumeBatchResult> ConsumeOnceAsync(
        CancellationToken cancellationToken = default)
    {
        _options.Validate();
        var messageTypes = _handlerResolver.MessageTypes;
        if (messageTypes.Count == 0)
        {
            return new(0, 0, 0, 0);
        }

        var claimed = await _store
            .ClaimBatchAsync(
                _options.Owner,
                _clock.GetUtcNow().ToUniversalTime(),
                _options.LeaseDuration,
                _options.BatchSize,
                messageTypes,
                cancellationToken)
            .ConfigureAwait(false);

        var processedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var deadLetteredCount = 0;
        foreach (var message in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _consumer
                .ConsumeClaimedAsync(message, cancellationToken)
                .ConfigureAwait(false);
            switch (result.Status)
            {
                case InboxConsumeStatus.Processed:
                    processedCount++;
                    break;
                case InboxConsumeStatus.Failed:
                case InboxConsumeStatus.NoHandler:
                    failedCount++;
                    break;
                case InboxConsumeStatus.DeadLettered:
                    deadLetteredCount++;
                    break;
                case InboxConsumeStatus.AlreadyProcessed:
                case InboxConsumeStatus.AlreadyInProgress:
                case InboxConsumeStatus.RetryScheduled:
                    skippedCount++;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"unknown inbox consume status: {result.Status}.");
            }
        }

        return new(
            claimed.Count,
            processedCount,
            failedCount,
            skippedCount,
            deadLetteredCount);
    }
}
