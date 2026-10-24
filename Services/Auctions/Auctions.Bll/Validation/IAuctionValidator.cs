using Auctions.Bll.DTO;

namespace Auctions.Bll.Validation;

public interface IAuctionValidator
{
    void ValidateCreate(CreateAuctionDto dto, DateTime utcNow);

    void ValidateUpdate(UpdateAuctionDto dto, DateTime utcNow);

    void ValidateStatus(string status);
}
