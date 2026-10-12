using Auctions.Bll.DTO;
using Auctions.Dal.Interfaces;
using Auctions.Domain.Exceptions;
using Auctions.Domain.Models;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace Auctions.Bll.Services;

public sealed class BidService : IBidService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BidService> _logger;

    public BidService(IUnitOfWork unitOfWork, IMapper mapper, TimeProvider timeProvider, ILogger<BidService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BidDto>> GetByAuctionAsync(long auctionId, CancellationToken cancellationToken = default)
    {
        await EnsureAuctionExistsAsync(auctionId, cancellationToken);

        return _mapper.Map<IReadOnlyList<BidDto>>(await _unitOfWork.Bids.GetByAuctionAsync(auctionId, cancellationToken));
    }

    public async Task<BidDto> GetByIdAsync(long auctionId, long bidId, CancellationToken cancellationToken = default)
    {
        var bid = await _unitOfWork.Bids.GetByIdAsync(bidId, cancellationToken);

        if (bid is null || bid.AuctionId != auctionId)
        {
            throw new NotFoundException(nameof(Bid), bidId);
        }

        return _mapper.Map<BidDto>(bid);
    }

    public async Task<BidDto> PlaceBidAsync(long auctionId, PlaceBidDto dto, CancellationToken cancellationToken = default)
    {
        var bid = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var auction = await _unitOfWork.Auctions.GetWithDetailsAsync(auctionId, forUpdate: true, cancellationToken)
                          ?? throw new NotFoundException(nameof(Auction), auctionId);
            var details = auction.Details!;

            if (auction.Status != AuctionStatuses.Active)
            {
                throw new BusinessConflictException("Auction is not active.");
            }

            if (now < auction.StartsAt || now >= auction.EndsAt)
            {
                throw new BusinessConflictException("Bidding is outside of the auction time window.");
            }

            var bidder = await _unitOfWork.Bidders.GetByIdAsync(dto.BidderId, cancellationToken)
                         ?? throw new NotFoundException(nameof(Bidder), dto.BidderId);

            if (bidder.IsBlocked || bidder.ExternalUserId == auction.SellerExternalId)
            {
                throw new BusinessConflictException("Bidder is blocked or is the seller of this auction.");
            }

            var registration = await _unitOfWork.Bidders.GetRegistrationAsync(auctionId, dto.BidderId, cancellationToken);
            if (registration is null || registration.Status != "Active")
            {
                throw new BusinessConflictException("Bidder is not registered (no active deposit) for this auction.");
            }

            if (auction.LeadingBidderId == dto.BidderId)
            {
                throw new BusinessConflictException("Bidder already has the highest bid.");
            }

            var minAmount = auction.BidCount == 0 ? auction.StartingPrice : auction.CurrentPrice + details.BidIncrement;
            if (dto.Amount < minAmount)
            {
                throw new BusinessConflictException($"Bid is below the minimum allowed amount {minAmount}.");
            }

            var newEndsAt = auction.EndsAt;
            if (details.AntiSnipingExtensionMinutes > 0
                && (auction.EndsAt - now).TotalSeconds <= details.AntiSnipingWindowMinutes * 60)
            {
                newEndsAt = auction.EndsAt.AddMinutes(details.AntiSnipingExtensionMinutes);
            }

            await _unitOfWork.Bids.ClearWinningFlagAsync(auctionId, cancellationToken);

            var newBid = new Bid
            {
                AuctionId = auctionId,
                BidderId = dto.BidderId,
                Amount = dto.Amount,
                PlacedAt = now,
                IsWinning = true
            };
            newBid.BidId = await _unitOfWork.Bids.AddAsync(newBid, cancellationToken);

            await _unitOfWork.Auctions.ApplyBidAsync(auctionId, dto.Amount, dto.BidderId, newEndsAt, cancellationToken);

            return newBid;
        }, cancellationToken);

        _logger.LogInformation("Bid {BidId} of {Amount} placed on auction {AuctionId}", bid.BidId, bid.Amount, auctionId);

        return _mapper.Map<BidDto>(bid);
    }

    private async Task EnsureAuctionExistsAsync(long auctionId, CancellationToken cancellationToken)
    {
        if (await _unitOfWork.Auctions.GetByIdAsync(auctionId, cancellationToken) is null)
        {
            throw new NotFoundException(nameof(Auction), auctionId);
        }
    }
}
