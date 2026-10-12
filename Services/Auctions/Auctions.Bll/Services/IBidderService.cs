using Auctions.Bll.DTO;

namespace Auctions.Bll.Services;

public interface IBidderService
{
    Task<IReadOnlyList<BidderDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<BidderDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<BidderDto> CreateAsync(CreateBidderDto dto, CancellationToken cancellationToken = default);

    Task<RegistrationDto> RegisterForAuctionAsync(long bidderId, RegisterForAuctionDto dto, CancellationToken cancellationToken = default);
}
