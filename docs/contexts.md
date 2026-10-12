# Предметна область, контексти, політика дублювання, посилання без FK

> Частина документації проєкту AuctionHouse. Повний опис і порядок запуску: [README.md](../README.md).

## 1. Предметна область і три контексти

**Домен.** Онлайн-аукціонний дім для колекціонерів (колекційні картки, поштові марки, вінілові платівки, монети).
*Продавець* виставляє предмет; *учасники торгів* реєструються на аукціон, вносять заставу з внутрішнього гаманця
і роблять ставки; після закриття торгів переможець оплачує лот, а *експерти й спільнота* оцінюють автентичність
предмета, обговорюють лот і формують історію цін та репутацію продавця.

Предметну область розділено на **три незалежні контексти (Bounded Contexts)**. Кожен має власну БД, власну модель і логіку;
жодних спільних схем і міжсервісних JOIN-ів. **Кожна сутність належить рівно одному контексту.**

| Контекст | БД | Агрегати (root + частини) | Сутності / колекції | Життєвий цикл |
|---|---|---|---|---|
| **Catalog** | `AuctionHouse_Catalog` (SQL Server) | `Item` (+ `ItemDetail`, `ItemImage`, `ItemTag`); `Category`; `Tag` | `Category`, `Item`, `ItemDetail`, `ItemImage`, `Tag`, `ItemTag` | Draft → Listed → InAuction → Sold / Withdrawn |
| **Auctions** | `AuctionHouse_Auctions` (SQL Server) | `Auction` (+ `AuctionDetails`, `Bid`, `AuctionRegistration`, `Settlement`, `AuctionStatusHistory`); `Bidder` (+ `WalletTransaction`) | `Bidder`, `Auction`, `AuctionDetails`, `Bid`, `AuctionRegistration`, `WalletTransaction`, `Settlement`, `AuctionStatusHistory` | Draft → Scheduled → Active → Closed → Settled (або Unsold / Cancelled) |
| **Community** | `AuctionHouse_Community` (MongoDB) | `expertOpinions`; `discussions` (+ вбудовані коментарі); `priceHistory` (+ вбудовані продажі); `sellerRatings` (+ вбудовані відгуки) | `expertOpinions`, `discussions`, `priceHistory`, `sellerRatings` | створюється під час торгів, доповнюється після розрахунку |

Користувачі (продавці, учасники, експерти) живуть у зовнішній системі ідентифікації (Identity / Keycloak, лабораторна № 9).
Усі контексти посилаються на користувача його **GUID** (`ExternalUserId`), а не FK.


## 2. Політика дублювання даних і узгодженості

Дублювання допустиме й очікуване: воно розв'язує сервіси та пришвидшує читання. Для кожного дубльованого поля визначено
**власника** (єдине джерело істини) і **механізм підтримки узгодженості**. Модель узгодженості — *eventual consistency*:
контекст-власник публікує подію (RabbitMQ, лабораторна № 8), споживачі оновлюють свої копії; споживачі ідемпотентні
(обробка повторної події не змінює результат).

| № | Дубльоване поле (де зберігається) | Власник (джерело істини) | Навіщо дублюємо | Механізм підтримки узгодженості | Знімок чи події |
|---|---|---|---|---|---|
| 1 | `Auctions.Auction.ItemTitle` | `Catalog.Item.Title` | Список і вітрина аукціонів без звернення до Catalog | Знімок під час `sp_CreateAuction` + оновлення за подією `ItemUpdated` (`UPDATE … WHERE ItemId = @ItemId`) | **Знімок + події** |
| 2 | `expertOpinions.itemTitle`, `discussions.itemTitle`, `priceHistory.itemTitle` (Community) | `Catalog.Item.Title` | Показ висновку/обговорення без звернення до Catalog | Знімок під час створення документа + `updateMany({itemId})` за подією `ItemUpdated` | **Знімок + події** |
| 3 | `expertOpinions.categoryKind`, `priceHistory.categoryKind` (Community) | `Catalog.Category.AttributeKind` | Вибір структури поля `attributes` і групування аналітики без запиту до Catalog | Знімок під час створення (категорія предмета змінюється рідко); за зміни — подія `ItemUpdated` | **Знімок** |
| 4 | `Catalog.Item.Status` (стани `InAuction`, `Sold`) | `Auctions.Auction.Status` | Каталог показує, чи предмет у торгах/проданий, без запиту до Auctions | Проєкція за подіями `AuctionActivated`, `AuctionClosed`, `AuctionSettled`, `AuctionCancelled` | **Події** |
| 5 | `priceHistory.sales[].finalPrice` і `priceHistory.stats` (Community) | `Auctions.Settlement.HammerPrice` | Аналітика цін без звернення до Auctions | Подія `AuctionSettled`: `$push` у `sales` (унікальність за `auctionId` робить обробку ідемпотентною) і перерахунок `stats` | **Події** |
| 6 | `Auctions.Bidder.DisplayName`; `expertOpinions.expert.displayName`; `discussions.comments[].author.displayName`; `sellerRatings.sellerName`, `recentReviews[].buyerName` | Identity (зовнішня система) | Показ імені без запиту до Identity | Знімок під час першої появи користувача + оновлення за подією `UserProfileUpdated` | **Знімок + події** |

Допомагають узгодженості й **локальні гарантії**: у кожній БД діють власні `UNIQUE`/`CHECK`, а всі зміни агрегату виконуються в одній
транзакції (SQL) або в межах одного документа (MongoDB).


## 3. Три самостійні бази без FOREIGN KEY між ними

Посилання на сутність іншого контексту — це **звичайний стовпець/поле з її Id, без зовнішнього ключа**
(СУБД різні, бази автономні, цілісність забезпечується подіями, а не FK).

| Звідки (контекст.об'єкт.поле) | Куди посилається | Тип поля | Примітка |
|---|---|---|---|
| Auctions.`Auction.ItemId` | Catalog.`Item.ItemId` | `BIGINT` | без FK; дублює `ItemTitle` |
| Auctions.`Auction.SellerExternalId` | Identity: користувач-продавець | `UNIQUEIDENTIFIER` | без FK |
| Auctions.`Bidder.ExternalUserId` | Identity: користувач | `UNIQUEIDENTIFIER`, `UNIQUE` | без FK |
| Catalog.`Item.SellerExternalId` | Identity: користувач-продавець | `UNIQUEIDENTIFIER` | без FK |
| Community.`expertOpinions.itemId` | Catalog.`Item.ItemId` | `long` | без посилальної цілісності |
| Community.`expertOpinions.auctionId`, `discussions.auctionId` | Auctions.`Auction.AuctionId` | `long` | без посилальної цілісності |
| Community.`discussions.itemId`, `priceHistory.itemId` | Catalog.`Item.ItemId` | `long` | без посилальної цілісності |
| Community.`…expert.userId`, `…author.userId`, `sellerRatings.sellerId`, `…buyerId` | Identity: користувач | `UUID` (BinData 4) | без посилальної цілісності |

Усі `FOREIGN KEY` в `schema.sql` посилаються **лише на таблиці своєї ж бази** (див. запити перевірки в розділі 9).

