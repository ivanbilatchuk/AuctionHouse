/* AuctionHouse_Auctions: сценарії перевірки (читання/пагінація, життєвий цикл, керовані помилки, RowVersion, CHECK, звірка гаманців).
   Запускати після schema/procedures/seed; можна повторно (кожен запуск зменшує баланс Iryna на 121). */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

USE AuctionHouse_Auctions;
GO

PRINT N'--- 1. READ + пошук + пагінація (представлення, індекс IX_Auction_Status_EndsAt)';
SELECT AuctionId, ItemTitle, Status, CurrentPrice, BidCount, EndsAt, LeadingBidderName
FROM dbo.vw_ActiveAuction
ORDER BY EndsAt
OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY;

SELECT TOP (3) BidId, BidderId, Amount, PlacedAt, IsWinning
FROM dbo.Bid
WHERE AuctionId = (SELECT AuctionId FROM dbo.Auction WHERE ItemId = 1) AND IsDeleted = 0
ORDER BY Amount DESC;
GO

PRINT N'--- 2. Повний життєвий цикл: Create -> Scheduled -> Active -> bids -> Closed -> Settled';
DECLARE @Seller UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111',
        @B2 BIGINT = (SELECT BidderId FROM dbo.Bidder WHERE ExternalUserId = '33333333-3333-3333-3333-333333333333'),
        @B3 BIGINT = (SELECT BidderId FROM dbo.Bidder WHERE ExternalUserId = '44444444-4444-4444-4444-444444444444'),
        @Now DATETIME2(3) = SYSUTCDATETIME(),
        @EndsAt DATETIME2(3),
        @AuctionId BIGINT, @BidId BIGINT, @Result NVARCHAR(20), @SettlementId BIGINT;
SET @EndsAt = DATEADD(HOUR, 1, @Now);

EXEC dbo.sp_CreateAuction
     @ItemId = 9001, @ItemTitle = N'Demo lot (verify.sql)', @SellerExternalId = @Seller,
     @StartsAt = @Now, @EndsAt = @EndsAt, @StartingPrice = 100.00,
     @ReservePrice = NULL, @BidIncrement = 10.00, @RequiredDeposit = 0, @BuyerPremiumPercent = 10,
     @Actor = N'verify', @AuctionId = @AuctionId OUTPUT;

EXEC dbo.sp_ChangeAuctionStatus @AuctionId = @AuctionId, @NewStatus = N'Scheduled', @Actor = N'verify', @ResultStatus = @Result OUTPUT;
EXEC dbo.sp_ChangeAuctionStatus @AuctionId = @AuctionId, @NewStatus = N'Active',    @Actor = N'verify', @Force = 1, @ResultStatus = @Result OUTPUT;

EXEC dbo.sp_RegisterBidder @AuctionId = @AuctionId, @BidderId = @B2, @Actor = N'verify';
EXEC dbo.sp_RegisterBidder @AuctionId = @AuctionId, @BidderId = @B3, @Actor = N'verify';

EXEC dbo.sp_PlaceBid @AuctionId = @AuctionId, @BidderId = @B2, @Amount = 100.00, @Actor = N'verify', @BidId = @BidId OUTPUT;
EXEC dbo.sp_PlaceBid @AuctionId = @AuctionId, @BidderId = @B3, @Amount = 110.00, @Actor = N'verify', @BidId = @BidId OUTPUT;

-- негативні кейси (нічого не змінюють: транзакція відкочується)
BEGIN TRY EXEC dbo.sp_PlaceBid @AuctionId = @AuctionId, @BidderId = @B3, @Amount = 130.00, @Actor = N'verify';
END TRY BEGIN CATCH SELECT N'expect 50045 (already leading)' AS Case_, ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage; END CATCH

BEGIN TRY EXEC dbo.sp_PlaceBid @AuctionId = @AuctionId, @BidderId = @B2, @Amount = 115.00, @Actor = N'verify';
END TRY BEGIN CATCH SELECT N'expect 50046 (below min = 110 + 10)' AS Case_, ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage; END CATCH

