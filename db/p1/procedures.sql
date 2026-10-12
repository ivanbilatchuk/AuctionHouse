/* AuctionHouse_Auctions: 6 процедур (sp_CreateAuction, sp_UpdateAuction, sp_RegisterBidder, sp_PlaceBid, sp_ChangeAuctionStatus, sp_SettleAuction).
   Усі працюють у явній транзакції (XACT_ABORT ON, TRY/CATCH, ROLLBACK, повторний THROW). Помилки -- керовані THROW із кодами 50000+.
   Порядок блокувань скрізь однаковий: Auction -> Bidder. Ізоляція: READ COMMITTED + UPDLOCK, HOLDLOCK на рядку Auction. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE AuctionHouse_Auctions;
GO

CREATE OR ALTER PROCEDURE dbo.sp_CreateAuction
    @ItemId              BIGINT,
    @ItemTitle           NVARCHAR(200),
    @SellerExternalId    UNIQUEIDENTIFIER,
    @StartsAt            DATETIME2(3),
    @EndsAt              DATETIME2(3),
    @StartingPrice       DECIMAL(18,2),
    @ReservePrice        DECIMAL(18,2) = NULL,
    @BidIncrement        DECIMAL(18,2) = 1,
    @RequiredDeposit     DECIMAL(18,2) = 0,
    @BuyerPremiumPercent DECIMAL(5,2)  = 10,
    @Actor               NVARCHAR(100),
    @AuctionId           BIGINT        = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @EndsAt <= @StartsAt
        BEGIN
            THROW 50011, N'EndsAt must be later than StartsAt', 1;
        END

        IF EXISTS (SELECT 1
                   FROM dbo.Auction WITH (UPDLOCK, HOLDLOCK)
                   WHERE ItemId = @ItemId
                     AND IsDeleted = 0
                     AND Status IN (N'Draft', N'Scheduled', N'Active', N'Closed'))
        BEGIN
            THROW 50010, N'Item already has an unfinished auction', 1;
        END

        INSERT dbo.Auction
            (ItemId, ItemTitle, SellerExternalId, Status, StartsAt, EndsAt,
             StartingPrice, CurrentPrice, BidCount, CreatedAt, CreatedBy)
        VALUES
            (@ItemId, @ItemTitle, @SellerExternalId, N'Draft', @StartsAt, @EndsAt,
             @StartingPrice, @StartingPrice, 0, SYSUTCDATETIME(), @Actor);

        SET @AuctionId = SCOPE_IDENTITY();

        INSERT dbo.AuctionDetails
            (AuctionId, ReservePrice, BidIncrement, RequiredDeposit, BuyerPremiumPercent, CreatedAt, CreatedBy)
        VALUES
            (@AuctionId, @ReservePrice, @BidIncrement, @RequiredDeposit, @BuyerPremiumPercent, SYSUTCDATETIME(), @Actor);

        INSERT dbo.AuctionStatusHistory (AuctionId, OldStatus, NewStatus, Reason, ChangedAt, ChangedBy)
        VALUES (@AuctionId, NULL, N'Draft', N'Auction created', SYSUTCDATETIME(), @Actor);

        COMMIT TRANSACTION;

        SELECT @AuctionId AS AuctionId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- 2. sp_UpdateAuction -- оптимістична конкурентність через RowVersion
CREATE OR ALTER PROCEDURE dbo.sp_UpdateAuction
    @AuctionId          BIGINT,
    @StartsAt           DATETIME2(3),
    @EndsAt             DATETIME2(3),
    @StartingPrice      DECIMAL(18,2),
    @ReservePrice       DECIMAL(18,2) = NULL,
    @BidIncrement       DECIMAL(18,2),
    @ExpectedRowVersion BINARY(8),
    @Actor              NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(20), @BidCount INT, @Now DATETIME2(3) = SYSUTCDATETIME();

        SELECT @Status = Status, @BidCount = BidCount
        FROM dbo.Auction WITH (UPDLOCK, HOLDLOCK)
        WHERE AuctionId = @AuctionId AND IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            THROW 50000, N'Auction not found', 1;
        END
        IF @Status NOT IN (N'Draft', N'Scheduled')
        BEGIN
            THROW 50031, N'Only Draft or Scheduled auctions can be edited', 1;
        END
        IF @BidCount > 0
        BEGIN
            THROW 50032, N'Auction already has bids and cannot be edited', 1;
        END
        IF @EndsAt <= @StartsAt
        BEGIN
            THROW 50011, N'EndsAt must be later than StartsAt', 1;
        END

        -- Оптимістична перевірка: рядок оновиться лише якщо RowVersion не змінився
        UPDATE dbo.Auction
        SET StartsAt      = @StartsAt,
            EndsAt        = @EndsAt,
            StartingPrice = @StartingPrice,
            CurrentPrice  = @StartingPrice,
            UpdatedAt     = @Now,
            UpdatedBy     = @Actor
        WHERE AuctionId = @AuctionId
          AND RowVersion = @ExpectedRowVersion;

        IF @@ROWCOUNT = 0
        BEGIN
            THROW 50030, N'Concurrency conflict: the auction was modified by another user', 1;
        END

        UPDATE dbo.AuctionDetails
        SET ReservePrice = @ReservePrice,
            BidIncrement = @BidIncrement,
            UpdatedAt    = @Now,
            UpdatedBy    = @Actor
        WHERE AuctionId = @AuctionId;

        COMMIT TRANSACTION;

        SELECT AuctionId, RowVersion
        FROM dbo.Auction
        WHERE AuctionId = @AuctionId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- 3. sp_RegisterBidder -- реєстрація на аукціон із заставою (M:N + гаманець)
CREATE OR ALTER PROCEDURE dbo.sp_RegisterBidder
    @AuctionId BIGINT,
    @BidderId  BIGINT,
    @Actor     NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Now DATETIME2(3) = SYSUTCDATETIME(),
                @Status NVARCHAR(20), @Seller UNIQUEIDENTIFIER, @Deposit DECIMAL(18,2),
                @ExternalUserId UNIQUEIDENTIFIER, @IsBlocked BIT, @Balance DECIMAL(18,2),
                @NewBalance DECIMAL(18,2);

        -- Порядок блокувань: Auction -> Bidder
        SELECT @Status = a.Status, @Seller = a.SellerExternalId, @Deposit = d.RequiredDeposit
        FROM dbo.Auction AS a WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.AuctionDetails AS d ON d.AuctionId = a.AuctionId
        WHERE a.AuctionId = @AuctionId AND a.IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            THROW 50000, N'Auction not found', 1;
        END
        IF @Status NOT IN (N'Scheduled', N'Active')
        BEGIN
            THROW 50020, N'Registration is available only for Scheduled or Active auctions', 1;
        END

        SELECT @ExternalUserId = ExternalUserId, @IsBlocked = IsBlocked, @Balance = WalletBalance
        FROM dbo.Bidder WITH (UPDLOCK)
        WHERE BidderId = @BidderId AND IsDeleted = 0;

        IF @ExternalUserId IS NULL
        BEGIN
            THROW 50042, N'Bidder not found', 1;
        END
        IF @IsBlocked = 1 OR @ExternalUserId = @Seller
        BEGIN
            THROW 50023, N'Bidder is blocked or is the seller of this auction', 1;
        END
        IF EXISTS (SELECT 1 FROM dbo.AuctionRegistration
                   WHERE AuctionId = @AuctionId AND BidderId = @BidderId)
        BEGIN
            THROW 50021, N'Bidder is already registered for this auction', 1;
        END
        IF @Balance < @Deposit
        BEGIN
            THROW 50022, N'Insufficient wallet balance for the required deposit', 1;
        END

        SET @NewBalance = @Balance - @Deposit;

        UPDATE dbo.Bidder
        SET WalletBalance = @NewBalance, UpdatedAt = @Now, UpdatedBy = @Actor
        WHERE BidderId = @BidderId;

        INSERT dbo.AuctionRegistration
            (AuctionId, BidderId, DepositAmount, Status, RegisteredAt, CreatedAt, CreatedBy)
        VALUES
            (@AuctionId, @BidderId, @Deposit, N'Active', @Now, @Now, @Actor);

        IF @Deposit > 0
        BEGIN
            INSERT dbo.WalletTransaction (BidderId, AuctionId, Type, Amount, BalanceAfter, CreatedAt, CreatedBy)
            VALUES (@BidderId, @AuctionId, N'DepositHold', -@Deposit, @NewBalance, @Now, @Actor);
        END

        COMMIT TRANSACTION;

        SELECT @AuctionId AS AuctionId, @BidderId AS BidderId, @Deposit AS DepositHeld, @NewBalance AS WalletBalance;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- 4. sp_PlaceBid -- серце системи: змагання ставок
-- UPDLOCK + HOLDLOCK на рядку Auction серіалізують ставки одного аукціону:
-- друга сесія чекає, поки перша зафіксує нову CurrentPrice, і перевіряє
-- мінімальну суму вже за актуальними даними (немає «втрачених оновлень»).
CREATE OR ALTER PROCEDURE dbo.sp_PlaceBid
    @AuctionId BIGINT,
    @BidderId  BIGINT,
    @Amount    DECIMAL(18,2),
    @Actor     NVARCHAR(100),
    @BidId     BIGINT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Now DATETIME2(3) = SYSUTCDATETIME(),
                @Status NVARCHAR(20), @StartsAt DATETIME2(3), @EndsAt DATETIME2(3),
                @StartingPrice DECIMAL(18,2), @CurrentPrice DECIMAL(18,2), @BidCount INT,
                @LeadingBidderId BIGINT, @Seller UNIQUEIDENTIFIER,
                @Increment DECIMAL(18,2), @WindowMin INT, @ExtensionMin INT,
                @ExternalUserId UNIQUEIDENTIFIER, @IsBlocked BIT,
                @MinAmount DECIMAL(18,2), @NewEndsAt DATETIME2(3), @Msg NVARCHAR(400);

        SELECT @Status = a.Status, @StartsAt = a.StartsAt, @EndsAt = a.EndsAt,
               @StartingPrice = a.StartingPrice, @CurrentPrice = a.CurrentPrice,
               @BidCount = a.BidCount, @LeadingBidderId = a.LeadingBidderId,
               @Seller = a.SellerExternalId,
               @Increment = d.BidIncrement,
               @WindowMin = d.AntiSnipingWindowMinutes, @ExtensionMin = d.AntiSnipingExtensionMinutes
        FROM dbo.Auction AS a WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.AuctionDetails AS d ON d.AuctionId = a.AuctionId
        WHERE a.AuctionId = @AuctionId AND a.IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            THROW 50000, N'Auction not found', 1;
        END
        IF @Status <> N'Active'
        BEGIN
            THROW 50040, N'Auction is not active', 1;
        END
        IF @Now < @StartsAt OR @Now >= @EndsAt
        BEGIN
            THROW 50041, N'Bidding is outside of the auction time window', 1;
        END

        SELECT @ExternalUserId = ExternalUserId, @IsBlocked = IsBlocked
        FROM dbo.Bidder
        WHERE BidderId = @BidderId AND IsDeleted = 0;

        IF @ExternalUserId IS NULL
        BEGIN
            THROW 50042, N'Bidder not found', 1;
        END
        IF @IsBlocked = 1 OR @ExternalUserId = @Seller
        BEGIN
            THROW 50023, N'Bidder is blocked or is the seller of this auction', 1;
        END
        IF NOT EXISTS (SELECT 1 FROM dbo.AuctionRegistration
                       WHERE AuctionId = @AuctionId AND BidderId = @BidderId AND Status = N'Active')
        BEGIN
            THROW 50043, N'Bidder is not registered (no active deposit) for this auction', 1;
        END
        IF @LeadingBidderId = @BidderId
        BEGIN
            THROW 50045, N'Bidder already has the highest bid', 1;
        END

        SET @MinAmount = CASE WHEN @BidCount = 0 THEN @StartingPrice ELSE @CurrentPrice + @Increment END;
        IF @Amount < @MinAmount
        BEGIN
            SET @Msg = CONCAT(N'Bid is below the minimum allowed amount ', CONVERT(NVARCHAR(30), @MinAmount));
            THROW 50046, @Msg, 1;
        END

        UPDATE dbo.Bid
        SET IsWinning = 0, UpdatedAt = @Now, UpdatedBy = @Actor
        WHERE AuctionId = @AuctionId AND IsWinning = 1;

        INSERT dbo.Bid (AuctionId, BidderId, Amount, PlacedAt, IsWinning, CreatedAt, CreatedBy)
        VALUES (@AuctionId, @BidderId, @Amount, @Now, 1, @Now, @Actor);

        SET @BidId = SCOPE_IDENTITY();

        SET @NewEndsAt = @EndsAt;
        IF @ExtensionMin > 0 AND DATEDIFF(SECOND, @Now, @EndsAt) <= @WindowMin * 60
        BEGIN
            SET @NewEndsAt = DATEADD(MINUTE, @ExtensionMin, @EndsAt);
        END

        UPDATE dbo.Auction
        SET CurrentPrice    = @Amount,
            BidCount        = BidCount + 1,
            LeadingBidderId = @BidderId,
            EndsAt          = @NewEndsAt,
            UpdatedAt       = @Now,
            UpdatedBy       = @Actor
        WHERE AuctionId = @AuctionId;

        COMMIT TRANSACTION;

        SELECT @BidId AS BidId,
               @Amount AS CurrentPrice,
               @NewEndsAt AS EndsAt,
               CAST(CASE WHEN @NewEndsAt > @EndsAt THEN 1 ELSE 0 END AS BIT) AS WasExtended;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- 5. sp_ChangeAuctionStatus -- транзакційна зміна статусу
-- Матриця дозволених переходів:
-- Draft     -> Scheduled | Cancelled
-- Scheduled -> Active    | Cancelled
-- Active    -> Closed    | Cancelled
-- Closed    -> Cancelled                 (Closed -> Settled лише sp_SettleAuction)
-- Будь-який інший перехід -> THROW 50001.
-- Побічні ефекти всередині тієї самої транзакції:
-- запит Closed: визначає переможця й перевіряє резервну ціну. Якщо торги
-- успішні -- статус Closed, застави програвших повертаються на гаманці;
-- якщо ставок немає або резерв не досягнуто -- статус Unsold і повертаються
-- усі застави;
-- Cancelled: повертаються всі активні застави;
-- кожна зміна фіксується в AuctionStatusHistory.
-- @Force = 1 дозволяє адміністратору пропустити перевірку часу
-- (запуск до StartsAt, закриття до EndsAt).
CREATE OR ALTER PROCEDURE dbo.sp_ChangeAuctionStatus
    @AuctionId    BIGINT,
    @NewStatus    NVARCHAR(20),
    @Actor        NVARCHAR(100),
    @Reason       NVARCHAR(500) = NULL,
    @Force        BIT           = 0,
    @ResultStatus NVARCHAR(20)  = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Now DATETIME2(3) = SYSUTCDATETIME(),
                @Old NVARCHAR(20), @StartsAt DATETIME2(3), @EndsAt DATETIME2(3),
                @Reserve DECIMAL(18,2), @Msg NVARCHAR(400),
                @Result NVARCHAR(20) = @NewStatus,
                @KeepBidderId BIGINT = NULL,
                @TopBidderId BIGINT, @TopAmount DECIMAL(18,2);

        SELECT @Old = a.Status, @StartsAt = a.StartsAt, @EndsAt = a.EndsAt, @Reserve = d.ReservePrice
        FROM dbo.Auction AS a WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.AuctionDetails AS d ON d.AuctionId = a.AuctionId
        WHERE a.AuctionId = @AuctionId AND a.IsDeleted = 0;

        IF @Old IS NULL
        BEGIN
            THROW 50000, N'Auction not found', 1;
        END

        IF @NewStatus = N'Settled'
        BEGIN
            THROW 50002, N'Status Settled can only be set by sp_SettleAuction', 1;
        END

        -- матриця переходів
        IF NOT (   (@Old = N'Draft'     AND @NewStatus IN (N'Scheduled', N'Cancelled'))
                OR (@Old = N'Scheduled' AND @NewStatus IN (N'Active',    N'Cancelled'))
                OR (@Old = N'Active'    AND @NewStatus IN (N'Closed',    N'Cancelled'))
                OR (@Old = N'Closed'    AND @NewStatus =  N'Cancelled'))
        BEGIN
            SET @Msg = CONCAT(N'Invalid status transition: ', @Old, N' -> ', @NewStatus);
            THROW 50001, @Msg, 1;
        END

        IF @Old = N'Scheduled' AND @NewStatus = N'Active' AND @Now < @StartsAt AND @Force = 0
        BEGIN
            THROW 50004, N'Auction start time has not been reached yet', 1;
        END
        IF @Old = N'Active' AND @NewStatus = N'Closed' AND @Now < @EndsAt AND @Force = 0
        BEGIN
            THROW 50005, N'Auction end time has not been reached yet', 1;
        END

        IF @NewStatus = N'Closed'
        BEGIN
            SELECT TOP (1) @TopBidderId = BidderId, @TopAmount = Amount
            FROM dbo.Bid
            WHERE AuctionId = @AuctionId AND IsDeleted = 0
            ORDER BY Amount DESC;

            IF @TopBidderId IS NOT NULL AND (@Reserve IS NULL OR @TopAmount >= @Reserve)
            BEGIN
                SET @KeepBidderId = @TopBidderId;
            END
            ELSE
            BEGIN
                SET @Result = N'Unsold';
            END
        END

        IF @NewStatus IN (N'Closed', N'Cancelled')
        BEGIN
            UPDATE b
            SET WalletBalance = b.WalletBalance + r.DepositAmount,
                UpdatedAt = @Now, UpdatedBy = @Actor
            FROM dbo.Bidder AS b
            JOIN dbo.AuctionRegistration AS r ON r.BidderId = b.BidderId
            WHERE r.AuctionId = @AuctionId AND r.Status = N'Active' AND r.DepositAmount > 0
              AND (@KeepBidderId IS NULL OR r.BidderId <> @KeepBidderId);

            INSERT dbo.WalletTransaction (BidderId, AuctionId, Type, Amount, BalanceAfter, CreatedAt, CreatedBy)
            SELECT b.BidderId, @AuctionId, N'DepositRelease', r.DepositAmount, b.WalletBalance, @Now, @Actor
            FROM dbo.Bidder AS b
            JOIN dbo.AuctionRegistration AS r ON r.BidderId = b.BidderId
            WHERE r.AuctionId = @AuctionId AND r.Status = N'Active' AND r.DepositAmount > 0
              AND (@KeepBidderId IS NULL OR r.BidderId <> @KeepBidderId);

            UPDATE dbo.AuctionRegistration
            SET Status = N'Released', UpdatedAt = @Now, UpdatedBy = @Actor
            WHERE AuctionId = @AuctionId AND Status = N'Active'
              AND (@KeepBidderId IS NULL OR BidderId <> @KeepBidderId);
        END

        UPDATE dbo.Auction
        SET Status = @Result, UpdatedAt = @Now, UpdatedBy = @Actor
        WHERE AuctionId = @AuctionId;

        INSERT dbo.AuctionStatusHistory (AuctionId, OldStatus, NewStatus, Reason, ChangedAt, ChangedBy)
        VALUES (@AuctionId, @Old, @Result, @Reason, @Now, @Actor);

        COMMIT TRANSACTION;

        SET @ResultStatus = @Result;
        SELECT @AuctionId AS AuctionId, @Old AS OldStatus, @Result AS NewStatus;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_SettleAuction
    @AuctionId    BIGINT,
    @Actor        NVARCHAR(100),
    @SettlementId BIGINT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Now DATETIME2(3) = SYSUTCDATETIME(),
                @Status NVARCHAR(20), @WinnerId BIGINT, @Hammer DECIMAL(18,2), @PremiumPercent DECIMAL(5,2),
                @WinningBidId BIGINT, @Premium DECIMAL(18,2), @Total DECIMAL(18,2),
                @Deposit DECIMAL(18,2), @Charge DECIMAL(18,2), @Balance DECIMAL(18,2), @NewBalance DECIMAL(18,2);

        -- порядок блокувань: Auction -> Bidder
        SELECT @Status = a.Status, @WinnerId = a.LeadingBidderId, @Hammer = a.CurrentPrice,
               @PremiumPercent = d.BuyerPremiumPercent
        FROM dbo.Auction AS a WITH (UPDLOCK, HOLDLOCK)
        JOIN dbo.AuctionDetails AS d ON d.AuctionId = a.AuctionId
        WHERE a.AuctionId = @AuctionId AND a.IsDeleted = 0;

        IF @Status IS NULL
        BEGIN
            THROW 50000, N'Auction not found', 1;
        END
        IF @Status <> N'Closed' OR @WinnerId IS NULL
        BEGIN
            THROW 50050, N'Settlement is possible only for a Closed auction with a winner', 1;
        END

        SELECT @WinningBidId = BidId
        FROM dbo.Bid
        WHERE AuctionId = @AuctionId AND BidderId = @WinnerId AND IsWinning = 1 AND IsDeleted = 0;

        SELECT @Deposit = DepositAmount
        FROM dbo.AuctionRegistration
        WHERE AuctionId = @AuctionId AND BidderId = @WinnerId AND Status = N'Active';

        IF @WinningBidId IS NULL OR @Deposit IS NULL
        BEGIN
            THROW 50052, N'Winner has no winning bid or no active deposit', 1;
        END

        SET @Premium = CAST(ROUND(@Hammer * @PremiumPercent / 100, 2) AS DECIMAL(18,2));
        SET @Total   = @Hammer + @Premium;
        SET @Charge  = @Total - @Deposit;

        SELECT @Balance = WalletBalance
        FROM dbo.Bidder WITH (UPDLOCK)
        WHERE BidderId = @WinnerId;

        IF @Charge > @Balance
        BEGIN
            THROW 50051, N'Insufficient wallet balance to pay for the lot', 1;
        END

        SET @NewBalance = @Balance - @Charge;

        UPDATE dbo.Bidder
        SET WalletBalance = @NewBalance, UpdatedAt = @Now, UpdatedBy = @Actor
        WHERE BidderId = @WinnerId;

        IF @Charge <> 0
        BEGIN
            INSERT dbo.WalletTransaction (BidderId, AuctionId, Type, Amount, BalanceAfter, CreatedAt, CreatedBy)
            VALUES (@WinnerId, @AuctionId, N'SettlementCharge', -@Charge, @NewBalance, @Now, @Actor);
        END

        UPDATE dbo.AuctionRegistration
        SET Status = N'Captured', UpdatedAt = @Now, UpdatedBy = @Actor
        WHERE AuctionId = @AuctionId AND BidderId = @WinnerId;

        INSERT dbo.Settlement
            (AuctionId, WinnerBidderId, WinningBidId, HammerPrice, BuyerPremium, TotalDue,
             DepositApplied, AmountCharged, SettledAt, CreatedAt, CreatedBy)
        VALUES
            (@AuctionId, @WinnerId, @WinningBidId, @Hammer, @Premium, @Total,
             @Deposit, @Charge, @Now, @Now, @Actor);

        SET @SettlementId = SCOPE_IDENTITY();

        UPDATE dbo.Auction
        SET Status = N'Settled', UpdatedAt = @Now, UpdatedBy = @Actor
        WHERE AuctionId = @AuctionId;

        INSERT dbo.AuctionStatusHistory (AuctionId, OldStatus, NewStatus, Reason, ChangedAt, ChangedBy)
        VALUES (@AuctionId, N'Closed', N'Settled', N'Winner has paid', @Now, @Actor);

        COMMIT TRANSACTION;

        SELECT @SettlementId AS SettlementId, @AuctionId AS AuctionId, @WinnerId AS WinnerBidderId,
               @Hammer AS HammerPrice, @Premium AS BuyerPremium, @Total AS TotalDue,
               @Deposit AS DepositApplied, @Charge AS AmountCharged;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

IF DATABASE_PRINCIPAL_ID(N'auctions_app') IS NULL
    CREATE ROLE auctions_app;
GO

GRANT EXECUTE ON dbo.sp_CreateAuction       TO auctions_app;
GRANT EXECUTE ON dbo.sp_UpdateAuction       TO auctions_app;
GRANT EXECUTE ON dbo.sp_RegisterBidder      TO auctions_app;
GRANT EXECUTE ON dbo.sp_PlaceBid            TO auctions_app;
GRANT EXECUTE ON dbo.sp_ChangeAuctionStatus TO auctions_app;
GRANT EXECUTE ON dbo.sp_SettleAuction       TO auctions_app;
GRANT SELECT  ON dbo.vw_ActiveAuction       TO auctions_app;
DENY  INSERT, UPDATE, DELETE ON SCHEMA::dbo TO auctions_app;
GO
