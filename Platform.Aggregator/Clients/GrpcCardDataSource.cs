using System.Globalization;
using Auctions.Grpc;
using Catalog.Grpc;
using Community.Grpc;
using Grpc.Core;
using Platform.Aggregator.Dto;
using BidDto = Platform.Aggregator.Dto.BidDto;
using ExpertOpinionDto = Platform.Aggregator.Dto.ExpertOpinionDto;

namespace Platform.Aggregator.Clients;

// Режим ?transport=grpc (за замовчуванням): згенеровані gRPC-клієнти (HTTP/2, protobuf).
// Адреси http://_grpc.<ресурс> розв'язує service discovery; X-Correlation-Id потрапляє в metadata
// через CorrelationIdHandler з ServiceDefaults (gRPC-клієнти використовують той самий IHttpClientFactory).
public sealed class GrpcCardDataSource(
    AuctionsService.AuctionsServiceClient auctions,
    CatalogService.CatalogServiceClient catalog,
    CommunityService.CommunityServiceClient community,
    ILogger<GrpcCardDataSource> logger) : ICardDataSource
{
    public const string Name = "grpc";
    private const int OpinionsPageSize = 50;

    public string Transport => Name;

    public async Task<AuctionInfoDto?> GetAuctionAsync(long auctionId, CancellationToken ct)
    {
        try
        {
            var a = await auctions.GetAuctionAsync(new GetAuctionRequest { Id = auctionId }, cancellationToken: ct);
            return new AuctionInfoDto
            {
                AuctionId = a.AuctionId,
                ItemId = a.ItemId,
                ItemTitle = a.ItemTitle,
                Status = a.Status,
                StartsAt = a.StartsAt.ToDateTime(),
                EndsAt = a.EndsAt.ToDateTime(),
                StartingPrice = Money(a.StartingPrice),
                CurrentPrice = Money(a.CurrentPrice),
                BidCount = a.BidCount,
                LeadingBidderId = a.HasLeadingBidderId ? a.LeadingBidderId : null,
                ReservePrice = a.HasReservePrice ? Money(a.ReservePrice) : null,
                BidIncrement = Money(a.BidIncrement),
                BuyerPremiumPercent = Money(a.BuyerPremiumPercent)
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogDebug("gRPC GetAuction {AuctionId}: NotFound", auctionId);
            return null;
        }
    }

    public async Task<LotDto?> GetLotAsync(long itemId, CancellationToken ct)
    {
        try
        {
            var item = await catalog.GetItemAsync(new GetItemRequest { Id = itemId }, cancellationToken: ct);
            return new LotDto
            {
                Id = item.Id,
                Sku = item.Sku,
                Title = item.Title,
                CategoryName = string.IsNullOrEmpty(item.CategoryName) ? null : item.CategoryName,
                EstimateLow = Money(item.EstimateLow),
                EstimateHigh = Money(item.EstimateHigh),
                Currency = item.Currency,
                Status = item.Status,
                Detail = item.Detail is null ? null : new LotDetailDto
                {
                    Condition = item.Detail.Condition,
                    ProductionYear = item.Detail.HasProductionYear ? (short)item.Detail.ProductionYear : null,
                    CountryOfOrigin = item.Detail.HasCountryOfOrigin ? item.Detail.CountryOfOrigin : null,
                    Provenance = item.Detail.HasProvenance ? item.Detail.Provenance : null,
                    Description = item.Detail.HasDescription ? item.Detail.Description : null
                },
                Images = item.Images.Select(i => new LotImageDto { Url = i.Url, IsPrimary = i.IsPrimary }).ToList()
            };
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogDebug("gRPC GetItem {ItemId}: NotFound", itemId);
            return null;
        }
    }

    public async Task<IReadOnlyList<ExpertOpinionDto>> GetExpertOpinionsAsync(long itemId, CancellationToken ct)
    {
        var reply = await community.GetOpinionsForItemAsync(
            new GetOpinionsForItemRequest { ItemId = itemId, PageSize = OpinionsPageSize }, cancellationToken: ct);

        return reply.Opinions.Select(o => new ExpertOpinionDto
        {
            Id = o.Id,
            ItemId = o.ItemId,
            CategoryKind = o.CategoryKind,
            ExpertName = o.ExpertName,
            Verdict = o.Verdict,
            Confidence = o.Confidence,
            CreatedAt = o.CreatedAt.ToDateTime()
        }).ToList();
    }

    public async Task<IReadOnlyList<BidDto>> GetRecentBidsAsync(long auctionId, int limit, CancellationToken ct)
    {
        var reply = await auctions.GetBidsAsync(new GetBidsRequest { AuctionId = auctionId, Limit = limit }, cancellationToken: ct);

        return reply.Bids.Select(b => new BidDto
        {
            BidId = b.BidId,
            BidderId = b.BidderId,
            Amount = Money(b.Amount),
            PlacedAt = b.PlacedAt.ToDateTime(),
            IsWinning = b.IsWinning
        }).ToList();
    }

    private static decimal Money(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
