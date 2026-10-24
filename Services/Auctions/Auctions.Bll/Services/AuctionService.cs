using Auctions.Bll.DTO;
using Auctions.Bll.Validation;
using Auctions.Dal.Interfaces;
using Auctions.Domain.Exceptions;
using Auctions.Domain.Models;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace Auctions.Bll.Services;

public sealed class AuctionService : IAuctionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAuctionValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuctionService> _logger;

    public AuctionService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IAuctionValidator validator,
        TimeProvider timeProvider,
        ILogger<AuctionService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _validator = validator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AuctionSummaryDto>> GetAllAsync(string? status, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Auction> auctions;

        if (string.IsNullOrWhiteSpace(status))
        {
            auctions = await _unitOfWork.Auctions.GetAllAsync(cancellationToken);
        }
        else
        {
            _validator.ValidateStatus(status);
            auctions = await _unitOfWork.Auctions.GetByStatusAsync(status, cancellationToken);
        }

        return _mapper.Map<IReadOnlyList<AuctionSummaryDto>>(auctions);
    }

    public async Task<AuctionDto> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var auction = await _unitOfWork.Auctions.GetWithDetailsAsync(id, forUpdate: false, cancellationToken)
                      ?? throw new NotFoundException(nameof(Auction), id);

        return _mapper.Map<AuctionDto>(auction);
    }

    public async Task<AuctionDto> CreateAsync(CreateAuctionDto dto, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        _validator.ValidateCreate(dto, now);

        var auction = _mapper.Map<Auction>(dto);
        auction.StartsAt = AuctionValidator.ToUtc(dto.StartsAt);
        auction.EndsAt = AuctionValidator.ToUtc(dto.EndsAt);

        var auctionId = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (await _unitOfWork.Auctions.HasUnfinishedAuctionForItemAsync(auction.ItemId, cancellationToken))
            {
                throw new BusinessConflictException($"Item {auction.ItemId} already has an unfinished auction.");
            }

            return await _unitOfWork.Auctions.AddAsync(auction, cancellationToken);
        }, cancellationToken);

        _logger.LogInformation("Auction {AuctionId} created for item {ItemId}", auctionId, auction.ItemId);

        return await GetByIdAsync(auctionId, cancellationToken);
    }

    public async Task<AuctionDto> UpdateAsync(long id, UpdateAuctionDto dto, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        _validator.ValidateUpdate(dto, now);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var auction = await _unitOfWork.Auctions.GetWithDetailsAsync(id, forUpdate: true, cancellationToken)
                          ?? throw new NotFoundException(nameof(Auction), id);

            if (auction.Status is not (AuctionStatuses.Draft or AuctionStatuses.Scheduled))
            {
                throw new BusinessConflictException("Only Draft or Scheduled auctions can be edited.");
            }

            if (auction.BidCount > 0)
            {
                throw new BusinessConflictException("Auction already has bids and cannot be edited.");
            }

            _mapper.Map(dto, auction);
            _mapper.Map(dto, auction.Details!);
            auction.StartsAt = AuctionValidator.ToUtc(dto.StartsAt);
            auction.EndsAt = AuctionValidator.ToUtc(dto.EndsAt);

            if (!await _unitOfWork.Auctions.UpdateAsync(auction, cancellationToken))
            {
                throw new BusinessConflictException("Concurrency conflict: the auction was modified by another user.");
            }

            return true;
        }, cancellationToken);

        _logger.LogInformation("Auction {AuctionId} updated", id);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<AuctionDto> ChangeStatusAsync(long id, ChangeAuctionStatusDto dto, CancellationToken cancellationToken = default)
    {
        _validator.ValidateStatus(dto.Status);

        var newStatus = await _unitOfWork.Auctions.ChangeStatusAsync(id, dto.Status, dto.Reason, cancellationToken);

        _logger.LogInformation("Auction {AuctionId} status changed to {Status}", id, newStatus);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var auction = await _unitOfWork.Auctions.GetWithDetailsAsync(id, forUpdate: true, cancellationToken)
                          ?? throw new NotFoundException(nameof(Auction), id);

            if (auction.Status is not (AuctionStatuses.Draft or AuctionStatuses.Cancelled) || auction.BidCount > 0)
            {
                throw new BusinessConflictException("Only Draft or Cancelled auctions without bids can be deleted.");
            }

            return await _unitOfWork.Auctions.DeleteAsync(id, cancellationToken);
        }, cancellationToken);

        _logger.LogInformation("Auction {AuctionId} deleted (soft)", id);
    }
}
