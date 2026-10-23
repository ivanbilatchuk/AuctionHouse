using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Catalog.Dal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Category",
                schema: "dbo",
                columns: table => new
                {
                    CategoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AttributeKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValueSql: "SUSER_SNAME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.CategoryId);
                    table.CheckConstraint("CK_Category_AttributeKind", "[AttributeKind] IN (N'generic', N'cards', N'stamps', N'vinyl', N'coins')");
                    table.CheckConstraint("CK_Category_NotOwnParent", "[ParentCategoryId] IS NULL OR [ParentCategoryId] <> [CategoryId]");
                    table.ForeignKey(
                        name: "FK_Category_Parent",
                        column: x => x.ParentCategoryId,
                        principalSchema: "dbo",
                        principalTable: "Category",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tag",
                schema: "dbo",
                columns: table => new
                {
                    TagId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValueSql: "SUSER_SNAME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tag", x => x.TagId);
                });

            migrationBuilder.CreateTable(
                name: "Item",
                schema: "dbo",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    SellerExternalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    EstimateLow = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimateHigh = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValueSql: "SUSER_SNAME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Item", x => x.ItemId);
                    table.CheckConstraint("CK_Item_Estimate", "[EstimateLow] > 0 AND [EstimateHigh] >= [EstimateLow]");
                    table.CheckConstraint("CK_Item_Status", "[Status] IN (N'Draft', N'Listed', N'InAuction', N'Sold', N'Withdrawn')");
                    table.ForeignKey(
                        name: "FK_Item_Category",
                        column: x => x.CategoryId,
                        principalSchema: "dbo",
                        principalTable: "Category",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItemDetail",
                schema: "dbo",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProductionYear = table.Column<short>(type: "smallint", nullable: true),
                    CountryOfOrigin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CertificateNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Provenance = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValueSql: "SUSER_SNAME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemDetail", x => x.ItemId);
                    table.CheckConstraint("CK_ItemDetail_Condition", "[Condition] IN (N'Mint', N'NearMint', N'Excellent', N'Good', N'Fair', N'Poor')");
                    table.CheckConstraint("CK_ItemDetail_Year", "[ProductionYear] IS NULL OR [ProductionYear] BETWEEN 1000 AND 2100");
                    table.ForeignKey(
                        name: "FK_ItemDetail_Item",
                        column: x => x.ItemId,
                        principalSchema: "dbo",
                        principalTable: "Item",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemImage",
                schema: "dbo",
                columns: table => new
                {
                    ImageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValueSql: "SUSER_SNAME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemImage", x => x.ImageId);
                    table.CheckConstraint("CK_ItemImage_Url", "[Url] LIKE N'http%'");
                    table.ForeignKey(
                        name: "FK_ItemImage_Item",
                        column: x => x.ItemId,
                        principalSchema: "dbo",
                        principalTable: "Item",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTag",
                schema: "dbo",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "bigint", nullable: false),
                    TagId = table.Column<long>(type: "bigint", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    AddedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValueSql: "SUSER_SNAME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTag", x => new { x.ItemId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ItemTag_Item",
                        column: x => x.ItemId,
                        principalSchema: "dbo",
                        principalTable: "Item",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemTag_Tag",
                        column: x => x.TagId,
                        principalSchema: "dbo",
                        principalTable: "Tag",
                        principalColumn: "TagId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Category",
                columns: new[] { "CategoryId", "AttributeKind", "CreatedAt", "CreatedBy", "IsDeleted", "Name", "ParentCategoryId", "Slug", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1L, "cards", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", false, "Trading Cards", null, "trading-cards", 10, null, null },
                    { 3L, "stamps", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", false, "Stamps", null, "stamps", 20, null, null },
                    { 4L, "vinyl", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", false, "Vinyl Records", null, "vinyl-records", 30, null, null },
                    { 5L, "coins", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", false, "Coins", null, "coins", 40, null, null }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Tag",
                columns: new[] { "TagId", "CreatedAt", "CreatedBy", "Name", "Slug" },
                values: new object[,]
                {
                    { 1L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "First Edition", "first-edition" },
                    { 2L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Graded", "graded" },
                    { 3L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Rare", "rare" },
                    { 4L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Limited Edition", "limited-edition" },
                    { 5L, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Certified", "certified" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Category",
                columns: new[] { "CategoryId", "AttributeKind", "CreatedAt", "CreatedBy", "IsDeleted", "Name", "ParentCategoryId", "Slug", "SortOrder", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 2L, "cards", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", false, "Pokemon Cards", 1L, "pokemon-cards", 10, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Category_ParentCategoryId",
                schema: "dbo",
                table: "Category",
                columns: new[] { "ParentCategoryId", "SortOrder" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Name", "Slug" });

            migrationBuilder.CreateIndex(
                name: "UQ_Category_Slug",
                schema: "dbo",
                table: "Category",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Item_CategoryId_Status",
                schema: "dbo",
                table: "Item",
                columns: new[] { "CategoryId", "Status" },
                filter: "[IsDeleted] = 0")
                .Annotation("SqlServer:Include", new[] { "Title", "Slug", "EstimateLow", "EstimateHigh" });

            migrationBuilder.CreateIndex(
                name: "IX_Item_SellerExternalId",
                schema: "dbo",
                table: "Item",
                column: "SellerExternalId",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Item_Title",
                schema: "dbo",
                table: "Item",
                column: "Title",
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_Item_Sku",
                schema: "dbo",
                table: "Item",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Item_Slug",
                schema: "dbo",
                table: "Item",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ItemDetail_CertificateNumber",
                schema: "dbo",
                table: "ItemDetail",
                column: "CertificateNumber",
                unique: true,
                filter: "[CertificateNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ItemImage_ItemId_SortOrder",
                schema: "dbo",
                table: "ItemImage",
                columns: new[] { "ItemId", "SortOrder" })
                .Annotation("SqlServer:Include", new[] { "Url", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "UX_ItemImage_OnePrimaryPerItem",
                schema: "dbo",
                table: "ItemImage",
                column: "ItemId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ItemTag_TagId_AddedAt",
                schema: "dbo",
                table: "ItemTag",
                columns: new[] { "TagId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "UQ_Tag_Name",
                schema: "dbo",
                table: "Tag",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Tag_Slug",
                schema: "dbo",
                table: "Tag",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemDetail",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ItemImage",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ItemTag",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Item",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Tag",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Category",
                schema: "dbo");
        }
    }
}
