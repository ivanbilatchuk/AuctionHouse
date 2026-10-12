/* AuctionHouse_Catalog: довідники та початкові дані. Ідемпотентний: кожен рядок вставляється лише якщо його ще немає (NOT EXISTS).
   Ключі задані явно (IDENTITY_INSERT, як HasData в EF Core). Title збігається з дубльованим Auctions.ItemTitle. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

USE AuctionHouse_Catalog;
GO

-- Category (довідник, ієрархія)
SET IDENTITY_INSERT dbo.Category ON;
INSERT dbo.Category (CategoryId, ParentCategoryId, Name, Slug, AttributeKind, SortOrder, CreatedBy)
SELECT v.CategoryId, v.ParentCategoryId, v.Name, v.Slug, v.AttributeKind, v.SortOrder, N'seed'
FROM (VALUES
    (1, CAST(NULL AS BIGINT), N'Trading Cards',  N'trading-cards',  N'cards',  10),
    (2, CAST(1    AS BIGINT), N'Pokemon Cards',  N'pokemon-cards',  N'cards',  10),
    (3, CAST(NULL AS BIGINT), N'Stamps',         N'stamps',         N'stamps', 20),
    (4, CAST(NULL AS BIGINT), N'Vinyl Records',  N'vinyl-records',  N'vinyl',  30),
    (5, CAST(NULL AS BIGINT), N'Coins',          N'coins',          N'coins',  40)
) AS v(CategoryId, ParentCategoryId, Name, Slug, AttributeKind, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Category AS c WHERE c.CategoryId = v.CategoryId);
SET IDENTITY_INSERT dbo.Category OFF;
GO

-- Tag (довідник)
SET IDENTITY_INSERT dbo.Tag ON;
INSERT dbo.Tag (TagId, Name, Slug, CreatedBy)
SELECT v.TagId, v.Name, v.Slug, N'seed'
FROM (VALUES
    (1, N'First Edition',   N'first-edition'),
    (2, N'Graded',          N'graded'),
    (3, N'Rare',            N'rare'),
    (4, N'Limited Edition', N'limited-edition'),
    (5, N'Certified',       N'certified')
) AS v(TagId, Name, Slug)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Tag AS t WHERE t.TagId = v.TagId);
SET IDENTITY_INSERT dbo.Tag OFF;
GO

-- Item
SET IDENTITY_INSERT dbo.Item ON;
INSERT dbo.Item (ItemId, CategoryId, SellerExternalId, Title, Slug, EstimateLow, EstimateHigh, Currency, Status, CreatedBy)
SELECT v.ItemId, v.CategoryId, v.SellerExternalId, v.Title, v.Slug, v.EstimateLow, v.EstimateHigh, 'USD', v.Status, N'seed'
FROM (VALUES
    (1, 2, CAST('11111111-1111-1111-1111-111111111111' AS UNIQUEIDENTIFIER),
        N'Charizard Holo 1st Edition, Base Set 1999 (PSA 9)', N'charizard-holo-1st-edition-base-set-1999-psa-9',
        CAST(8000.00 AS DECIMAL(18,2)), CAST(12000.00 AS DECIMAL(18,2)), N'InAuction'),
    (2, 3, CAST('11111111-1111-1111-1111-111111111111' AS UNIQUEIDENTIFIER),
        N'Penny Black, 1840, Great Britain', N'penny-black-1840-great-britain',
        CAST(300.00 AS DECIMAL(18,2)), CAST(600.00 AS DECIMAL(18,2)), N'Listed'),
    (3, 4, CAST('11111111-1111-1111-1111-111111111111' AS UNIQUEIDENTIFIER),
        N'Pink Floyd - The Dark Side of the Moon, UK 1st Press, 1973', N'pink-floyd-dark-side-of-the-moon-uk-1st-press-1973',
        CAST(1500.00 AS DECIMAL(18,2)), CAST(2500.00 AS DECIMAL(18,2)), N'Draft')
) AS v(ItemId, CategoryId, SellerExternalId, Title, Slug, EstimateLow, EstimateHigh, Status)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Item AS i WHERE i.ItemId = v.ItemId);
SET IDENTITY_INSERT dbo.Item OFF;
GO

-- ItemDetail (1:1)
INSERT dbo.ItemDetail (ItemId, Condition, ProductionYear, CountryOfOrigin, CertificateNumber, Provenance, Description, CreatedBy)
SELECT v.ItemId, v.Condition, v.ProductionYear, v.CountryOfOrigin, v.CertificateNumber, v.Provenance, v.Description, N'seed'
FROM (VALUES
    (1, N'Mint',      CAST(1999 AS SMALLINT), N'USA', N'PSA-60000001',
        N'Private collection, acquired 2003; grading certificate included.',
        N'Holographic Charizard from the first print run of the Base Set, graded PSA 9.'),
    (2, N'Excellent', CAST(1840 AS SMALLINT), N'United Kingdom', CAST(NULL AS NVARCHAR(50)),
        N'Estate collection, sold through a London auction house in 1998.',
        N'The first adhesive postage stamp in the world, four clear margins.'),
    (3, N'NearMint',  CAST(1973 AS SMALLINT), N'United Kingdom', CAST(NULL AS NVARCHAR(50)),
        N'Original owner; record and sleeve stored vertically since purchase.',
        N'UK first pressing on Harvest label with posters and stickers.')
) AS v(ItemId, Condition, ProductionYear, CountryOfOrigin, CertificateNumber, Provenance, Description)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ItemDetail AS d WHERE d.ItemId = v.ItemId);
GO

-- ItemImage (1:N)
INSERT dbo.ItemImage (ItemId, Url, IsPrimary, SortOrder, CreatedBy)
SELECT v.ItemId, v.Url, v.IsPrimary, v.SortOrder, N'seed'
FROM (VALUES
    (1, N'https://img.example.com/items/1/front.jpg', CAST(1 AS BIT), 0),
    (1, N'https://img.example.com/items/1/back.jpg',  CAST(0 AS BIT), 1),
    (2, N'https://img.example.com/items/2/front.jpg', CAST(1 AS BIT), 0),
    (3, N'https://img.example.com/items/3/sleeve.jpg',CAST(1 AS BIT), 0)
) AS v(ItemId, Url, IsPrimary, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ItemImage AS im WHERE im.ItemId = v.ItemId AND im.Url = v.Url);
GO

-- ItemTag (M:N з власними полями AddedAt/AddedBy)
INSERT dbo.ItemTag (ItemId, TagId, AddedBy)
SELECT v.ItemId, v.TagId, N'seed'
FROM (VALUES
    (1, 1), (1, 2), (1, 3), (1, 5),
    (2, 3), (2, 5),
    (3, 4)
) AS v(ItemId, TagId)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ItemTag AS it WHERE it.ItemId = v.ItemId AND it.TagId = v.TagId);
GO

PRINT N'Catalog seed completed.';
GO
