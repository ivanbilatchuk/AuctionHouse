using Community.Domain.Entities;
using MongoDB.Bson;

namespace Community.Domain.Repositories;

public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    Task<bool> ExistsForAuctionAsync(long auctionId, CancellationToken ct = default);

    Task AddAsync(Discussion discussion, CancellationToken ct = default);

    Task UpdateAsync(Discussion discussion, int expectedVersion, CancellationToken ct = default);

    // Лаба 8: оновлює денормалізовану назву лота (item.title) в усіх документах лота за подією Каталогу ItemUpdated.
    // Документи, до яких уже застосовано новішу зміну (itemTitleSyncedAt >= changedAt), не змінюються (out-of-order).
    // Повертає кількість змінених документів.
    Task<long> UpdateItemTitleAsync(long itemId, string title, DateTime changedAt, CancellationToken ct = default);
}
