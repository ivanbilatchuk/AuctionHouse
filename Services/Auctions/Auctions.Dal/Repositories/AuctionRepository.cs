using System.Data;
using Auctions.Dal.Interfaces;
using Auctions.Domain.Models;

namespace Auctions.Dal.Repositories;

public sealed class AuctionRepository : BaseDapperRepository<Auction>, IAuctionRepository
{
    private const string SelectByIdQuery = """
        SELECT AuctionId, ItemId, ItemTitle, SellerExternalId, Status, StartsAt, EndsAt, StartingPrice, CurrentPrice,
               BidCount, LeadingBidderId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
        FROM dbo.Auction
        WHERE AuctionId = @Id AND IsDeleted = 0;
        """;

    private const string SelectAllQuery = """
        SELECT AuctionId, ItemId, ItemTitle, SellerExternalId, Status, StartsAt, EndsAt, StartingPrice, CurrentPrice,
               BidCount, LeadingBidderId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
        FROM dbo.Auction
        WHERE IsDeleted = 0
        ORDER BY AuctionId;
        """;

    private const string SelectByStatusQuery = """
        SELECT AuctionId, ItemId, ItemTitle, SellerExternalId, Status, StartsAt, EndsAt, StartingPrice, CurrentPrice,
               BidCount, LeadingBidderId, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, RowVersion
        FROM dbo.Auction
        WHERE Status = @Status AND IsDeleted = 0
        ORDER BY EndsAt;
        """;

    private const string SelectWithDetailsQuery = """
        SELECT a.AuctionId, a.ItemId, a.ItemTitle, a.SellerExternalId, a.Status, a.StartsAt, a.EndsAt, a.StartingPrice,
               a.CurrentPrice, a.BidCount, a.LeadingBidderId, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy,
               a.IsDeleted, a.RowVersion,
               d.AuctionId, d.ReservePrice, d.BidIncrement, d.RequiredDeposit, d.BuyerPremiumPercent,
               d.AntiSnipingWindowMinutes, d.AntiSnipingExtensionMinutes, d.Notes
        FROM dbo.Auction AS a
        JOIN dbo.AuctionDetails AS d ON d.AuctionId = a.AuctionId
        WHERE a.AuctionId = @Id AND a.IsDeleted = 0;
        """;

    private const string SelectWithDetailsForUpdateQuery = """
        SELECT a.AuctionId, a.ItemId, a.ItemTitle, a.SellerExternalId, a.Status, a.StartsAt, a.EndsAt, a.StartingPrice,
               a.CurrentPrice, a.BidCount, a.LeadingBidderId, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy,
               a.IsDeleted, a.RowVersion,
               d.AuctionId, d.ReservePrice, d.BidIncrement, d.RequiredDeposit, d.BuyerPremiumPercent,
               d.AntiSnipingWindowMinutes, d.AntiSnipingExtensionMinutes, d.Notes
        FROM dbo.Auction AS a WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.AuctionDetails AS d ON d.AuctionId = a.AuctionId
        WHERE a.AuctionId = @Id AND a.IsDeleted = 0;
        """;

    private const string InsertQuery = """
        DECLARE @AuctionId BIGINT;

        INSERT INTO dbo.Auction (ItemId, ItemTitle, SellerExternalId, Status, StartsAt, EndsAt, StartingPrice, CurrentPrice, CreatedBy)
        VALUES (@ItemId, @ItemTitle, @SellerExternalId, @Status, @StartsAt, @EndsAt, @StartingPrice, @CurrentPrice, @Actor);

        SET @AuctionId = SCOPE_IDENTITY();

        INSERT INTO dbo.AuctionDetails (AuctionId, ReservePrice, BidIncrement, RequiredDeposit, BuyerPremiumPercent,
                                        AntiSnipingWindowMinutes, AntiSnipingExtensionMinutes, Notes, CreatedBy)
        VALUES (@AuctionId, @ReservePrice, @BidIncrement, @RequiredDeposit, @BuyerPremiumPercent,
                @AntiSnipingWindowMinutes, @AntiSnipingExtensionMinutes, @Notes, @Actor);

        INSERT INTO dbo.AuctionStatusHistory (AuctionId, OldStatus, NewStatus, Reason, ChangedBy)
        VALUES (@AuctionId, NULL, @Status, N'Auction created', @Actor);

        SELECT @AuctionId;
        """;

    private const string UpdateQuery = """
        DECLARE @Updated INT;

        UPDATE dbo.Auction
        SET ItemTitle = @ItemTitle, StartsAt = @StartsAt, EndsAt = @EndsAt,
            StartingPrice = @StartingPrice, CurrentPrice = @CurrentPrice,
            UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
        WHERE AuctionId = @AuctionId AND RowVersion = @RowVersion AND IsDeleted = 0;

        SET @Updated = @@ROWCOUNT;

        IF @Updated > 0
            UPDATE dbo.AuctionDetails
            SET ReservePrice = @ReservePrice, BidIncrement = @BidIncrement, RequiredDeposit = @RequiredDeposit,
                BuyerPremiumPercent = @BuyerPremiumPercent, Notes = @Notes,
                UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
            WHERE AuctionId = @AuctionId;

        SELECT @Updated;
        """;

