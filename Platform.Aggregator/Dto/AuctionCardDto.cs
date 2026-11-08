namespace Platform.Aggregator.Dto;

// Картка аукціону для сторінки лота: аукціон (Auctions) + лот (Catalog) + висновки експертів (Community) + останні ставки.
// Будь-яка частина, крім аукціону, може бути відсутня — тоді причина потрапляє у Warnings.
public sealed record AuctionCardDto(
    AuctionInfoDto Auction,
    LotDto? Lot,
    IReadOnlyList<ExpertOpinionDto> ExpertOpinions,
    IReadOnlyList<BidDto> RecentBids,
    AuctionCardSummaryDto? Summary,
    IReadOnlyList<string> Warnings);

// Підсумок для покупця: скільки ставити далі, чи досягнуто резерву, як ціна співвідноситься з оцінкою та що кажуть експерти.
public sealed record AuctionCardSummaryDto(
    decimal CurrentPrice,
    decimal NextMinimumBid,
    string? Currency,
    bool? ReserveMet,
    string? PriceVsEstimate,
    int ExpertOpinionsCount,
    string? ExpertConsensus,
    double? AverageConfidence,
    int TotalBids,
    TimeSpan? TimeLeft);
