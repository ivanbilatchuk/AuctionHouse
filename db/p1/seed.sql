/* AuctionHouse_Auctions: початкові дані. Ідемпотентний: перед кожною вставкою перевіряється наявність запису (IF NOT EXISTS).
   GUID користувачів та ItemId 1-3 збігаються з seed-ами Catalog і Community. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

USE AuctionHouse_Auctions;
GO

DECLARE @Now    DATETIME2(3) = SYSUTCDATETIME(),
        @Seller UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111',
        @U1 UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222',
        @U2 UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333',
        @U3 UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444444';

-- Bidder (баланси вказані ПІСЛЯ утримання застав)
IF NOT EXISTS (SELECT 1 FROM dbo.Bidder WHERE ExternalUserId = @U1)
    INSERT dbo.Bidder (ExternalUserId, DisplayName, Email, WalletBalance, CreatedBy)
    VALUES (@U1, N'Olena Kovalenko', N'olena.kovalenko@example.com', 4450.00, N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Bidder WHERE ExternalUserId = @U2)
    INSERT dbo.Bidder (ExternalUserId, DisplayName, Email, WalletBalance, CreatedBy)
    VALUES (@U2, N'Marko Bondar', N'marko.bondar@example.com', 2500.00, N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Bidder WHERE ExternalUserId = @U3)
    INSERT dbo.Bidder (ExternalUserId, DisplayName, Email, WalletBalance, CreatedBy)
    VALUES (@U3, N'Iryna Melnyk', N'iryna.melnyk@example.com', 1500.00, N'seed');

DECLARE @B1 BIGINT = (SELECT BidderId FROM dbo.Bidder WHERE ExternalUserId = @U1),
        @B2 BIGINT = (SELECT BidderId FROM dbo.Bidder WHERE ExternalUserId = @U2),
        @B3 BIGINT = (SELECT BidderId FROM dbo.Bidder WHERE ExternalUserId = @U3);

-- Auction #1: Active, 4 ставки
IF NOT EXISTS (SELECT 1 FROM dbo.Auction WHERE ItemId = 1)
BEGIN
    INSERT dbo.Auction (ItemId, ItemTitle, SellerExternalId, Status, StartsAt, EndsAt,
                        StartingPrice, CurrentPrice, BidCount, LeadingBidderId, CreatedBy)
    VALUES (1, N'Charizard Holo 1st Edition, Base Set 1999 (PSA 9)', @Seller, N'Active',
            DATEADD(DAY, -2, @Now), DATEADD(DAY, 3, @Now),
            5000.00, 6250.00, 4, @B1, N'seed');

    DECLARE @A1 BIGINT = SCOPE_IDENTITY();

    INSERT dbo.AuctionDetails (AuctionId, ReservePrice, BidIncrement, RequiredDeposit, BuyerPremiumPercent, CreatedBy)
    VALUES (@A1, 8000.00, 250.00, 500.00, 10.00, N'seed');

    INSERT dbo.AuctionRegistration (AuctionId, BidderId, DepositAmount, Status, RegisteredAt, CreatedBy)
    VALUES (@A1, @B1, 500.00, N'Active', DATEADD(DAY, -2, @Now), N'seed'),
           (@A1, @B2, 500.00, N'Active', DATEADD(DAY, -2, @Now), N'seed'),
           (@A1, @B3, 500.00, N'Active', DATEADD(DAY, -2, @Now), N'seed');

    INSERT dbo.Bid (AuctionId, BidderId, Amount, PlacedAt, IsWinning, CreatedBy)
    VALUES (@A1, @B1, 5000.00, DATEADD(HOUR, -40, @Now), 0, N'seed'),
           (@A1, @B2, 5250.00, DATEADD(HOUR, -30, @Now), 0, N'seed'),
           (@A1, @B3, 5750.00, DATEADD(HOUR, -20, @Now), 0, N'seed'),
           (@A1, @B1, 6250.00, DATEADD(HOUR, -10, @Now), 1, N'seed');

    INSERT dbo.AuctionStatusHistory (AuctionId, OldStatus, NewStatus, Reason, ChangedAt, ChangedBy)
    VALUES (@A1, NULL,        N'Draft',     N'Auction created', DATEADD(DAY, -3, @Now), N'seed'),
           (@A1, N'Draft',    N'Scheduled', N'Published',       DATEADD(DAY, -3, @Now), N'seed'),
           (@A1, N'Scheduled',N'Active',    N'Bidding started', DATEADD(DAY, -2, @Now), N'seed');
END

-- Auction #2: Scheduled
IF NOT EXISTS (SELECT 1 FROM dbo.Auction WHERE ItemId = 2)
BEGIN
    INSERT dbo.Auction (ItemId, ItemTitle, SellerExternalId, Status, StartsAt, EndsAt,
                        StartingPrice, CurrentPrice, BidCount, CreatedBy)
    VALUES (2, N'Penny Black, 1840, Great Britain', @Seller, N'Scheduled',
            DATEADD(DAY, 2, @Now), DATEADD(DAY, 9, @Now),
            200.00, 200.00, 0, N'seed');

    DECLARE @A2 BIGINT = SCOPE_IDENTITY();

    INSERT dbo.AuctionDetails (AuctionId, ReservePrice, BidIncrement, RequiredDeposit, BuyerPremiumPercent, CreatedBy)
    VALUES (@A2, 400.00, 20.00, 50.00, 10.00, N'seed');

    INSERT dbo.AuctionRegistration (AuctionId, BidderId, DepositAmount, Status, RegisteredAt, CreatedBy)
    VALUES (@A2, @B1, 50.00, N'Active', @Now, N'seed');

    INSERT dbo.AuctionStatusHistory (AuctionId, OldStatus, NewStatus, Reason, ChangedAt, ChangedBy)
    VALUES (@A2, NULL,     N'Draft',     N'Auction created', DATEADD(DAY, -1, @Now), N'seed'),
           (@A2, N'Draft', N'Scheduled', N'Published',       DATEADD(DAY, -1, @Now), N'seed');
END

-- Auction #3: Draft
IF NOT EXISTS (SELECT 1 FROM dbo.Auction WHERE ItemId = 3)
BEGIN
    INSERT dbo.Auction (ItemId, ItemTitle, SellerExternalId, Status, StartsAt, EndsAt,
                        StartingPrice, CurrentPrice, BidCount, CreatedBy)
    VALUES (3, N'Pink Floyd - The Dark Side of the Moon, UK 1st Press, 1973', @Seller, N'Draft',
            DATEADD(DAY, 10, @Now), DATEADD(DAY, 17, @Now),
            1000.00, 1000.00, 0, N'seed');

    DECLARE @A3 BIGINT = SCOPE_IDENTITY();

    INSERT dbo.AuctionDetails (AuctionId, ReservePrice, BidIncrement, RequiredDeposit, BuyerPremiumPercent, CreatedBy)
    VALUES (@A3, 1500.00, 50.00, 100.00, 10.00, N'seed');

    INSERT dbo.AuctionStatusHistory (AuctionId, OldStatus, NewStatus, Reason, ChangedAt, ChangedBy)
    VALUES (@A3, NULL, N'Draft', N'Auction created', @Now, N'seed');
END

-- WalletTransaction: журнал, що узгоджується з балансами
DECLARE @A1id BIGINT = (SELECT AuctionId FROM dbo.Auction WHERE ItemId = 1),
        @A2id BIGINT = (SELECT AuctionId FROM dbo.Auction WHERE ItemId = 2);

IF NOT EXISTS (SELECT 1 FROM dbo.WalletTransaction WHERE BidderId = @B1)
BEGIN
    INSERT dbo.WalletTransaction (BidderId, AuctionId, Type, Amount, BalanceAfter, CreatedBy)
    VALUES (@B1, NULL,   N'TopUp',       5000.00, 5000.00, N'seed'),
           (@B1, @A1id,  N'DepositHold', -500.00, 4500.00, N'seed'),
           (@B1, @A2id,  N'DepositHold',  -50.00, 4450.00, N'seed');
END

IF NOT EXISTS (SELECT 1 FROM dbo.WalletTransaction WHERE BidderId = @B2)
BEGIN
    INSERT dbo.WalletTransaction (BidderId, AuctionId, Type, Amount, BalanceAfter, CreatedBy)
    VALUES (@B2, NULL,  N'TopUp',       3000.00, 3000.00, N'seed'),
           (@B2, @A1id, N'DepositHold', -500.00, 2500.00, N'seed');
END

IF NOT EXISTS (SELECT 1 FROM dbo.WalletTransaction WHERE BidderId = @B3)
BEGIN
    INSERT dbo.WalletTransaction (BidderId, AuctionId, Type, Amount, BalanceAfter, CreatedBy)
    VALUES (@B3, NULL,  N'TopUp',       2000.00, 2000.00, N'seed'),
           (@B3, @A1id, N'DepositHold', -500.00, 1500.00, N'seed');
END
GO

PRINT N'Auctions seed completed.';
GO
