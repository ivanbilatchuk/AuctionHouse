IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[Category] (
        [CategoryId] bigint NOT NULL IDENTITY,
        [ParentCategoryId] bigint NULL,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(120) NOT NULL,
        [AttributeKind] nvarchar(20) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NOT NULL DEFAULT (SUSER_SNAME()),
        [UpdatedAt] datetime2(3) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Category] PRIMARY KEY ([CategoryId]),
        CONSTRAINT [CK_Category_AttributeKind] CHECK ([AttributeKind] IN (N'generic', N'cards', N'stamps', N'vinyl', N'coins')),
        CONSTRAINT [CK_Category_NotOwnParent] CHECK ([ParentCategoryId] IS NULL OR [ParentCategoryId] <> [CategoryId]),
        CONSTRAINT [FK_Category_Parent] FOREIGN KEY ([ParentCategoryId]) REFERENCES [dbo].[Category] ([CategoryId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[Tag] (
        [TagId] bigint NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [Slug] nvarchar(60) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NOT NULL DEFAULT (SUSER_SNAME()),
        CONSTRAINT [PK_Tag] PRIMARY KEY ([TagId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[Item] (
        [ItemId] bigint NOT NULL IDENTITY,
        [CategoryId] bigint NOT NULL,
        [SellerExternalId] uniqueidentifier NOT NULL,
        [Sku] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Slug] nvarchar(220) NOT NULL,
        [EstimateLow] decimal(18,2) NOT NULL,
        [EstimateHigh] decimal(18,2) NOT NULL,
        [Currency] char(3) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NOT NULL DEFAULT (SUSER_SNAME()),
        [UpdatedAt] datetime2(3) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Item] PRIMARY KEY ([ItemId]),
        CONSTRAINT [CK_Item_Estimate] CHECK ([EstimateLow] > 0 AND [EstimateHigh] >= [EstimateLow]),
        CONSTRAINT [CK_Item_Status] CHECK ([Status] IN (N'Draft', N'Listed', N'InAuction', N'Sold', N'Withdrawn')),
        CONSTRAINT [FK_Item_Category] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Category] ([CategoryId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[ItemDetail] (
        [ItemId] bigint NOT NULL,
        [Condition] nvarchar(20) NOT NULL,
        [ProductionYear] smallint NULL,
        [CountryOfOrigin] nvarchar(100) NULL,
        [CertificateNumber] nvarchar(50) NULL,
        [Provenance] nvarchar(2000) NULL,
        [Description] nvarchar(max) NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NOT NULL DEFAULT (SUSER_SNAME()),
        [UpdatedAt] datetime2(3) NULL,
        [UpdatedBy] nvarchar(100) NULL,
        CONSTRAINT [PK_ItemDetail] PRIMARY KEY ([ItemId]),
        CONSTRAINT [CK_ItemDetail_Condition] CHECK ([Condition] IN (N'Mint', N'NearMint', N'Excellent', N'Good', N'Fair', N'Poor')),
        CONSTRAINT [CK_ItemDetail_Year] CHECK ([ProductionYear] IS NULL OR [ProductionYear] BETWEEN 1000 AND 2100),
        CONSTRAINT [FK_ItemDetail_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[ItemImage] (
        [ImageId] bigint NOT NULL IDENTITY,
        [ItemId] bigint NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [IsPrimary] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedBy] nvarchar(100) NOT NULL DEFAULT (SUSER_SNAME()),
        CONSTRAINT [PK_ItemImage] PRIMARY KEY ([ImageId]),
        CONSTRAINT [CK_ItemImage_Url] CHECK ([Url] LIKE N'http%'),
        CONSTRAINT [FK_ItemImage_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE TABLE [dbo].[ItemTag] (
        [ItemId] bigint NOT NULL,
        [TagId] bigint NOT NULL,
        [AddedAt] datetime2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
        [AddedBy] nvarchar(100) NOT NULL DEFAULT (SUSER_SNAME()),
        CONSTRAINT [PK_ItemTag] PRIMARY KEY ([ItemId], [TagId]),
        CONSTRAINT [FK_ItemTag_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ItemTag_Tag] FOREIGN KEY ([TagId]) REFERENCES [dbo].[Tag] ([TagId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryId', N'AttributeKind', N'CreatedAt', N'CreatedBy', N'IsDeleted', N'Name', N'ParentCategoryId', N'Slug', N'SortOrder', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[dbo].[Category]'))
        SET IDENTITY_INSERT [dbo].[Category] ON;
    EXEC(N'INSERT INTO [dbo].[Category] ([CategoryId], [AttributeKind], [CreatedAt], [CreatedBy], [IsDeleted], [Name], [ParentCategoryId], [Slug], [SortOrder], [UpdatedAt], [UpdatedBy])
    VALUES (CAST(1 AS bigint), N''cards'', ''2026-01-01T00:00:00.000Z'', N''seed'', CAST(0 AS bit), N''Trading Cards'', NULL, N''trading-cards'', 10, NULL, NULL),
    (CAST(3 AS bigint), N''stamps'', ''2026-01-01T00:00:00.000Z'', N''seed'', CAST(0 AS bit), N''Stamps'', NULL, N''stamps'', 20, NULL, NULL),
    (CAST(4 AS bigint), N''vinyl'', ''2026-01-01T00:00:00.000Z'', N''seed'', CAST(0 AS bit), N''Vinyl Records'', NULL, N''vinyl-records'', 30, NULL, NULL),
    (CAST(5 AS bigint), N''coins'', ''2026-01-01T00:00:00.000Z'', N''seed'', CAST(0 AS bit), N''Coins'', NULL, N''coins'', 40, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryId', N'AttributeKind', N'CreatedAt', N'CreatedBy', N'IsDeleted', N'Name', N'ParentCategoryId', N'Slug', N'SortOrder', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[dbo].[Category]'))
        SET IDENTITY_INSERT [dbo].[Category] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'TagId', N'CreatedAt', N'CreatedBy', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[dbo].[Tag]'))
        SET IDENTITY_INSERT [dbo].[Tag] ON;
    EXEC(N'INSERT INTO [dbo].[Tag] ([TagId], [CreatedAt], [CreatedBy], [Name], [Slug])
    VALUES (CAST(1 AS bigint), ''2026-01-01T00:00:00.000Z'', N''seed'', N''First Edition'', N''first-edition''),
    (CAST(2 AS bigint), ''2026-01-01T00:00:00.000Z'', N''seed'', N''Graded'', N''graded''),
    (CAST(3 AS bigint), ''2026-01-01T00:00:00.000Z'', N''seed'', N''Rare'', N''rare''),
    (CAST(4 AS bigint), ''2026-01-01T00:00:00.000Z'', N''seed'', N''Limited Edition'', N''limited-edition''),
    (CAST(5 AS bigint), ''2026-01-01T00:00:00.000Z'', N''seed'', N''Certified'', N''certified'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'TagId', N'CreatedAt', N'CreatedBy', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[dbo].[Tag]'))
        SET IDENTITY_INSERT [dbo].[Tag] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryId', N'AttributeKind', N'CreatedAt', N'CreatedBy', N'IsDeleted', N'Name', N'ParentCategoryId', N'Slug', N'SortOrder', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[dbo].[Category]'))
        SET IDENTITY_INSERT [dbo].[Category] ON;
    EXEC(N'INSERT INTO [dbo].[Category] ([CategoryId], [AttributeKind], [CreatedAt], [CreatedBy], [IsDeleted], [Name], [ParentCategoryId], [Slug], [SortOrder], [UpdatedAt], [UpdatedBy])
    VALUES (CAST(2 AS bigint), N''cards'', ''2026-01-01T00:00:00.000Z'', N''seed'', CAST(0 AS bit), N''Pokemon Cards'', CAST(1 AS bigint), N''pokemon-cards'', 10, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryId', N'AttributeKind', N'CreatedAt', N'CreatedBy', N'IsDeleted', N'Name', N'ParentCategoryId', N'Slug', N'SortOrder', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[dbo].[Category]'))
        SET IDENTITY_INSERT [dbo].[Category] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Category_ParentCategoryId] ON [dbo].[Category] ([ParentCategoryId], [SortOrder]) INCLUDE ([Name], [Slug]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Category_Slug] ON [dbo].[Category] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Item_CategoryId_Status] ON [dbo].[Item] ([CategoryId], [Status]) INCLUDE ([Title], [Slug], [EstimateLow], [EstimateHigh]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Item_SellerExternalId] ON [dbo].[Item] ([SellerExternalId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Item_Title] ON [dbo].[Item] ([Title]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Item_Sku] ON [dbo].[Item] ([Sku]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Item_Slug] ON [dbo].[Item] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ItemDetail_CertificateNumber] ON [dbo].[ItemDetail] ([CertificateNumber]) WHERE [CertificateNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ItemImage_ItemId_SortOrder] ON [dbo].[ItemImage] ([ItemId], [SortOrder]) INCLUDE ([Url], [IsPrimary]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ItemImage_OnePrimaryPerItem] ON [dbo].[ItemImage] ([ItemId]) WHERE [IsPrimary] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ItemTag_TagId_AddedAt] ON [dbo].[ItemTag] ([TagId], [AddedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Tag_Name] ON [dbo].[Tag] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Tag_Slug] ON [dbo].[Tag] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023204607_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023204607_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

