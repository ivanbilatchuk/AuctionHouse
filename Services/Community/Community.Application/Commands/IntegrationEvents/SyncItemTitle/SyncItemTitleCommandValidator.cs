using FluentValidation;

namespace Community.Application.Commands.IntegrationEvents.SyncItemTitle;

// Некоректні дані події від повтору не виправляться: ValidationException -> повідомлення одразу в DLQ.
public sealed class SyncItemTitleCommandValidator : AbstractValidator<SyncItemTitleCommand>
{
    public SyncItemTitleCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ChangedAt).NotEmpty();
    }
}
