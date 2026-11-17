using Platform.EventBus.Abstractions;

namespace Auctions.Api.IntegrationEvents.Events;

// Auctions (власник статусу) -> Catalog: статус аукціону змінився. Exchange "auctions-events",
// routing key "auction.<новий статус>" (auction.scheduled, auction.active, auction.closed, auction.unsold, auction.cancelled) —
// кожен споживач прив'язує чергу лише до потрібних статусів. Самодостатня: ціна, кількість ставок і лідер — для
// споживачів, яким потрібен результат торгів (історія цін, лаба 1 — політика №5), без запиту до Auctions.
public sealed record AuctionStatusChangedIntegrationEvent(
    long AuctionId,
    long ItemId,
    string ItemTitle,
    string? OldStatus,
    string NewStatus,
    decimal CurrentPrice,
    int BidCount,
    long? LeadingBidderId) : IntegrationEvent;