    private const string SoftDeleteQuery = """
        UPDATE dbo.Auction
        SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
        WHERE AuctionId = @Id AND IsDeleted = 0;
        """;

    private const string HasUnfinishedQuery = """
        SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Auction
                                      WHERE ItemId = @ItemId AND IsDeleted = 0
                                        AND Status IN (N'Draft', N'Scheduled', N'Active'))
                         THEN 1 ELSE 0 END AS BIT);
        """;

    private const string ApplyBidQuery = """
        UPDATE dbo.Auction
        SET CurrentPrice = @Amount, BidCount = BidCount + 1, LeadingBidderId = @BidderId, EndsAt = @EndsAt,
            UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
        WHERE AuctionId = @AuctionId;
        """;

    public AuctionRepository(IDbSession session) : base(session)
    {
    }

    protected override string SelectByIdSql => SelectByIdQuery;

    protected override string SelectAllSql => SelectAllQuery;

    protected override string SoftDeleteSql => SoftDeleteQuery;

    public override async Task<long> AddAsync(Auction entity, CancellationToken cancellationToken = default)
    {
        var details = entity.Details ?? throw new ArgumentException("Auction.Details must be set.", nameof(entity));

        return await ExecuteScalarAsync<long>(InsertQuery, new
        {
            entity.ItemId,
            entity.ItemTitle,
            entity.SellerExternalId,
            entity.Status,
            entity.StartsAt,
            entity.EndsAt,
            entity.StartingPrice,
            entity.CurrentPrice,
            details.ReservePrice,
            details.BidIncrement,
            details.RequiredDeposit,
            details.BuyerPremiumPercent,
            details.AntiSnipingWindowMinutes,
            details.AntiSnipingExtensionMinutes,
            details.Notes,
            Actor = AuditActor
        }, cancellationToken);
    }

    public override async Task<bool> UpdateAsync(Auction entity, CancellationToken cancellationToken = default)
    {
        var details = entity.Details ?? throw new ArgumentException("Auction.Details must be set.", nameof(entity));

        var updated = await ExecuteScalarAsync<int>(UpdateQuery, new
        {
            entity.AuctionId,
            entity.ItemTitle,
            entity.StartsAt,
            entity.EndsAt,
            entity.StartingPrice,
            entity.CurrentPrice,
            entity.RowVersion,
            details.ReservePrice,
            details.BidIncrement,
            details.RequiredDeposit,
            details.BuyerPremiumPercent,
            details.Notes,
            Actor = AuditActor
        }, cancellationToken);

        return updated > 0;
    }

    public async Task<Auction?> GetWithDetailsAsync(long id, bool forUpdate = false, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<Auction, AuctionDetails, Auction>(
            forUpdate ? SelectWithDetailsForUpdateQuery : SelectWithDetailsQuery,
            (auction, details) =>
            {
                auction.Details = details;
                return auction;
            },
            splitOn: "AuctionId",
            parameters: new { Id = id },
            cancellationToken: cancellationToken);

        return rows.FirstOrDefault();
    }

    public Task<IReadOnlyList<Auction>> GetByStatusAsync(string status, CancellationToken cancellationToken = default) =>
        QueryAsync<Auction>(SelectByStatusQuery, new { Status = status }, cancellationToken: cancellationToken);

    public Task<bool> HasUnfinishedAuctionForItemAsync(long itemId, CancellationToken cancellationToken = default) =>
        ExecuteScalarAsync<bool>(HasUnfinishedQuery, new { ItemId = itemId }, cancellationToken);

    public async Task ApplyBidAsync(long auctionId, decimal amount, long bidderId, DateTime endsAt, CancellationToken cancellationToken = default) =>
        await ExecuteAsync(
            ApplyBidQuery,
            new { AuctionId = auctionId, Amount = amount, BidderId = bidderId, EndsAt = endsAt, Actor = AuditActor },
            cancellationToken: cancellationToken);

    public async Task<string> ChangeStatusAsync(long auctionId, string newStatus, string? reason, CancellationToken cancellationToken = default)
    {
        var result = await QuerySingleOrDefaultAsync<StatusChangeRow>(
            "dbo.sp_ChangeAuctionStatus",
            new { AuctionId = auctionId, NewStatus = newStatus, Actor = AuditActor, Reason = reason, Force = false },
            CommandType.StoredProcedure,
            cancellationToken);

        return result?.NewStatus ?? newStatus;
    }

    private sealed class StatusChangeRow
    {
        public long AuctionId { get; set; }
        public string? OldStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
    }
}
