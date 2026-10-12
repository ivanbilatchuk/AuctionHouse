# ERD — Проєкт №1 (Auctions, SQL Server + ADO.NET/Dapper)

> Частина документації проєкту AuctionHouse. Повний опис і порядок запуску: [README.md](../README.md).

![erd-p1-auctions](img/erd-p1-auctions.png)

*Якщо діаграма не відображається, зображення вище — той самий ERD; текстове джерело (Mermaid) нижче.*

```mermaid
erDiagram
    Bidder ||--o{ Bid : "1:N places"
    Bidder ||--o{ AuctionRegistration : "M:N via"
    Auction ||--o{ AuctionRegistration : "M:N via"
    Auction ||--|| AuctionDetails : "1:1"
    Auction ||--o{ Bid : "1:N"
    Auction ||--o{ AuctionStatusHistory : "1:N"
    Auction ||--o| Settlement : "1:1 (UNIQUE)"
    Bidder ||--o{ WalletTransaction : "1:N ledger"
    Bidder |o--o{ Auction : "LeadingBidderId"

    Bidder {
        bigint BidderId PK
        uniqueidentifier ExternalUserId UK "Identity, no FK"
        nvarchar DisplayName
        nvarchar Email UK "filtered unique"
        decimal WalletBalance "CHECK >= 0"
        bit IsBlocked
        datetime2 CreatedAt
        nvarchar CreatedBy
        datetime2 UpdatedAt
        nvarchar UpdatedBy
        bit IsDeleted
        rowversion RowVersion
    }
    Auction {
        bigint AuctionId PK
        bigint ItemId "Catalog, no FK"
        nvarchar ItemTitle "duplicate of Catalog.Item.Title"
        uniqueidentifier SellerExternalId "Identity, no FK"
        nvarchar Status "CHECK enum"
        datetime2 StartsAt
        datetime2 EndsAt "CHECK > StartsAt"
        decimal StartingPrice
        decimal CurrentPrice "CHECK >= StartingPrice"
        int BidCount
        bigint LeadingBidderId FK
        bit IsDeleted
        rowversion RowVersion
    }
    AuctionDetails {
        bigint AuctionId PK "PK = FK (1:1)"
        decimal ReservePrice
        decimal BidIncrement "CHECK > 0"
        decimal RequiredDeposit
        decimal BuyerPremiumPercent
        int AntiSnipingWindowMinutes
        int AntiSnipingExtensionMinutes
    }
    Bid {
        bigint BidId PK
        bigint AuctionId FK
        bigint BidderId FK
        decimal Amount "CHECK > 0"
        datetime2 PlacedAt
        bit IsWinning
        bit IsDeleted
        rowversion RowVersion
    }
    AuctionRegistration {
        bigint AuctionId PK "FK"
        bigint BidderId PK "FK"
        decimal DepositAmount "own field"
        nvarchar Status "Active/Released/Captured"
        datetime2 RegisteredAt
        rowversion RowVersion
    }
    WalletTransaction {
        bigint TransactionId PK
        bigint BidderId FK
        bigint AuctionId FK
        nvarchar Type
        decimal Amount
        decimal BalanceAfter
    }
    Settlement {
        bigint SettlementId PK
        bigint AuctionId FK "UNIQUE"
        bigint WinnerBidderId FK
        bigint WinningBidId FK
        decimal HammerPrice
        decimal BuyerPremium
        decimal TotalDue
        decimal DepositApplied
        decimal AmountCharged
    }
    AuctionStatusHistory {
        bigint HistoryId PK
        bigint AuctionId FK
        nvarchar OldStatus
        nvarchar NewStatus
        nvarchar Reason
        datetime2 ChangedAt
        nvarchar ChangedBy
    }
```

**Зв'язки (вимога «щонайменше 4 таблиці зі зв'язками 1:1, 1:N і M:N»).** Усього 8 таблиць.

| Тип | Де реалізовано | Як |
|---|---|---|
| **1:1** | `Auction` ↔ `AuctionDetails` | PK залежної таблиці `AuctionDetails.AuctionId` є одночасно FK на `Auction` |
| **1:1** | `Auction` ↔ `Settlement` | `UNIQUE (AuctionId)` у `Settlement` |
| **1:N** | `Auction` → `Bid`; `Bidder` → `Bid`; `Bidder` → `WalletTransaction`; `Auction` → `AuctionStatusHistory` | FK на боці «багатьох» |
| **M:N** | `Bidder` ↔ `Auction` через `AuctionRegistration` | проміжна таблиця з двома FK, складений PK `(AuctionId, BidderId)`, власні поля `DepositAmount`, `Status` |
