using Auctions.Bll.DTO;

namespace Auctions.Bll.Services;

public interface IAuctionService
{
    Task<IReadOnlyList<AuctionSummaryDto>> GetAllAsync(string? status, CancellationToken cancellationToken = default);

    Task<AuctionDto> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<AuctionDto> CreateAsync(CreateAuctionDto dto, CancellationToken cancellationToken = default);

    Task<AuctionDto> UpdateAsync(long id, UpdateAuctionDto dto, CancellationToken cancellationToken = default);

    Task<AuctionDto> ChangeStatusAsync(long id, ChangeAuctionStatusDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
}
