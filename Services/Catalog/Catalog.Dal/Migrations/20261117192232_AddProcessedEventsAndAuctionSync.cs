using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Dal.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedEventsAndAuctionSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AuctionStatusSyncedAt",
                schema: "dbo",
                table: "Item",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessedEvents",
                schema: "dbo",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEvents_ProcessedAt",
                schema: "dbo",
                table: "ProcessedEvents",
                column: "ProcessedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedEvents",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "AuctionStatusSyncedAt",
                schema: "dbo",
                table: "Item");
        }
    }
}
