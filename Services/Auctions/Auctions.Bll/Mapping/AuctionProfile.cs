using Auctions.Bll.DTO;
using Auctions.Domain.Models;
using AutoMapper;

namespace Auctions.Bll.Mapping;

public class AuctionProfile : Profile
{
    public AuctionProfile()
    {
        CreateMap<Auction, AuctionDto>()
            .ForMember(d => d.ReservePrice, o => o.MapFrom(s => s.Details != null ? s.Details.ReservePrice : null))
            .ForMember(d => d.BidIncrement, o => o.MapFrom(s => s.Details != null ? s.Details.BidIncrement : 0m))
            .ForMember(d => d.RequiredDeposit, o => o.MapFrom(s => s.Details != null ? s.Details.RequiredDeposit : 0m))
            .ForMember(d => d.BuyerPremiumPercent, o => o.MapFrom(s => s.Details != null ? s.Details.BuyerPremiumPercent : 0m))
            .ForMember(d => d.AntiSnipingWindowMinutes, o => o.MapFrom(s => s.Details != null ? s.Details.AntiSnipingWindowMinutes : 0))
            .ForMember(d => d.AntiSnipingExtensionMinutes, o => o.MapFrom(s => s.Details != null ? s.Details.AntiSnipingExtensionMinutes : 0))
            .ForMember(d => d.Notes, o => o.MapFrom(s => s.Details != null ? s.Details.Notes : null));

        CreateMap<Auction, AuctionSummaryDto>();

        CreateMap<CreateAuctionDto, Auction>(MemberList.None)
            .ForMember(d => d.Status, o => o.MapFrom(_ => AuctionStatuses.Draft))
            .ForMember(d => d.CurrentPrice, o => o.MapFrom(s => s.StartingPrice))
            .ForMember(d => d.Details, o => o.MapFrom(s => new AuctionDetails
            {
                ReservePrice = s.ReservePrice,
                BidIncrement = s.BidIncrement,
                RequiredDeposit = s.RequiredDeposit,
                BuyerPremiumPercent = s.BuyerPremiumPercent,
                Notes = s.Notes
            }));

        CreateMap<UpdateAuctionDto, Auction>(MemberList.None)
            .ForMember(d => d.CurrentPrice, o => o.MapFrom(s => s.StartingPrice))
            .ForMember(d => d.Details, o => o.Ignore());

        CreateMap<UpdateAuctionDto, AuctionDetails>(MemberList.None);
    }
}
