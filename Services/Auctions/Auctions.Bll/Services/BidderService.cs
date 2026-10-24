using Auctions.Bll.DTO;
using Auctions.Dal.Interfaces;
using Auctions.Domain.Exceptions;
using Auctions.Domain.Models;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace Auctions.Bll.Services;

public sealed class BidderService : IBidderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<BidderService> _logger;

    public BidderService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<BidderService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BidderDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _mapper.Map<IReadOnlyList<BidderDto>>(await _unitOfWork.Bidders.GetAllAsync(cancellationToken));

    public async Task<BidderDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var bidder = await _unitOfWork.Bidders.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException(nameof(Bidder), id);

        return _mapper.Map<BidderDto>(bidder);
    }

    public async Task<BidderDto> CreateAsync(CreateBidderDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.ExternalUserId == Guid.Empty)
        {
            throw new ValidationException(nameof(dto.ExternalUserId), "ExternalUserId must not be an empty GUID.");
        }

        if (await _unitOfWork.Bidders.GetByEmailAsync(dto.Email, cancellationToken) is not null)
        {
            throw new BusinessConflictException($"A bidder with email '{dto.Email}' already exists.");
        }

        var bidderId = await _unitOfWork.Bidders.AddAsync(_mapper.Map<Bidder>(dto), cancellationToken);
        _logger.LogInformation("Bidder {BidderId} created", bidderId);

        return await GetByIdAsync(bidderId, cancellationToken);
    }

    public async Task<RegistrationDto> RegisterForAuctionAsync(
        long bidderId,
        RegisterForAuctionDto dto,
        CancellationToken cancellationToken = default)
    {
        var registration = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var auction = await _unitOfWork.Auctions.GetWithDetailsAsync(dto.AuctionId, forUpdate: true, cancellationToken)
                          ?? throw new NotFoundException(nameof(Auction), dto.AuctionId);

            var bidder = await _unitOfWork.Bidders.GetForUpdateAsync(bidderId, cancellationToken)
                         ?? throw new NotFoundException(nameof(Bidder), bidderId);

            if (auction.Status is not (AuctionStatuses.Scheduled or AuctionStatuses.Active))
            {
                throw new BusinessConflictException("Registration is available only for Scheduled or Active auctions.");
            }

            if (bidder.IsBlocked || bidder.ExternalUserId == auction.SellerExternalId)
            {
                throw new BusinessConflictException("Bidder is blocked or is the seller of this auction.");
            }

            if (await _unitOfWork.Bidders.GetRegistrationAsync(auction.AuctionId, bidderId, cancellationToken) is not null)
            {
                throw new BusinessConflictException("Bidder is already registered for this auction.");
            }

            var deposit = auction.Details!.RequiredDeposit;
            if (bidder.WalletBalance < deposit)
            {
                throw new BusinessConflictException("Insufficient wallet balance for the required deposit.");
            }

            if (deposit > 0)
            {
                await _unitOfWork.Bidders.UpdateWalletAsync(
                    bidderId, bidder.WalletBalance - deposit, "DepositHold", auction.AuctionId, -deposit, cancellationToken);
            }

            var entity = new AuctionRegistration
            {
                AuctionId = auction.AuctionId,
                BidderId = bidderId,
                DepositAmount = deposit,
                Status = "Active"
            };
            await _unitOfWork.Bidders.AddRegistrationAsync(entity, cancellationToken);

            return entity;
        }, cancellationToken);

        _logger.LogInformation("Bidder {BidderId} registered for auction {AuctionId}", bidderId, dto.AuctionId);

        var saved = await _unitOfWork.Bidders.GetRegistrationAsync(registration.AuctionId, bidderId, cancellationToken);
        return _mapper.Map<RegistrationDto>(saved ?? registration);
    }
}
