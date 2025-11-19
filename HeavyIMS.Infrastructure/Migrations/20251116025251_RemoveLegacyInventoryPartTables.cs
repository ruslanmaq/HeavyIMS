using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeavyIMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyInventoryPartTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyInventoryTransaction");

            migrationBuilder.DropTable(
                name: "InventoryParts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BinLocation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    MaximumStockLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    MinimumStockLevel = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PartName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    QuantityReserved = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ReorderQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupplierPartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Warehouse = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryParts", x => x.Id);
                    table.CheckConstraint("CK_InventoryParts_Quantities", "[QuantityOnHand] >= 0 AND [QuantityReserved] >= 0 AND [QuantityReserved] <= [QuantityOnHand]");
                });

            migrationBuilder.CreateTable(
                name: "LegacyInventoryTransaction",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransactionType = table.Column<int>(type: "int", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyInventoryTransaction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyInventoryTransaction_InventoryParts_PartId",
                        column: x => x.PartId,
                        principalTable: "InventoryParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryParts_Category",
                table: "InventoryParts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryParts_IsActive",
                table: "InventoryParts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryParts_LowStock",
                table: "InventoryParts",
                columns: new[] { "IsActive", "QuantityOnHand", "MinimumStockLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryParts_PartNumber_Unique",
                table: "InventoryParts",
                column: "PartNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyInventoryTransaction_PartId",
                table: "LegacyInventoryTransaction",
                column: "PartId");
        }
    }
}
