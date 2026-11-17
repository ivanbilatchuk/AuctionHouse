using Catalog.Dal.Entities;
using Catalog.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public sealed class ProcessedEventRepository(CatalogDbContext context) : IProcessedEventRepository
{
    public Task<bool> ExistsAsync(Guid eventId, CancellationToken ct = default)
        => context.ProcessedEvents.AsNoTracking().AnyAsync(e => e.EventId == eventId, ct);

    public void Add(Guid eventId, string eventType, DateTime processedAt)
        => context.ProcessedEvents.Add(new ProcessedEvent(eventId, eventType, processedAt));
}
