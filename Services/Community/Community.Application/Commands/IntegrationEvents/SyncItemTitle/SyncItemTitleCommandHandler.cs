using Community.Application.Abstractions.Persistence;
using Community.Application.Caching;
using Community.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Community.Application.Commands.IntegrationEvents.SyncItemTitle;

// Ідемпотентне застосування ItemUpdated у MongoDB.
// MongoDB в Aspire — standalone (без replica set), тож багатодокументних транзакцій немає; ідемпотентність
// забезпечує порядок кроків:
//   1) EventId уже в processedEvents -> дублікат, пропуск;
//   2) updateMany з умовою itemTitleSyncedAt < changedAt — повторне застосування тієї самої події нічого не змінює;
//   3) лише після цього EventId записується в processedEvents (unique _id).
// Падіння між 2) і 3) безпечне: повторна доставка пройде крок 2 вхолосту й позначить подію обробленою.
public sealed class SyncItemTitleCommandHandler(
    IExpertOpinionRepository opinions,
    IDiscussionRepository discussions,
    IProcessedEventStore processedEvents,
    TwoLevelCache cache,
    ILogger<SyncItemTitleCommandHandler> logger)
    : IRequestHandler<SyncItemTitleCommand, SyncItemTitleResult>
{
    public async Task<SyncItemTitleResult> Handle(SyncItemTitleCommand request, CancellationToken cancellationToken)
    {
        if (await processedEvents.IsProcessedAsync(request.EventId, cancellationToken))
        {
            logger.LogInformation("Duplicate {EventType} {EventId} for item {ItemId} skipped", request.EventType, request.EventId, request.ItemId);
            return new SyncItemTitleResult(true, 0, 0);
        }

        var title = request.Title.Trim();
        var opinionsUpdated = await opinions.UpdateItemTitleAsync(request.ItemId, title, request.ChangedAt, cancellationToken);
        var discussionsUpdated = await discussions.UpdateItemTitleAsync(request.ItemId, title, request.ChangedAt, cancellationToken);

        if (!await processedEvents.TryMarkProcessedAsync(request.EventId, request.EventType, cancellationToken))
            logger.LogInformation("{EventType} {EventId} was marked processed concurrently by another instance", request.EventType, request.EventId);

        if (opinionsUpdated > 0)
            await cache.InvalidateAsync(CommunityCacheKeys.OpinionsForItemVersion(request.ItemId),
                $"item {request.ItemId} renamed", cancellationToken);

        return new SyncItemTitleResult(false, opinionsUpdated, discussionsUpdated);
    }
}
