using Auctions.Bll.DTO;

namespace Auctions.Bll.IntegrationEvents;

// Публікація integration events сервісу аукціонів (реалізація — Auctions.Api/IntegrationEvents/AuctionsIntegrationEventService.cs).
// BLL викликає її ЛИШЕ після успішного коміту транзакції (sp_ChangeAuctionStatus виконує зміну атомарно):
// якщо зміна в БД не вдалася — виняток, і до публікації справа не доходить.
public interface IAuctionsIntegrationEventService
{
    // Статус аукціону дублюється в Catalog (Item.Status: InAuction / Sold) — політика дублювання №4.
    Task PublishAuctionStatusChangedAsync(AuctionDto auction, string? oldStatus, DateTime changedAt, CancellationToken cancellationToken = default);
}
