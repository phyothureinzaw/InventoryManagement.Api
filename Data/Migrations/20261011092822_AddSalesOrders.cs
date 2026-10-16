using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "SalesOrderNumberSequence",
                schema: "dbo");

            migrationBuilder.AddColumn<int>(
                name: "ReservedStock",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValueSql: "'SO-' + RIGHT('00000000' + CONVERT(varchar(20), NEXT VALUE FOR dbo.SalesOrderNumberSequence), 8)"),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrders", x => x.Id);
                    table.CheckConstraint("CK_SalesOrders_Status_Valid", "[Status] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesFulfillments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FulfilledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FulfilledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesFulfillments", x => x.Id);
                    table.UniqueConstraint("AK_SalesFulfillments_Id_SalesOrderId", x => new { x.Id, x.SalesOrderId });
                    table.ForeignKey(
                        name: "FK_SalesFulfillments_AspNetUsers_FulfilledByUserId",
                        column: x => x.FulfilledByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesFulfillments_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedQuantity = table.Column<int>(type: "int", nullable: false),
                    FulfilledQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderLines", x => x.Id);
                    table.UniqueConstraint("AK_SalesOrderLines_Id_SalesOrderId", x => new { x.Id, x.SalesOrderId });
                    table.UniqueConstraint("AK_SalesOrderLines_Id_SalesOrderId_ProductId", x => new { x.Id, x.SalesOrderId, x.ProductId });
                    table.CheckConstraint("CK_SalesOrderLines_FulfilledQuantity_NonNegative", "[FulfilledQuantity] >= 0");
                    table.CheckConstraint("CK_SalesOrderLines_FulfilledQuantity_NotOverOrdered", "[FulfilledQuantity] <= [OrderedQuantity]");
                    table.CheckConstraint("CK_SalesOrderLines_OrderedQuantity_Positive", "[OrderedQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_SalesOrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderLines_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesFulfillmentLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesFulfillmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesFulfillmentLines", x => x.Id);
                    table.CheckConstraint("CK_SalesFulfillmentLines_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_SalesFulfillmentLines_SalesFulfillments_SalesFulfillmentId_SalesOrderId",
                        columns: x => new { x.SalesFulfillmentId, x.SalesOrderId },
                        principalTable: "SalesFulfillments",
                        principalColumns: new[] { "Id", "SalesOrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesFulfillmentLines_SalesOrderLines_SalesOrderLineId_SalesOrderId",
                        columns: x => new { x.SalesOrderLineId, x.SalesOrderId },
                        principalTable: "SalesOrderLines",
                        principalColumns: new[] { "Id", "SalesOrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesFulfillmentLines_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalesOrderReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false),
                    FulfilledQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ReleasedQuantity = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderReservations", x => x.Id);
                    table.UniqueConstraint("AK_SalesOrderReservations_Line_Order_Product", x => new { x.SalesOrderLineId, x.SalesOrderId, x.ProductId });
                    table.CheckConstraint("CK_SalesOrderReservations_ConsumedQuantity_NotOverReserved", "CONVERT(bigint, [FulfilledQuantity]) + CONVERT(bigint, [ReleasedQuantity]) <= CONVERT(bigint, [ReservedQuantity])");
                    table.CheckConstraint("CK_SalesOrderReservations_FulfilledQuantity_NonNegative", "[FulfilledQuantity] >= 0");
                    table.CheckConstraint("CK_SalesOrderReservations_ReleasedQuantity_NonNegative", "[ReleasedQuantity] >= 0");
                    table.CheckConstraint("CK_SalesOrderReservations_ReservedQuantity_Positive", "[ReservedQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_SalesOrderReservations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderReservations_SalesOrderLines_SalesOrderLineId_SalesOrderId_ProductId",
                        columns: x => new { x.SalesOrderLineId, x.SalesOrderId, x.ProductId },
                        principalTable: "SalesOrderLines",
                        principalColumns: new[] { "Id", "SalesOrderId", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalesOrderReservations_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ReservedStock_NonNegative",
                table: "Products",
                sql: "[ReservedStock] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ReservedStock_NotOverCurrent",
                table: "Products",
                sql: "[ReservedStock] <= [CurrentStock]");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_IsActive_Name_Id",
                table: "Customers",
                columns: new[] { "IsActive", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesFulfillmentLines_SalesFulfillmentId_SalesOrderId",
                table: "SalesFulfillmentLines",
                columns: new[] { "SalesFulfillmentId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesFulfillmentLines_SalesFulfillmentId_SalesOrderLineId",
                table: "SalesFulfillmentLines",
                columns: new[] { "SalesFulfillmentId", "SalesOrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesFulfillmentLines_SalesOrderLineId",
                table: "SalesFulfillmentLines",
                column: "SalesOrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesFulfillmentLines_SalesOrderLineId_SalesOrderId",
                table: "SalesFulfillmentLines",
                columns: new[] { "SalesOrderLineId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesFulfillmentLines_StockMovementId",
                table: "SalesFulfillmentLines",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesFulfillments_FulfilledByUserId",
                table: "SalesFulfillments",
                column: "FulfilledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesFulfillments_RequestId",
                table: "SalesFulfillments",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesFulfillments_SalesOrderId_FulfilledAtUtc",
                table: "SalesFulfillments",
                columns: new[] { "SalesOrderId", "FulfilledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_ProductId",
                table: "SalesOrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_SalesOrderId_ProductId",
                table: "SalesOrderLines",
                columns: new[] { "SalesOrderId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderReservations_ProductId_SalesOrderId",
                table: "SalesOrderReservations",
                columns: new[] { "ProductId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderReservations_SalesOrderId_SalesOrderLineId",
                table: "SalesOrderReservations",
                columns: new[] { "SalesOrderId", "SalesOrderLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CancelledByUserId",
                table: "SalesOrders",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_ConfirmedByUserId",
                table: "SalesOrders",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CreatedByUserId",
                table: "SalesOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId_Status_CreatedAtUtc",
                table: "SalesOrders",
                columns: new[] { "CustomerId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_OrderNumber",
                table: "SalesOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_Status_CreatedAtUtc",
                table: "SalesOrders",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalesFulfillmentLines");

            migrationBuilder.DropTable(
                name: "SalesOrderReservations");

            migrationBuilder.DropTable(
                name: "SalesFulfillments");

            migrationBuilder.DropTable(
                name: "SalesOrderLines");

            migrationBuilder.DropTable(
                name: "SalesOrders");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ReservedStock_NonNegative",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ReservedStock_NotOverCurrent",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReservedStock",
                table: "Products");

            migrationBuilder.DropSequence(
                name: "SalesOrderNumberSequence",
                schema: "dbo");
        }
    }
}
