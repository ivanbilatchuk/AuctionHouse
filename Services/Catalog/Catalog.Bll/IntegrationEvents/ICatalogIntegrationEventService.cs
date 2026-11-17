using Catalog.Domain.Entities;

namespace Catalog.Bll.IntegrationEvents;

// Публікація integration events Каталогу (реалізація — Catalog.Api/IntegrationEvents/CatalogIntegrationEventService.cs).
// BLL викликає її ЛИШЕ після успішного SaveChangesAsync: якщо транзакція БД не вдалася, подія не публікується.
// Між збереженням і публікацією застосунок може впасти — подію буде втрачено; гарантію доставки дав би
// Transactional Outbox (у межах лаби не реалізовано, див. README §17).
public interface ICatalogIntegrationEventService
{
    // Назва предмета (Title) дублюється в Auctions (Auction.ItemTitle) і Community (item.title) — політика дублювання №1–2.
    Task PublishItemUpdatedAsync(Item item, string previousTitle, CancellationToken ct = default);
}
