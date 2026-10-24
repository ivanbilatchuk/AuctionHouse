using System.Data;

namespace Auctions.Dal.Interfaces;

/// <summary>
/// Unit of Work: одне з'єднання та одна транзакція для кількох репозиторіїв.
/// Гарантує Commit або Rollback; при Dispose без Commit транзакція відкочується.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IAuctionRepository Auctions { get; }
    IBidderRepository Bidders { get; }
    IBidRepository Bids { get; }

    bool HasActiveTransaction { get; }

    /// <summary>
    /// Починає транзакцію. За замовчуванням ReadCommitted: не читає «брудні» дані, а
    /// конкурентні зміни рядка захищені явним UPDLOCK у репозиторіях (менше deadlock-ів, ніж Serializable).
    /// </summary>
    Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
