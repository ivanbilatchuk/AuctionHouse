using System.Globalization;
using Auctions.Bll.DTO;
using Auctions.Bll.Services;
using Auctions.Domain.Exceptions;
using Auctions.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Auctions.Api.Grpc;

// Тонкий адаптер gRPC над IAuctionService/IBidService (кешуючі декоратори з Auctions.Bll/Caching).
// Очікувані помилки -> InvalidArgument / NotFound, а не Internal.
public sealed class AuctionsGrpcService(IAuctionService auctions, IBidService bids) : AuctionsService.AuctionsServiceBase
{
    private const int MaxBids = 100;

    public override async Task<AuctionReply> GetAuction(GetAuctionRequest request, ServerCallContext context)
    {
        if (request.Id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "id must be positive."));

        try
        {
            return ToReply(await auctions.GetByIdAsync(request.Id, context.CancellationToken));
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
    }

    public override async Task<GetBidsReply> GetBids(GetBidsRequest request, ServerCallContext context)
    {
        if (request.AuctionId <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "auction_id must be positive."));
        if (request.Limit is < 0 or > MaxBids)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"limit must be in 0..{MaxBids}."));

        IReadOnlyList<BidDto> all;
        try
        {
            all = await bids.GetByAuctionAsync(request.AuctionId, context.CancellationToken);
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }

        var limit = request.Limit == 0 ? MaxBids : request.Limit;
        var reply = new GetBidsReply { TotalCount = all.Count };
        reply.Bids.AddRange(all
            .OrderByDescending(b => b.PlacedAt)
            .Take(limit)
            .Select(b => new BidReply
            {
                BidId = b.BidId,
                BidderId = b.BidderId,
                Amount = Money(b.Amount),
                PlacedAt = ToTimestamp(b.PlacedAt),
                IsWinning = b.IsWinning
            }));

        return reply;
    }

    private static AuctionReply ToReply(AuctionDto a)
    {
        var reply = new AuctionReply
        {
            AuctionId = a.AuctionId,
            ItemId = a.ItemId,
            ItemTitle = a.ItemTitle,
            Status = a.Status,
            StartsAt = ToTimestamp(a.StartsAt),
            EndsAt = ToTimestamp(a.EndsAt),
            StartingPrice = Money(a.StartingPrice),
            CurrentPrice = Money(a.CurrentPrice),
            BidCount = a.BidCount,
            BidIncrement = Money(a.BidIncrement),
            BuyerPremiumPercent = Money(a.BuyerPremiumPercent)
        };

        if (a.LeadingBidderId is { } leader) reply.LeadingBidderId = leader;
        if (a.ReservePrice is { } reserve) reply.ReservePrice = Money(reserve);

        return reply;
    }

    private static string Money(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    // Дати в БД зберігаються в UTC (Kind = Unspecified після Dapper) — Timestamp вимагає Kind = Utc.
    private static Timestamp ToTimestamp(DateTime value) => Timestamp.FromDateTime(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
