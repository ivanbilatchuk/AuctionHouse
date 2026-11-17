using Auctions.Domain.Models;

namespace Auctions.Dal.Interfaces;

public interface IAuctionRepository : IGenericRepository<Auction>
{
    Task<Auction?> GetWithDetailsAsync(long id, bool forUpdate = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Auction>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);

    Task<bool> HasUnfinishedAuctionForItemAsync(long itemId, CancellationToken cancellationToken = default);

    Task ApplyBidAsync(long auctionId, decimal amount, long bidderId, DateTime endsAt, CancellationToken cancellationToken = default);

    Task<AuctionStatusChange> ChangeStatusAsync(long auctionId, string newStatus, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Лаба 8: оновлює копію назви предмета (ItemTitle) в усіх аукціонах предмета за подією Каталогу ItemUpdated.
    /// Подія, старіша за вже застосовану (ItemTitleSyncedAt), рядків не змінює. Повертає Id змінених аукціонів (для інвалідації кешу).
    /// </summary>
    Task<IReadOnlyList<long>> UpdateItemTitleAsync(long itemId, string itemTitle, DateTime changedAt, CancellationToken cancellationToken = default);
}

/// <summary>Результат sp_ChangeAuctionStatus: попередній і фактичний новий статус (Closed може стати Unsold).</summary>
public sealed record AuctionStatusChange(string? OldStatus, string NewStatus);
