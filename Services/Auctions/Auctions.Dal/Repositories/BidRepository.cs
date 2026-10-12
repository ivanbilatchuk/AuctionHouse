using System.Data;
using Auctions.Dal.Interfaces;
using Auctions.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Auctions.Dal.Repositories;

public sealed class BidRepository : IBidRepository
{
    private const string SelectByIdQuery = """
        SELECT BidId, AuctionId, BidderId, Amount, PlacedAt, IsWinning
        FROM dbo.Bid
        WHERE BidId = @BidId AND IsDeleted = 0;
        """;

    private const string SelectByAuctionQuery = """
        SELECT BidId, AuctionId, BidderId, Amount, PlacedAt, IsWinning
        FROM dbo.Bid
        WHERE AuctionId = @AuctionId AND IsDeleted = 0
        ORDER BY Amount DESC;
        """;

    private const string SelectHighestQuery = """
        SELECT TOP (1) BidId, AuctionId, BidderId, Amount, PlacedAt, IsWinning
        FROM dbo.Bid
        WHERE AuctionId = @AuctionId AND IsDeleted = 0
        ORDER BY Amount DESC;
        """;

    private const string ClearWinningQuery = """
        UPDATE dbo.Bid
        SET IsWinning = 0, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @Actor
        WHERE AuctionId = @AuctionId AND IsWinning = 1;
        """;

    private const string InsertQuery = """
        INSERT INTO dbo.Bid (AuctionId, BidderId, Amount, PlacedAt, IsWinning, CreatedBy)
        OUTPUT INSERTED.BidId
        VALUES (@AuctionId, @BidderId, @Amount, @PlacedAt, @IsWinning, @Actor);
        """;

    private const string AuditActor = "Auctions.Api";

    private readonly IDbSession _session;

    public BidRepository(IDbSession session)
    {
        _session = session;
    }

    public async Task<Bid?> GetByIdAsync(long bidId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = await CreateCommandAsync(SelectByIdQuery, cancellationToken);
            command.Parameters.Add(new SqlParameter("@BidId", SqlDbType.BigInt) { Value = bidId });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    public async Task<IReadOnlyList<Bid>> GetByAuctionAsync(long auctionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = await CreateCommandAsync(SelectByAuctionQuery, cancellationToken);
            command.Parameters.Add(new SqlParameter("@AuctionId", SqlDbType.BigInt) { Value = auctionId });

            var bids = new List<Bid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                bids.Add(Map(reader));
            }

            return bids;
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    public async Task<Bid?> GetHighestAsync(long auctionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = await CreateCommandAsync(SelectHighestQuery, cancellationToken);
            command.Parameters.Add(new SqlParameter("@AuctionId", SqlDbType.BigInt) { Value = auctionId });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? Map(reader) : null;
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    public async Task ClearWinningFlagAsync(long auctionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = await CreateCommandAsync(ClearWinningQuery, cancellationToken);
            command.Parameters.Add(new SqlParameter("@AuctionId", SqlDbType.BigInt) { Value = auctionId });
            command.Parameters.Add(new SqlParameter("@Actor", SqlDbType.NVarChar, 100) { Value = AuditActor });

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    public async Task<long> AddAsync(Bid bid, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = await CreateCommandAsync(InsertQuery, cancellationToken);
            command.Parameters.Add(new SqlParameter("@AuctionId", SqlDbType.BigInt) { Value = bid.AuctionId });
            command.Parameters.Add(new SqlParameter("@BidderId", SqlDbType.BigInt) { Value = bid.BidderId });
            command.Parameters.Add(new SqlParameter("@Amount", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = bid.Amount });
            command.Parameters.Add(new SqlParameter("@PlacedAt", SqlDbType.DateTime2) { Scale = 3, Value = bid.PlacedAt });
            command.Parameters.Add(new SqlParameter("@IsWinning", SqlDbType.Bit) { Value = bid.IsWinning });
            command.Parameters.Add(new SqlParameter("@Actor", SqlDbType.NVarChar, 100) { Value = AuditActor });

            var id = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(id);
        }
        catch (SqlException ex)
        {
            throw SqlExceptionTranslator.Translate(ex);
        }
    }

    private async Task<SqlCommand> CreateCommandAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = await _session.GetOpenConnectionAsync(cancellationToken);

        return new SqlCommand(sql, connection, _session.Transaction)
        {
            CommandTimeout = _session.CommandTimeoutSeconds
        };
    }

    private static Bid Map(SqlDataReader reader) => new()
    {
        BidId = reader.GetInt64(reader.GetOrdinal("BidId")),
        AuctionId = reader.GetInt64(reader.GetOrdinal("AuctionId")),
        BidderId = reader.GetInt64(reader.GetOrdinal("BidderId")),
        Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
        PlacedAt = reader.GetDateTime(reader.GetOrdinal("PlacedAt")),
        IsWinning = reader.GetBoolean(reader.GetOrdinal("IsWinning"))
    };
}
