using Auctions.Dal.Interfaces;
using Auctions.Domain.Models;

namespace Auctions.Dal.Repositories;

public sealed class BidderRepository : BaseDapperRepository<Bidder>, IBidderRepository
{
    private const string SelectByIdQuery = """
        SELECT BidderId, ExternalUserId, DisplayName, Email, WalletBalance, IsBlocked,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
        FROM dbo.Bidder
        WHERE BidderId = @Id AND IsDeleted = 0;
        """;

    private const string SelectForUpdateQuery = """
        SELECT BidderId, ExternalUserId, DisplayName, Email, WalletBalance, IsBlocked,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
        FROM dbo.Bidder WITH (UPDLOCK, ROWLOCK)
        WHERE BidderId = @Id AND IsDeleted = 0;
        """;

    private const string SelectAllQuery = """
        SELECT BidderId, ExternalUserId, DisplayName, Email, WalletBalance, IsBlocked,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
        FROM dbo.Bidder
        WHERE IsDeleted = 0
        ORDER BY BidderId;
        """;

    private const string SelectByEmailQuery = """
        SELECT BidderId, ExternalUserId, DisplayName, Email, WalletBalance, IsBlocked,
               CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
        FROM dbo.Bidder
        WHERE Email = @Email AND IsDeleted = 0;
        """;

    private const string InsertQuery = """
        INSERT INTO dbo.Bidder (ExternalUserId, DisplayName, Email, WalletBalance, CreatedBy)
        OUTPUT INSERTED.BidderId
        VALUES (@ExternalUserId, @DisplayName, @Email, @WalletBalance, @Actor);
        """;

    private const string UpdateQuery = """
        UPDATE dbo.Bidder
        SET DisplayName = @DisplayName, Email = @Email, IsBlocked = @IsBlocked,
            UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
        WHERE BidderId = @BidderId AND RowVersion = @RowVersion AND IsDeleted = 0;
        """;

    private const string SoftDeleteQuery = """
        UPDATE dbo.Bidder
        SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
        WHERE BidderId = @Id AND IsDeleted = 0;
        """;

    private const string SelectRegistrationQuery = """
        SELECT AuctionId, BidderId, DepositAmount, Status, RegisteredAt
        FROM dbo.AuctionRegistration
        WHERE AuctionId = @AuctionId AND BidderId = @BidderId;
        """;

    private const string InsertRegistrationQuery = """
        INSERT INTO dbo.AuctionRegistration (AuctionId, BidderId, DepositAmount, Status, RegisteredAt, CreatedBy)
        VALUES (@AuctionId, @BidderId, @DepositAmount, @Status, SYSUTCDATETIME(), @Actor);
        """;

    private const string UpdateWalletQuery = """
        UPDATE dbo.Bidder
        SET WalletBalance = @NewBalance, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
        WHERE BidderId = @BidderId;

        INSERT INTO dbo.WalletTransaction (BidderId, AuctionId, Type, Amount, BalanceAfter, CreatedBy)
        VALUES (@BidderId, @AuctionId, @Type, @Amount, @NewBalance, @Actor);
        """;

    public BidderRepository(IDbSession session) : base(session)
    {
    }

    protected override string SelectByIdSql => SelectByIdQuery;

    protected override string SelectAllSql => SelectAllQuery;

    protected override string SoftDeleteSql => SoftDeleteQuery;

    public override Task<long> AddAsync(Bidder entity, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<long>(InsertQuery, new
        {
            entity.ExternalUserId,
            entity.DisplayName,
            entity.Email,
            entity.WalletBalance,
            Actor = AuditActor
        }, cancellationToken);

    public override async Task<bool> UpdateAsync(Bidder entity, CancellationToken cancellationToken = default) =>
        await ExecuteAsync(UpdateQuery, new
        {
            entity.BidderId,
            entity.DisplayName,
            entity.Email,
            entity.IsBlocked,
            entity.RowVersion,
            Actor = AuditActor
        }, cancellationToken: cancellationToken) > 0;

    public Task<Bidder?> GetForUpdateAsync(long id, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<Bidder>(SelectForUpdateQuery, new { Id = id }, cancellationToken: cancellationToken);

    public Task<Bidder?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<Bidder>(SelectByEmailQuery, new { Email = email }, cancellationToken: cancellationToken);

    public Task<AuctionRegistration?> GetRegistrationAsync(long auctionId, long bidderId, CancellationToken cancellationToken = default) =>
        QuerySingleOrDefaultAsync<AuctionRegistration>(
            SelectRegistrationQuery,
            new { AuctionId = auctionId, BidderId = bidderId },
            cancellationToken: cancellationToken);

    public async Task AddRegistrationAsync(AuctionRegistration registration, CancellationToken cancellationToken = default) =>
        await ExecuteAsync(InsertRegistrationQuery, new
        {
            registration.AuctionId,
            registration.BidderId,
            registration.DepositAmount,
            registration.Status,
            Actor = AuditActor
        }, cancellationToken: cancellationToken);

    public async Task UpdateWalletAsync(
        long bidderId,
        decimal newBalance,
        string transactionType,
        long? auctionId,
        decimal amount,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(UpdateWalletQuery, new
        {
            BidderId = bidderId,
            NewBalance = newBalance,
            Type = transactionType,
            AuctionId = auctionId,
            Amount = amount,
            Actor = AuditActor
        }, cancellationToken: cancellationToken);
}
