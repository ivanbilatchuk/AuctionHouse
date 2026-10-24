using Auctions.Bll.DTO;
using Auctions.Domain.Exceptions;
using Auctions.Domain.Models;

namespace Auctions.Bll.Validation;

public sealed class AuctionValidator : IAuctionValidator
{
    public void ValidateCreate(CreateAuctionDto dto, DateTime utcNow)
    {
        var errors = new Dictionary<string, string[]>();

        if (dto.SellerExternalId == Guid.Empty)
        {
            errors[nameof(dto.SellerExternalId)] = new[] { "SellerExternalId must not be an empty GUID." };
        }

        ValidateSchedule(errors, dto.StartsAt, dto.EndsAt, utcNow);
        ValidatePrices(errors, dto.StartingPrice, dto.ReservePrice);
        Throw(errors);
    }

    public void ValidateUpdate(UpdateAuctionDto dto, DateTime utcNow)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateSchedule(errors, dto.StartsAt, dto.EndsAt, utcNow);
        ValidatePrices(errors, dto.StartingPrice, dto.ReservePrice);
        Throw(errors);
    }

    public void ValidateStatus(string status)
    {
        if (!AuctionStatuses.All.Contains(status))
        {
            throw new ValidationException(
                nameof(ChangeAuctionStatusDto.Status),
                $"Unknown status '{status}'. Allowed: {string.Join(", ", AuctionStatuses.All.Order())}.");
        }
    }

    public static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

    private static void ValidateSchedule(Dictionary<string, string[]> errors, DateTime startsAt, DateTime endsAt, DateTime utcNow)
    {
        var starts = ToUtc(startsAt);
        var ends = ToUtc(endsAt);

        if (ends <= starts)
        {
            errors[nameof(CreateAuctionDto.EndsAt)] = new[] { "EndsAt must be later than StartsAt." };
        }
        else if (ends <= utcNow)
        {
            errors[nameof(CreateAuctionDto.EndsAt)] = new[] { "EndsAt must be in the future." };
        }
    }

    private static void ValidatePrices(Dictionary<string, string[]> errors, decimal startingPrice, decimal? reservePrice)
    {
        if (reservePrice.HasValue && reservePrice.Value < startingPrice)
        {
            errors[nameof(CreateAuctionDto.ReservePrice)] = new[] { "ReservePrice must not be lower than StartingPrice." };
        }
    }

    private static void Throw(Dictionary<string, string[]> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException("One or more business validation rules failed.", errors);
        }
    }
}
