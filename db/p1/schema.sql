/* AuctionHouse_Auctions (Проєкт №1: SQL Server + ADO.NET/Dapper). Порядок: schema.sql -> procedures.sql -> seed.sql. Скрипт ідемпотентний.
   PK: BIGINT IDENTITY -- послідовний кластеризований ключ, компактні FK та індекси (GUID лише для ExternalUserId із зовнішньої системи).
   Посилання на інші контексти (ItemId, SellerExternalId, ExternalUserId) -- звичайні стовпці БЕЗ FOREIGN KEY. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'AuctionHouse_Auctions') IS NULL
    CREATE DATABASE AuctionHouse_Auctions;
GO

USE AuctionHouse_Auctions;
GO

-- 1. Bidder -- учасник торгів із внутрішнім гаманцем.
-- Зв'язки: 1:N з Bid, M:N з Auction (через AuctionRegistration).
IF OBJECT_ID(N'dbo.Bidder', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bidder
    (
        BidderId       BIGINT           IDENTITY(1,1) NOT NULL,
        -- посилання на користувача в Identity (інший контекст) -- БЕЗ FK
        ExternalUserId UNIQUEIDENTIFIER NOT NULL,
        DisplayName    NVARCHAR(100)    NOT NULL,
        Email          NVARCHAR(256)    NOT NULL,
        WalletBalance  DECIMAL(18,2)    NOT NULL CONSTRAINT DF_Bidder_WalletBalance DEFAULT (0),
        IsBlocked      BIT              NOT NULL CONSTRAINT DF_Bidder_IsBlocked     DEFAULT (0),
        -- аудит, soft delete, оптимістична конкурентність
        CreatedAt      DATETIME2(3)     NOT NULL CONSTRAINT DF_Bidder_CreatedAt     DEFAULT (SYSUTCDATETIME()),
        CreatedBy      NVARCHAR(100)    NOT NULL CONSTRAINT DF_Bidder_CreatedBy     DEFAULT (SUSER_SNAME()),
        UpdatedAt      DATETIME2(3)     NULL,
        UpdatedBy      NVARCHAR(100)    NULL,
        IsDeleted      BIT              NOT NULL CONSTRAINT DF_Bidder_IsDeleted     DEFAULT (0),
        RowVersion     ROWVERSION       NOT NULL,
        CONSTRAINT PK_Bidder                PRIMARY KEY CLUSTERED (BidderId),
        CONSTRAINT UQ_Bidder_ExternalUserId UNIQUE (ExternalUserId),
        CONSTRAINT CK_Bidder_WalletBalance  CHECK (WalletBalance >= 0),
        CONSTRAINT CK_Bidder_Email          CHECK (Email LIKE N'%_@_%._%')
    );

    -- Запит: «знайти учасника за email при вході/реєстрації» (унікальність
    -- лише серед невидалених, щоб email можна було використати повторно).
    CREATE UNIQUE NONCLUSTERED INDEX UX_Bidder_Email
        ON dbo.Bidder (Email) WHERE IsDeleted = 0;
END
GO

-- 2. Auction -- корінь агрегату. Зв'язки: 1:1 з AuctionDetails, 1:N з Bid,
-- M:N з Bidder (AuctionRegistration), 1:N зі StatusHistory, 1:1 із Settlement.
IF OBJECT_ID(N'dbo.Auction', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Auction
    (
        AuctionId         BIGINT           IDENTITY(1,1) NOT NULL,
        -- дубльовані/зовнішні дані (власник -- Catalog), БЕЗ FK
        ItemId            BIGINT           NOT NULL,
        ItemTitle         NVARCHAR(200)    NOT NULL,   -- знімок назви, оновлюється подією ItemUpdated
        SellerExternalId  UNIQUEIDENTIFIER NOT NULL,   -- продавець (Identity), БЕЗ FK
        Status            NVARCHAR(20)     NOT NULL CONSTRAINT DF_Auction_Status DEFAULT (N'Draft'),
        StartsAt          DATETIME2(3)     NOT NULL,
        EndsAt            DATETIME2(3)     NOT NULL,
        StartingPrice     DECIMAL(18,2)    NOT NULL,
        CurrentPrice      DECIMAL(18,2)    NOT NULL,
        BidCount          INT              NOT NULL CONSTRAINT DF_Auction_BidCount DEFAULT (0),
        LeadingBidderId   BIGINT           NULL,
        CreatedAt         DATETIME2(3)     NOT NULL CONSTRAINT DF_Auction_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy         NVARCHAR(100)    NOT NULL CONSTRAINT DF_Auction_CreatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAt         DATETIME2(3)     NULL,
        UpdatedBy         NVARCHAR(100)    NULL,
        IsDeleted         BIT              NOT NULL CONSTRAINT DF_Auction_IsDeleted DEFAULT (0),
        RowVersion        ROWVERSION       NOT NULL,
        CONSTRAINT PK_Auction PRIMARY KEY CLUSTERED (AuctionId),
        -- Внутрішній FK (та сама база). NO ACTION: учасників не видаляємо
        -- фізично, лише soft delete, тож каскад тут небезпечний.
        CONSTRAINT FK_Auction_LeadingBidder FOREIGN KEY (LeadingBidderId)
            REFERENCES dbo.Bidder (BidderId) ON DELETE NO ACTION,
        CONSTRAINT CK_Auction_Status CHECK (Status IN
            (N'Draft', N'Scheduled', N'Active', N'Closed', N'Settled', N'Unsold', N'Cancelled')),
        CONSTRAINT CK_Auction_Dates  CHECK (EndsAt > StartsAt),
        CONSTRAINT CK_Auction_Prices CHECK (StartingPrice > 0 AND CurrentPrice >= StartingPrice),
        CONSTRAINT CK_Auction_BidCount CHECK (BidCount >= 0)
    );

    -- Запит: «активні лоти, що закінчуються найближчим часом» (вітрина) та
    -- фонове завдання «знайти прострочені активні аукціони для закриття».
    -- Покриваючий + фільтрований: вітрина не звертається до кластеризованого індексу.
    CREATE NONCLUSTERED INDEX IX_Auction_Status_EndsAt
        ON dbo.Auction (Status, EndsAt)
        INCLUDE (ItemId, ItemTitle, CurrentPrice, BidCount)
        WHERE IsDeleted = 0;

    -- Запит: «усі аукціони конкретного предмета» (перевірка дубля та історія лота).
    CREATE NONCLUSTERED INDEX IX_Auction_ItemId
        ON dbo.Auction (ItemId) WHERE IsDeleted = 0;

    -- Запит: «мої аукціони» в кабінеті продавця.
    CREATE NONCLUSTERED INDEX IX_Auction_SellerExternalId
        ON dbo.Auction (SellerExternalId, Status) WHERE IsDeleted = 0;
END
GO

-- 3. AuctionDetails -- зв'язок 1:1 з Auction (PK залежної таблиці = FK).
-- Налаштування торгів винесені окремо: рідко читаються у списках.
-- CASCADE: життєвий цикл деталей повністю залежить від аукціону.
IF OBJECT_ID(N'dbo.AuctionDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuctionDetails
    (
        AuctionId                    BIGINT        NOT NULL,   -- PK і FK одночасно (1:1)
        ReservePrice                 DECIMAL(18,2) NULL,
        BidIncrement                 DECIMAL(18,2) NOT NULL CONSTRAINT DF_AuctionDetails_BidIncrement DEFAULT (1),
        RequiredDeposit              DECIMAL(18,2) NOT NULL CONSTRAINT DF_AuctionDetails_Deposit      DEFAULT (0),
        BuyerPremiumPercent          DECIMAL(5,2)  NOT NULL CONSTRAINT DF_AuctionDetails_Premium      DEFAULT (10),
        AntiSnipingWindowMinutes     INT           NOT NULL CONSTRAINT DF_AuctionDetails_SnipeWin     DEFAULT (2),
        AntiSnipingExtensionMinutes  INT           NOT NULL CONSTRAINT DF_AuctionDetails_SnipeExt     DEFAULT (2),
        Notes                        NVARCHAR(1000) NULL,
        CreatedAt                    DATETIME2(3)  NOT NULL CONSTRAINT DF_AuctionDetails_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy                    NVARCHAR(100) NOT NULL CONSTRAINT DF_AuctionDetails_CreatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAt                    DATETIME2(3)  NULL,
        UpdatedBy                    NVARCHAR(100) NULL,
        CONSTRAINT PK_AuctionDetails PRIMARY KEY CLUSTERED (AuctionId),
        CONSTRAINT FK_AuctionDetails_Auction FOREIGN KEY (AuctionId)
            REFERENCES dbo.Auction (AuctionId) ON DELETE CASCADE,
        CONSTRAINT CK_AuctionDetails_Reserve   CHECK (ReservePrice IS NULL OR ReservePrice > 0),
        CONSTRAINT CK_AuctionDetails_Increment CHECK (BidIncrement > 0),
        CONSTRAINT CK_AuctionDetails_Deposit   CHECK (RequiredDeposit >= 0),
        CONSTRAINT CK_AuctionDetails_Premium   CHECK (BuyerPremiumPercent BETWEEN 0 AND 100),
        CONSTRAINT CK_AuctionDetails_Snipe     CHECK (AntiSnipingWindowMinutes >= 0 AND AntiSnipingExtensionMinutes >= 0)
    );
END
GO

-- 4. Bid -- зв'язок 1:N (Auction -> Bid, Bidder -> Bid). Ставки незмінні:
-- фізично не видаляються (NO ACTION), «видалення» = IsDeleted (модерація).
IF OBJECT_ID(N'dbo.Bid', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bid
    (
        BidId      BIGINT        IDENTITY(1,1) NOT NULL,
        AuctionId  BIGINT        NOT NULL,
        BidderId   BIGINT        NOT NULL,
        Amount     DECIMAL(18,2) NOT NULL,
        PlacedAt   DATETIME2(3)  NOT NULL CONSTRAINT DF_Bid_PlacedAt  DEFAULT (SYSUTCDATETIME()),
        IsWinning  BIT           NOT NULL CONSTRAINT DF_Bid_IsWinning DEFAULT (0),
        CreatedAt  DATETIME2(3)  NOT NULL CONSTRAINT DF_Bid_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy  NVARCHAR(100) NOT NULL CONSTRAINT DF_Bid_CreatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAt  DATETIME2(3)  NULL,
        UpdatedBy  NVARCHAR(100) NULL,
        IsDeleted  BIT           NOT NULL CONSTRAINT DF_Bid_IsDeleted DEFAULT (0),
        RowVersion ROWVERSION    NOT NULL,
        CONSTRAINT PK_Bid PRIMARY KEY CLUSTERED (BidId),
        CONSTRAINT FK_Bid_Auction FOREIGN KEY (AuctionId) REFERENCES dbo.Auction (AuctionId) ON DELETE NO ACTION,
        CONSTRAINT FK_Bid_Bidder  FOREIGN KEY (BidderId)  REFERENCES dbo.Bidder  (BidderId)  ON DELETE NO ACTION,
        CONSTRAINT CK_Bid_Amount  CHECK (Amount > 0)
    );

    -- Запит: «поточна найвища ставка / топ-N ставок аукціону» (ORDER BY Amount DESC).
    -- UNIQUE: дві різні ставки на однакову суму в одному аукціоні неможливі
    -- (друга лінія захисту від гонки поряд з UPDLOCK у sp_PlaceBid).
    CREATE UNIQUE NONCLUSTERED INDEX UX_Bid_AuctionId_Amount
        ON dbo.Bid (AuctionId, Amount DESC)
        INCLUDE (BidderId, PlacedAt, IsWinning)
        WHERE IsDeleted = 0;

    -- Запит: «мої ставки» (історія учасника, найновіші першими).
    CREATE NONCLUSTERED INDEX IX_Bid_BidderId_PlacedAt
        ON dbo.Bid (BidderId, PlacedAt DESC)
        INCLUDE (AuctionId, Amount, IsWinning)
        WHERE IsDeleted = 0;
END
GO

-- 5. AuctionRegistration -- зв'язок M:N (Bidder <-> Auction) з власними полями:
-- сума застави та її статус. Складений PK = два FK.
IF OBJECT_ID(N'dbo.AuctionRegistration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuctionRegistration
    (
        AuctionId     BIGINT        NOT NULL,
        BidderId      BIGINT        NOT NULL,
        DepositAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_AuctionRegistration_Deposit DEFAULT (0),
        Status        NVARCHAR(20)  NOT NULL CONSTRAINT DF_AuctionRegistration_Status  DEFAULT (N'Active'),
        RegisteredAt  DATETIME2(3)  NOT NULL CONSTRAINT DF_AuctionRegistration_RegAt   DEFAULT (SYSUTCDATETIME()),
        CreatedAt     DATETIME2(3)  NOT NULL CONSTRAINT DF_AuctionRegistration_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy     NVARCHAR(100) NOT NULL CONSTRAINT DF_AuctionRegistration_CreatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAt     DATETIME2(3)  NULL,
        UpdatedBy     NVARCHAR(100) NULL,
        RowVersion    ROWVERSION    NOT NULL,
        CONSTRAINT PK_AuctionRegistration PRIMARY KEY CLUSTERED (AuctionId, BidderId),
        CONSTRAINT FK_AuctionRegistration_Auction FOREIGN KEY (AuctionId) REFERENCES dbo.Auction (AuctionId) ON DELETE NO ACTION,
        CONSTRAINT FK_AuctionRegistration_Bidder  FOREIGN KEY (BidderId)  REFERENCES dbo.Bidder  (BidderId)  ON DELETE NO ACTION,
        CONSTRAINT CK_AuctionRegistration_Deposit CHECK (DepositAmount >= 0),
        CONSTRAINT CK_AuctionRegistration_Status  CHECK (Status IN (N'Active', N'Released', N'Captured'))
    );

    -- Запит: «аукціони, на які зареєстрований учасник» (PK покриває лише пошук
    -- за AuctionId, тому потрібен зворотний індекс за BidderId).
    CREATE NONCLUSTERED INDEX IX_AuctionRegistration_BidderId
        ON dbo.AuctionRegistration (BidderId) INCLUDE (Status, DepositAmount);
END
GO

-- 6. WalletTransaction -- журнал руху коштів гаманця (append-only, 1:N від Bidder).
-- Ledger дозволяє довести, що баланс = сума проводок (аудит грошей).
IF OBJECT_ID(N'dbo.WalletTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WalletTransaction
    (
        TransactionId BIGINT        IDENTITY(1,1) NOT NULL,
        BidderId      BIGINT        NOT NULL,
        AuctionId     BIGINT        NULL,
        Type          NVARCHAR(20)  NOT NULL,
        Amount        DECIMAL(18,2) NOT NULL,
        BalanceAfter  DECIMAL(18,2) NOT NULL,
        CreatedAt     DATETIME2(3)  NOT NULL CONSTRAINT DF_WalletTransaction_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy     NVARCHAR(100) NOT NULL CONSTRAINT DF_WalletTransaction_CreatedBy DEFAULT (SUSER_SNAME()),
        CONSTRAINT PK_WalletTransaction PRIMARY KEY CLUSTERED (TransactionId),
        CONSTRAINT FK_WalletTransaction_Bidder  FOREIGN KEY (BidderId)  REFERENCES dbo.Bidder  (BidderId)  ON DELETE NO ACTION,
        CONSTRAINT FK_WalletTransaction_Auction FOREIGN KEY (AuctionId) REFERENCES dbo.Auction (AuctionId) ON DELETE NO ACTION,
        CONSTRAINT CK_WalletTransaction_Type    CHECK (Type IN (N'TopUp', N'DepositHold', N'DepositRelease', N'SettlementCharge')),
        CONSTRAINT CK_WalletTransaction_Amount  CHECK (Amount <> 0),
        CONSTRAINT CK_WalletTransaction_Balance CHECK (BalanceAfter >= 0)
    );

    -- Запит: «виписка по гаманцю учасника», найновіші проводки першими.
    CREATE NONCLUSTERED INDEX IX_WalletTransaction_BidderId_CreatedAt
        ON dbo.WalletTransaction (BidderId, CreatedAt DESC)
        INCLUDE (Type, Amount, BalanceAfter);
END
GO

-- 7. Settlement -- результат розрахунку; рівно один на аукціон (1:1 за UNIQUE).
IF OBJECT_ID(N'dbo.Settlement', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Settlement
    (
        SettlementId    BIGINT        IDENTITY(1,1) NOT NULL,
        AuctionId       BIGINT        NOT NULL,
        WinnerBidderId  BIGINT        NOT NULL,
        WinningBidId    BIGINT        NOT NULL,
        HammerPrice     DECIMAL(18,2) NOT NULL,
        BuyerPremium    DECIMAL(18,2) NOT NULL,
        TotalDue        DECIMAL(18,2) NOT NULL,
        DepositApplied  DECIMAL(18,2) NOT NULL,
        AmountCharged   DECIMAL(18,2) NOT NULL,
        SettledAt       DATETIME2(3)  NOT NULL CONSTRAINT DF_Settlement_SettledAt DEFAULT (SYSUTCDATETIME()),
        CreatedAt       DATETIME2(3)  NOT NULL CONSTRAINT DF_Settlement_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy       NVARCHAR(100) NOT NULL CONSTRAINT DF_Settlement_CreatedBy DEFAULT (SUSER_SNAME()),
        RowVersion      ROWVERSION    NOT NULL,
        CONSTRAINT PK_Settlement PRIMARY KEY CLUSTERED (SettlementId),
        CONSTRAINT UQ_Settlement_AuctionId UNIQUE (AuctionId),
        CONSTRAINT FK_Settlement_Auction FOREIGN KEY (AuctionId)      REFERENCES dbo.Auction (AuctionId) ON DELETE NO ACTION,
        CONSTRAINT FK_Settlement_Bidder  FOREIGN KEY (WinnerBidderId) REFERENCES dbo.Bidder  (BidderId)  ON DELETE NO ACTION,
        CONSTRAINT FK_Settlement_Bid     FOREIGN KEY (WinningBidId)   REFERENCES dbo.Bid     (BidId)     ON DELETE NO ACTION,
        CONSTRAINT CK_Settlement_Amounts CHECK (HammerPrice > 0 AND BuyerPremium >= 0 AND TotalDue = HammerPrice + BuyerPremium)
    );

    -- Запит: «покупки переможця» (мої виграші).
    CREATE NONCLUSTERED INDEX IX_Settlement_WinnerBidderId
        ON dbo.Settlement (WinnerBidderId) INCLUDE (AuctionId, TotalDue, SettledAt);
END
GO

-- 8. AuctionStatusHistory -- журнал змін статусу (1:N від Auction), append-only.
IF OBJECT_ID(N'dbo.AuctionStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuctionStatusHistory
    (
        HistoryId  BIGINT        IDENTITY(1,1) NOT NULL,
        AuctionId  BIGINT        NOT NULL,
        OldStatus  NVARCHAR(20)  NULL,
        NewStatus  NVARCHAR(20)  NOT NULL,
        Reason     NVARCHAR(500) NULL,
        ChangedAt  DATETIME2(3)  NOT NULL CONSTRAINT DF_AuctionStatusHistory_ChangedAt DEFAULT (SYSUTCDATETIME()),
        ChangedBy  NVARCHAR(100) NOT NULL CONSTRAINT DF_AuctionStatusHistory_ChangedBy DEFAULT (SUSER_SNAME()),
        CONSTRAINT PK_AuctionStatusHistory PRIMARY KEY CLUSTERED (HistoryId),
        CONSTRAINT FK_AuctionStatusHistory_Auction FOREIGN KEY (AuctionId)
            REFERENCES dbo.Auction (AuctionId) ON DELETE NO ACTION,
        CONSTRAINT CK_AuctionStatusHistory_New CHECK (NewStatus IN
            (N'Draft', N'Scheduled', N'Active', N'Closed', N'Settled', N'Unsold', N'Cancelled'))
    );

    -- Запит: «хронологія статусів конкретного аукціону».
    CREATE NONCLUSTERED INDEX IX_AuctionStatusHistory_AuctionId_ChangedAt
        ON dbo.AuctionStatusHistory (AuctionId, ChangedAt);
END
GO

-- 9. Представлення для читання (денормалізована «вітрина» активних лотів).
CREATE OR ALTER VIEW dbo.vw_ActiveAuction
AS
SELECT  a.AuctionId,
        a.ItemId,
        a.ItemTitle,
        a.Status,
        a.StartsAt,
        a.EndsAt,
        a.StartingPrice,
        a.CurrentPrice,
        a.BidCount,
        d.BidIncrement,
        d.RequiredDeposit,
        CAST(CASE WHEN d.ReservePrice IS NULL OR a.CurrentPrice >= d.ReservePrice THEN 1 ELSE 0 END AS BIT) AS IsReserveMet,
        b.DisplayName AS LeadingBidderName
FROM    dbo.Auction        AS a
JOIN    dbo.AuctionDetails AS d ON d.AuctionId = a.AuctionId
LEFT JOIN dbo.Bidder       AS b ON b.BidderId  = a.LeadingBidderId
WHERE   a.IsDeleted = 0
  AND   a.Status IN (N'Scheduled', N'Active');
GO
