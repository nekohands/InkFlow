namespace InkFlow.BuildingBlocks.Messaging;

/// <summary>
/// 一次性领取并投递 Outbox。发布传输成功后才写 ProcessedAt；
/// 发布失败则释放 lease 并安排有界退避。确认失败时不擅自标记成功，
/// 让 lease 到期后由其他实例再次投递，保持 at-least-once 语义。
/// </summary>
public sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly IOutboxStore _store;
    private readonly IIntegrationMessagePublisher _publisher;
    private readonly TimeProvider _clock;
    private readonly OutboxDispatcherOptions _options;

    public OutboxDispatcher(
        IOutboxStore store,
        IIntegrationMessagePublisher publisher,
        TimeProvider clock,
        OutboxDispatcherOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<OutboxDispatchResult> DispatchOnceAsync(
        CancellationToken cancellationToken = default)
    {
        _options.Validate();
        var claimedAt = _clock.GetUtcNow();
        var messages = await _store
            .ClaimBatchAsync(
                _options.Owner,
                claimedAt,
                _options.LeaseDuration,
                _options.BatchSize,
                cancellationToken)
            .ConfigureAwait(false);

        var publishedCount = 0;
        var failedCount = 0;
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _publisher
                    .PublishAsync(message, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                var failedAt = _clock.GetUtcNow();
                var retryDelay = _options.RetryPolicy.DelayFor(message.AttemptCount);
                MessageExecutionValidation.ValidateRetryDelay(retryDelay);
                await _store
                    .MarkFailedAsync(
                        message.Id,
                        _options.Owner,
                        failedAt,
                        failedAt + retryDelay,
                        MessageFailureCodes.PublishFailed,
                        cancellationToken)
                    .ConfigureAwait(false);
                failedCount++;
                continue;
            }

            // 若这里失败，不把消息错误地变成成功；lease 到期后会再次投递。
            await _store
                .MarkPublishedAsync(
                    message.Id,
                    _options.Owner,
                    _clock.GetUtcNow(),
                    cancellationToken)
                .ConfigureAwait(false);
            publishedCount++;
        }

        return new(messages.Count, publishedCount, failedCount);
    }
}

/// <summary>
/// Inbox 消费执行器。重复消息在 TryClaim 阶段结束；处理器只有成功返回后才确认。
/// 未注册处理器和处理异常均使用稳定失败码，不把异常原文写入消息存储。
/// </summary>
public sealed class IntegrationMessageConsumer : IIntegrationMessageConsumer
{
    private readonly IInboxStore _store;
    private readonly IIntegrationMessageHandlerResolver _handlerResolver;
    private readonly TimeProvider _clock;
    private readonly InboxConsumerOptions _options;

    public IntegrationMessageConsumer(
        IInboxStore store,
        IIntegrationMessageHandlerResolver handlerResolver,
        TimeProvider clock,
        InboxConsumerOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _handlerResolver = handlerResolver ?? throw new ArgumentNullException(nameof(handlerResolver));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<InboxConsumeResult> ConsumeAsync(
        IntegrationMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _options.Validate();

        var claimed = await _store
            .TryClaimAsync(
                message,
                _options.Owner,
                _clock.GetUtcNow(),
                _options.LeaseDuration,
                cancellationToken)
            .ConfigureAwait(false);
        if (claimed.Status != InboxClaimStatus.Claimed)
        {
            return new(
                message.Id,
                claimed.Status == InboxClaimStatus.AlreadyProcessed
                    ? InboxConsumeStatus.AlreadyProcessed
                    : InboxConsumeStatus.AlreadyInProgress,
                claimed.AttemptCount);
        }

        return await ConsumeClaimedAsync(
                new InboxMessageRecord(message, claimed.AttemptCount),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<InboxConsumeResult> ConsumeClaimedAsync(
        InboxMessageRecord claimedMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claimedMessage);
        var message = claimedMessage.Message;
        ArgumentNullException.ThrowIfNull(message);
        _options.Validate();

        var handler = _handlerResolver.Resolve(message.MessageType);
        if (handler is null)
        {
            await _store
                .MarkFailedAsync(
                    message.Id,
                    _options.Owner,
                    _clock.GetUtcNow(),
                    MessageFailureCodes.HandlerNotRegistered,
                    cancellationToken)
                .ConfigureAwait(false);
            return new(
                message.Id,
                InboxConsumeStatus.NoHandler,
                claimedMessage.AttemptCount,
                MessageFailureCodes.HandlerNotRegistered);
        }

        try
        {
            await handler
                .HandleAsync(message, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await _store
                .MarkFailedAsync(
                    message.Id,
                    _options.Owner,
                    _clock.GetUtcNow(),
                    MessageFailureCodes.HandlerFailed,
                    cancellationToken)
                .ConfigureAwait(false);
            return new(
                message.Id,
                InboxConsumeStatus.Failed,
                claimedMessage.AttemptCount,
                MessageFailureCodes.HandlerFailed);
        }

        await _store
            .MarkProcessedAsync(
                message.Id,
                _options.Owner,
                _clock.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
        return new(message.Id, InboxConsumeStatus.Processed, claimedMessage.AttemptCount);
    }
}
