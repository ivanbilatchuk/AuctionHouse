using Auctions.Bll.DTO;
using Auctions.Domain.Models;
using AutoMapper;

namespace Auctions.Bll.Mapping;

public class BidderProfile : Profile
{
    public BidderProfile()
    {
        CreateMap<Bidder, BidderDto>();

        CreateMap<CreateBidderDto, Bidder>(MemberList.None)
            .ForMember(d => d.WalletBalance, o => o.MapFrom(s => s.InitialBalance));

        CreateMap<AuctionRegistration, RegistrationDto>();
    }
}
