using Community.Application.Abstractions.Messaging;

namespace Community.Application.Commands.IntegrationEvents.SyncItemTitle;

// Застосування події Каталогу ItemUpdated до копій назви лота (політика дублювання №2):
// expertOpinions.item.title і discussions.item.title. Надсилається обробником integration event у Community.WebApi.
public sealed record SyncItemTitleCommand(Guid EventId, string EventType, long ItemId, string Title, DateTime ChangedAt)
    : ICommand<SyncItemTitleResult>;

public sealed record SyncItemTitleResult(bool IsDuplicate, long OpinionsUpdated, long DiscussionsUpdated);
