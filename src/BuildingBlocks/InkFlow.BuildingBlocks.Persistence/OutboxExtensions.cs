using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

public static class OutboxExtensions
{
    public static OutboxMessage AddOutboxMessage(
        this DbContext dbContext,
        string type,
        string payload,
        DateTimeOffset occurredAtUtc,
        Guid? messageId = null)
    {
        var message = new OutboxMessage(messageId ?? Guid.CreateVersion7(), occurredAtUtc, type, payload);
        dbContext.Set<OutboxMessage>().Add(message);
        return message;
    }
}
