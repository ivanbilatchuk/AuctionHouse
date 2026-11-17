using Platform.Aggregator.Dto;

namespace Platform.Aggregator.Clients;

// Джерело даних картки: gRPC (за замовчуванням) або HTTP/JSON (для порівняння затримок).
// null у GetAuctionAsync/GetLotAsync — сервіс відповів "не знайдено".
public interface ICardDataSource
{
    string Transport { get; }

    Task<AuctionInfoDto?> GetAuctionAsync(long auctionId, CancellationToken ct);

    Task<LotDto?> GetLotAsync(long itemId, CancellationToken ct);

    Task<IReadOnlyList<ExpertOpinionDto>> GetExpertOpinionsAsync(long itemId, CancellationToken ct);

    Task<IReadOnlyList<BidDto>> GetRecentBidsAsync(long auctionId, int limit, CancellationToken ct);
}
