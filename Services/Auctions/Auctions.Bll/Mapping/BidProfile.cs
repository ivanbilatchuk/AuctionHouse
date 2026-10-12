using Auctions.Bll.DTO;
using Auctions.Domain.Models;
using AutoMapper;

namespace Auctions.Bll.Mapping;

public class BidProfile : Profile
{
    public BidProfile()
    {
        CreateMap<Bid, BidDto>();
    }
}