EXEC dbo.sp_ChangeAuctionStatus @AuctionId = @AuctionId, @NewStatus = N'Closed', @Actor = N'verify', @Force = 1, @ResultStatus = @Result OUTPUT;

BEGIN TRY EXEC dbo.sp_ChangeAuctionStatus @AuctionId = @AuctionId, @NewStatus = N'Active', @Actor = N'verify';
END TRY BEGIN CATCH SELECT N'expect 50001 (Closed -> Active)' AS Case_, ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage; END CATCH

EXEC dbo.sp_SettleAuction @AuctionId = @AuctionId, @Actor = N'verify', @SettlementId = @SettlementId OUTPUT;

SELECT OldStatus, NewStatus, Reason, ChangedAt FROM dbo.AuctionStatusHistory WHERE AuctionId = @AuctionId ORDER BY HistoryId;
SELECT * FROM dbo.Settlement WHERE AuctionId = @AuctionId;
GO

PRINT N'--- 3. Оптимістична конкурентність (застарілий RowVersion)';
DECLARE @Seller2 UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111',
        @Now2 DATETIME2(3) = SYSUTCDATETIME(), @DraftId BIGINT, @Rv BINARY(8), @Result2 NVARCHAR(20),
        @D1 DATETIME2(3), @D2 DATETIME2(3), @D3 DATETIME2(3);
SELECT @D1 = DATEADD(DAY, 1, @Now2), @D2 = DATEADD(DAY, 2, @Now2), @D3 = DATEADD(DAY, 3, @Now2);

EXEC dbo.sp_CreateAuction
     @ItemId = 9002, @ItemTitle = N'Demo draft (verify.sql)', @SellerExternalId = @Seller2,
     @StartsAt = @D1, @EndsAt = @D2, @StartingPrice = 50.00,
     @Actor = N'verify', @AuctionId = @DraftId OUTPUT;

SELECT @Rv = RowVersion FROM dbo.Auction WHERE AuctionId = @DraftId;

-- перше оновлення з актуальним RowVersion проходить
EXEC dbo.sp_UpdateAuction @AuctionId = @DraftId, @StartsAt = @D1, @EndsAt = @D3,
     @StartingPrice = 60.00, @ReservePrice = 100.00, @BidIncrement = 5.00, @ExpectedRowVersion = @Rv, @Actor = N'verify';

-- друге з тим самим (вже застарілим) RowVersion -> 50030
BEGIN TRY
    EXEC dbo.sp_UpdateAuction @AuctionId = @DraftId, @StartsAt = @D1, @EndsAt = @D3,
         @StartingPrice = 70.00, @ReservePrice = 100.00, @BidIncrement = 5.00, @ExpectedRowVersion = @Rv, @Actor = N'verify';
END TRY
BEGIN CATCH
    SELECT N'expect 50030 (stale RowVersion)' AS Case_, ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;
END CATCH

EXEC dbo.sp_ChangeAuctionStatus @AuctionId = @DraftId, @NewStatus = N'Cancelled', @Actor = N'verify', @Reason = N'verify.sql cleanup', @ResultStatus = @Result2 OUTPUT;
GO

PRINT N'--- 4. Обмеження (CHECK) -- пряме порушення відхиляється СУБД';
BEGIN TRY
    UPDATE dbo.Auction SET CurrentPrice = 1 WHERE ItemId = 1;
END TRY
BEGIN CATCH
    SELECT N'expect CHECK violation CK_Auction_Prices' AS Case_, ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;
END CATCH
GO

PRINT N'--- 5. Цілісність грошей: баланс гаманця = сума проводок журналу';
SELECT b.BidderId, b.DisplayName, b.WalletBalance,
       SUM(w.Amount) AS LedgerSum,
       CASE WHEN b.WalletBalance = SUM(w.Amount) THEN N'OK' ELSE N'MISMATCH' END AS Consistent
FROM dbo.Bidder AS b
JOIN dbo.WalletTransaction AS w ON w.BidderId = b.BidderId
GROUP BY b.BidderId, b.DisplayName, b.WalletBalance;
GO
