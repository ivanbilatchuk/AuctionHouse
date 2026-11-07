using Community.Domain.Repositories;
using FluentValidation;

namespace Community.Application.Commands.Discussions.CreateDiscussion;

public sealed class CreateDiscussionCommandValidator : AbstractValidator<CreateDiscussionCommand>
{
    public CreateDiscussionCommandValidator(IDiscussionRepository repository)
    {
        RuleFor(x => x.AuctionId).GreaterThan(0)
            // Асинхронне правило з запитом до MongoDB: одне обговорення на аукціон.
            .MustAsync(async (auctionId, ct) => !await repository.ExistsForAuctionAsync(auctionId, ct))
            .WithMessage("A discussion for this auction already exists.");
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.ItemTitle).NotEmpty().MaximumLength(200);
    }
}
