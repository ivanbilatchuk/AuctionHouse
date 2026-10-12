/* AuctionHouse_Catalog (Проєкт №2: SQL Server + EF Core, Code First). Порядок: schema.sql -> seed.sql. Скрипт ідемпотентний.
   Еталонна схема моделі EF Core (Fluent API + міграції в лабі 3). PK: BIGINT IDENTITY (послідовний кластеризований ключ).
   Item.SellerExternalId -- посилання на Identity, звичайний стовпець БЕЗ FOREIGN KEY. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF DB_ID(N'AuctionHouse_Catalog') IS NULL
    CREATE DATABASE AuctionHouse_Catalog;
GO

USE AuctionHouse_Catalog;
GO

-- Category -- ієрархія (Trading Cards -> Pokemon Cards).  Зв'язок 1:N на себе.
-- NO ACTION: не можна видалити категорію, що має підкатегорії або предмети.
IF OBJECT_ID(N'dbo.Category', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Category
    (
        CategoryId       BIGINT        IDENTITY(1,1) NOT NULL,
        ParentCategoryId BIGINT        NULL,
        Name             NVARCHAR(100) NOT NULL,
        Slug             NVARCHAR(120) NOT NULL,
        AttributeKind    NVARCHAR(20)  NOT NULL CONSTRAINT DF_Category_AttributeKind DEFAULT (N'generic'),
        SortOrder        INT           NOT NULL CONSTRAINT DF_Category_SortOrder DEFAULT (0),
        CreatedAt        DATETIME2(3)  NOT NULL CONSTRAINT DF_Category_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy        NVARCHAR(100) NOT NULL CONSTRAINT DF_Category_CreatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAt        DATETIME2(3)  NULL,
        UpdatedBy        NVARCHAR(100) NULL,
        IsDeleted        BIT           NOT NULL CONSTRAINT DF_Category_IsDeleted DEFAULT (0),
        RowVersion       ROWVERSION    NOT NULL,
        CONSTRAINT PK_Category PRIMARY KEY CLUSTERED (CategoryId),
        CONSTRAINT FK_Category_Parent FOREIGN KEY (ParentCategoryId)
            REFERENCES dbo.Category (CategoryId) ON DELETE NO ACTION,
        CONSTRAINT UQ_Category_Slug UNIQUE (Slug),
        CONSTRAINT CK_Category_NotOwnParent CHECK (ParentCategoryId IS NULL OR ParentCategoryId <> CategoryId),
        CONSTRAINT CK_Category_AttributeKind CHECK (AttributeKind IN (N'generic', N'cards', N'stamps', N'vinyl', N'coins'))
    );

    -- Запит: «дочірні категорії» при побудові дерева навігації.
    CREATE NONCLUSTERED INDEX IX_Category_ParentCategoryId
        ON dbo.Category (ParentCategoryId, SortOrder) INCLUDE (Name, Slug)
        WHERE IsDeleted = 0;
END
GO

-- Item -- корінь агрегату.  Зв'язки: N:1 до Category, 1:1 з ItemDetail,
-- 1:N з ItemImage, M:N з Tag (через ItemTag).
IF OBJECT_ID(N'dbo.Item', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Item
    (
        ItemId           BIGINT           IDENTITY(1,1) NOT NULL,
        CategoryId       BIGINT           NOT NULL,
        SellerExternalId UNIQUEIDENTIFIER NOT NULL,   -- Identity, БЕЗ FK
        Title            NVARCHAR(200)    NOT NULL,   -- власник назви: Catalog (дублюється в Auctions/Community)
        Slug             NVARCHAR(220)    NOT NULL,
        EstimateLow      DECIMAL(18,2)    NOT NULL,
        EstimateHigh     DECIMAL(18,2)    NOT NULL,
        Currency         CHAR(3)          NOT NULL CONSTRAINT DF_Item_Currency DEFAULT ('USD'),
        Status           NVARCHAR(20)     NOT NULL CONSTRAINT DF_Item_Status   DEFAULT (N'Draft'),
        CreatedAt        DATETIME2(3)     NOT NULL CONSTRAINT DF_Item_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy        NVARCHAR(100)    NOT NULL CONSTRAINT DF_Item_CreatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAt        DATETIME2(3)     NULL,
        UpdatedBy        NVARCHAR(100)    NULL,
        IsDeleted        BIT              NOT NULL CONSTRAINT DF_Item_IsDeleted DEFAULT (0),
        RowVersion       ROWVERSION       NOT NULL,   -- токен оптимістичної конкурентності (EF: IsRowVersion)
        CONSTRAINT PK_Item PRIMARY KEY CLUSTERED (ItemId),
        CONSTRAINT FK_Item_Category FOREIGN KEY (CategoryId)
            REFERENCES dbo.Category (CategoryId) ON DELETE NO ACTION,
        CONSTRAINT UQ_Item_Slug UNIQUE (Slug),
        CONSTRAINT CK_Item_Estimate CHECK (EstimateLow > 0 AND EstimateHigh >= EstimateLow),
        CONSTRAINT CK_Item_Status   CHECK (Status IN (N'Draft', N'Listed', N'InAuction', N'Sold', N'Withdrawn'))
    );

    -- Запит: «предмети категорії за статусом» (сторінка категорії, фільтр + пагінація).
    CREATE NONCLUSTERED INDEX IX_Item_CategoryId_Status
        ON dbo.Item (CategoryId, Status)
        INCLUDE (Title, Slug, EstimateLow, EstimateHigh)
        WHERE IsDeleted = 0;

    -- Запит: пошук за початком назви (LIKE N'Char%') у рядку пошуку.
    CREATE NONCLUSTERED INDEX IX_Item_Title
        ON dbo.Item (Title) WHERE IsDeleted = 0;

    -- Запит: «мої лоти» в кабінеті продавця.
    CREATE NONCLUSTERED INDEX IX_Item_SellerExternalId
        ON dbo.Item (SellerExternalId) WHERE IsDeleted = 0;
END
GO

-- ItemDetail -- зв'язок 1:1 (PK залежної таблиці є одночасно FK на Item).
-- CASCADE: деталі не існують без предмета.
IF OBJECT_ID(N'dbo.ItemDetail', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemDetail
    (
        ItemId            BIGINT         NOT NULL,   -- PK і FK
        Condition         NVARCHAR(20)   NOT NULL,
        ProductionYear    SMALLINT       NULL,
        CountryOfOrigin   NVARCHAR(100)  NULL,
        CertificateNumber NVARCHAR(50)   NULL,
        Provenance        NVARCHAR(2000) NULL,
        Description       NVARCHAR(MAX)  NULL,
        CreatedAt         DATETIME2(3)   NOT NULL CONSTRAINT DF_ItemDetail_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy         NVARCHAR(100)  NOT NULL CONSTRAINT DF_ItemDetail_CreatedBy DEFAULT (SUSER_SNAME()),
        UpdatedAt         DATETIME2(3)   NULL,
        UpdatedBy         NVARCHAR(100)  NULL,
        CONSTRAINT PK_ItemDetail PRIMARY KEY CLUSTERED (ItemId),
        CONSTRAINT FK_ItemDetail_Item FOREIGN KEY (ItemId)
            REFERENCES dbo.Item (ItemId) ON DELETE CASCADE,
        CONSTRAINT CK_ItemDetail_Condition CHECK (Condition IN
            (N'Mint', N'NearMint', N'Excellent', N'Good', N'Fair', N'Poor')),
        CONSTRAINT CK_ItemDetail_Year CHECK (ProductionYear IS NULL OR ProductionYear BETWEEN 1000 AND 2100)
    );

    -- UNIQUE (фільтрований): номер сертифіката автентичності не може повторюватись,
    -- але NULL (немає сертифіката) допустимий для багатьох предметів.
    CREATE UNIQUE NONCLUSTERED INDEX UX_ItemDetail_CertificateNumber
        ON dbo.ItemDetail (CertificateNumber) WHERE CertificateNumber IS NOT NULL;
END
GO

-- ItemImage -- зв'язок 1:N.  CASCADE: зображення належать предмету.
IF OBJECT_ID(N'dbo.ItemImage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemImage
    (
        ImageId    BIGINT        IDENTITY(1,1) NOT NULL,
        ItemId     BIGINT        NOT NULL,
        Url        NVARCHAR(500) NOT NULL,
        IsPrimary  BIT           NOT NULL CONSTRAINT DF_ItemImage_IsPrimary DEFAULT (0),
        SortOrder  INT           NOT NULL CONSTRAINT DF_ItemImage_SortOrder DEFAULT (0),
        CreatedAt  DATETIME2(3)  NOT NULL CONSTRAINT DF_ItemImage_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy  NVARCHAR(100) NOT NULL CONSTRAINT DF_ItemImage_CreatedBy DEFAULT (SUSER_SNAME()),
        CONSTRAINT PK_ItemImage PRIMARY KEY CLUSTERED (ImageId),
        CONSTRAINT FK_ItemImage_Item FOREIGN KEY (ItemId)
            REFERENCES dbo.Item (ItemId) ON DELETE CASCADE,
        CONSTRAINT CK_ItemImage_Url CHECK (Url LIKE N'http%')
    );

    -- Запит: «зображення предмета у порядку показу».
    CREATE NONCLUSTERED INDEX IX_ItemImage_ItemId_SortOrder
        ON dbo.ItemImage (ItemId, SortOrder) INCLUDE (Url, IsPrimary);

    -- Інваріант «рівно одне головне зображення на предмет» (максимум одне -- на рівні СУБД).
    CREATE UNIQUE NONCLUSTERED INDEX UX_ItemImage_OnePrimaryPerItem
        ON dbo.ItemImage (ItemId) WHERE IsPrimary = 1;
END
GO

-- Tag -- довідник тегів.
IF OBJECT_ID(N'dbo.Tag', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Tag
    (
        TagId     BIGINT        IDENTITY(1,1) NOT NULL,
        Name      NVARCHAR(50)  NOT NULL,
        Slug      NVARCHAR(60)  NOT NULL,
        CreatedAt DATETIME2(3)  NOT NULL CONSTRAINT DF_Tag_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_Tag_CreatedBy DEFAULT (SUSER_SNAME()),
        CONSTRAINT PK_Tag PRIMARY KEY CLUSTERED (TagId),
        CONSTRAINT UQ_Tag_Name UNIQUE (Name),   -- унікальний індекс одночасно обслуговує пошук тега за назвою
        CONSTRAINT UQ_Tag_Slug UNIQUE (Slug)
    );
END
GO

-- ItemTag -- зв'язок M:N (Item <-> Tag) з власними полями AddedAt і AddedBy.
-- Складений PK не дозволяє двічі призначити той самий тег предмету.
IF OBJECT_ID(N'dbo.ItemTag', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ItemTag
    (
        ItemId  BIGINT        NOT NULL,
        TagId   BIGINT        NOT NULL,
        AddedAt DATETIME2(3)  NOT NULL CONSTRAINT DF_ItemTag_AddedAt DEFAULT (SYSUTCDATETIME()),
        AddedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_ItemTag_AddedBy DEFAULT (SUSER_SNAME()),
        CONSTRAINT PK_ItemTag PRIMARY KEY CLUSTERED (ItemId, TagId),
        CONSTRAINT FK_ItemTag_Item FOREIGN KEY (ItemId) REFERENCES dbo.Item (ItemId) ON DELETE CASCADE,
        CONSTRAINT FK_ItemTag_Tag  FOREIGN KEY (TagId)  REFERENCES dbo.Tag  (TagId)  ON DELETE NO ACTION
    );

    -- Запит: «усі предмети з тегом X» (зворотний напрямок, PK покриває лише Item -> Tags).
    CREATE NONCLUSTERED INDEX IX_ItemTag_TagId
        ON dbo.ItemTag (TagId, ItemId);
END
GO
