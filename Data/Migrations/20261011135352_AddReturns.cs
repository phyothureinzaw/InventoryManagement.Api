using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryManagement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "CustomerReturnNumberSequence",
                schema: "dbo");

            migrationBuilder.CreateSequence(
                name: "SupplierReturnNumberSequence",
                schema: "dbo");

            migrationBuilder.AddColumn<int>(
                name: "ReturnedQuantity",
                table: "SalesFulfillmentLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SalesFulfillmentLines",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "ReturnedQuantity",
                table: "PurchaseReceiptLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseReceiptLines",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_SalesFulfillmentLines_Id_Fulfillment_Order",
                table: "SalesFulfillmentLines",
                columns: new[] { "Id", "SalesFulfillmentId", "SalesOrderId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PurchaseReceiptLines_Id_Receipt_Order",
                table: "PurchaseReceiptLines",
                columns: new[] { "Id", "PurchaseReceiptId", "PurchaseOrderId" });

            migrationBuilder.CreateTable(
                name: "CustomerReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValueSql: "'CR-' + RIGHT('00000000' + CONVERT(varchar(20), NEXT VALUE FOR dbo.CustomerReturnNumberSequence), 8)"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesFulfillmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturns", x => x.Id);
                    table.UniqueConstraint("AK_CustomerReturns_Id_Fulfillment_Order", x => new { x.Id, x.SalesFulfillmentId, x.SalesOrderId });
                    table.CheckConstraint("CK_CustomerReturns_Reason_NotBlank", "ISNULL(LEN(LTRIM(RTRIM([Reason]))), 0) > 0");
                    table.ForeignKey(
                        name: "FK_CustomerReturns_AspNetUsers_ReturnedByUserId",
                        column: x => x.ReturnedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturns_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturns_SalesFulfillments_SalesFulfillmentId_SalesOrderId",
                        columns: x => new { x.SalesFulfillmentId, x.SalesOrderId },
                        principalTable: "SalesFulfillments",
                        principalColumns: new[] { "Id", "SalesOrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturns_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValueSql: "'SR-' + RIGHT('00000000' + CONVERT(varchar(20), NEXT VALUE FOR dbo.SupplierReturnNumberSequence), 8)"),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturns", x => x.Id);
                    table.UniqueConstraint("AK_SupplierReturns_Id_Receipt_Order", x => new { x.Id, x.PurchaseReceiptId, x.PurchaseOrderId });
                    table.CheckConstraint("CK_SupplierReturns_Reason_NotBlank", "ISNULL(LEN(LTRIM(RTRIM([Reason]))), 0) > 0");
                    table.ForeignKey(
                        name: "FK_SupplierReturns_AspNetUsers_ReturnedByUserId",
                        column: x => x.ReturnedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_PurchaseReceipts_PurchaseReceiptId_PurchaseOrderId",
                        columns: x => new { x.PurchaseReceiptId, x.PurchaseOrderId },
                        principalTable: "PurchaseReceipts",
                        principalColumns: new[] { "Id", "PurchaseOrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturns_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerReturnLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesFulfillmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesFulfillmentLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerReturnLines", x => x.Id);
                    table.CheckConstraint("CK_CustomerReturnLines_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_CustomerReturns_CustomerReturnId",
                        column: x => x.CustomerReturnId,
                        principalTable: "CustomerReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_SalesFulfillmentLines_SalesFulfillmentLineId_SalesFulfillmentId_SalesOrderId",
                        columns: x => new { x.SalesFulfillmentLineId, x.SalesFulfillmentId, x.SalesOrderId },
                        principalTable: "SalesFulfillmentLines",
                        principalColumns: new[] { "Id", "SalesFulfillmentId", "SalesOrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerReturnLines_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierReturnLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseReceiptLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierReturnLines", x => x.Id);
                    table.CheckConstraint("CK_SupplierReturnLines_Quantity_Positive", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_PurchaseReceiptLines_PurchaseReceiptLineId_PurchaseReceiptId_PurchaseOrderId",
                        columns: x => new { x.PurchaseReceiptLineId, x.PurchaseReceiptId, x.PurchaseOrderId },
                        principalTable: "PurchaseReceiptLines",
                        principalColumns: new[] { "Id", "PurchaseReceiptId", "PurchaseOrderId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_StockMovements_StockMovementId",
                        column: x => x.StockMovementId,
                        principalTable: "StockMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierReturnLines_SupplierReturns_SupplierReturnId",
                        column: x => x.SupplierReturnId,
                        principalTable: "SupplierReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesFulfillmentLines_ReturnedQuantity_NonNegative",
                table: "SalesFulfillmentLines",
                sql: "[ReturnedQuantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalesFulfillmentLines_ReturnedQuantity_NotOverQuantity",
                table: "SalesFulfillmentLines",
                sql: "[ReturnedQuantity] <= [Quantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReceiptLines_ReturnedQuantity_NonNegative",
                table: "PurchaseReceiptLines",
                sql: "[ReturnedQuantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseReceiptLines_ReturnedQuantity_NotOverQuantity",
                table: "PurchaseReceiptLines",
                sql: "[ReturnedQuantity] <= [Quantity]");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_CustomerReturnId_SalesFulfillmentLineId",
                table: "CustomerReturnLines",
                columns: new[] { "CustomerReturnId", "SalesFulfillmentLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_SalesFulfillmentId_SalesOrderId_SalesFulfillmentLineId",
                table: "CustomerReturnLines",
                columns: new[] { "SalesFulfillmentId", "SalesOrderId", "SalesFulfillmentLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_SalesFulfillmentLineId_SalesFulfillmentId_SalesOrderId",
                table: "CustomerReturnLines",
                columns: new[] { "SalesFulfillmentLineId", "SalesFulfillmentId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturnLines_StockMovementId",
                table: "CustomerReturnLines",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_CustomerId",
                table: "CustomerReturns",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_RequestId",
                table: "CustomerReturns",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_ReturnedByUserId",
                table: "CustomerReturns",
                column: "ReturnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_ReturnNumber",
                table: "CustomerReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_SalesFulfillmentId_ReturnedAtUtc",
                table: "CustomerReturns",
                columns: new[] { "SalesFulfillmentId", "ReturnedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_SalesFulfillmentId_SalesOrderId",
                table: "CustomerReturns",
                columns: new[] { "SalesFulfillmentId", "SalesOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerReturns_SalesOrderId",
                table: "CustomerReturns",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_PurchaseReceiptId_PurchaseOrderId_PurchaseReceiptLineId",
                table: "SupplierReturnLines",
                columns: new[] { "PurchaseReceiptId", "PurchaseOrderId", "PurchaseReceiptLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_PurchaseReceiptLineId_PurchaseReceiptId_PurchaseOrderId",
                table: "SupplierReturnLines",
                columns: new[] { "PurchaseReceiptLineId", "PurchaseReceiptId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_StockMovementId",
                table: "SupplierReturnLines",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturnLines_SupplierReturnId_PurchaseReceiptLineId",
                table: "SupplierReturnLines",
                columns: new[] { "SupplierReturnId", "PurchaseReceiptLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_PurchaseOrderId",
                table: "SupplierReturns",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_PurchaseReceiptId_PurchaseOrderId",
                table: "SupplierReturns",
                columns: new[] { "PurchaseReceiptId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_PurchaseReceiptId_ReturnedAtUtc",
                table: "SupplierReturns",
                columns: new[] { "PurchaseReceiptId", "ReturnedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_RequestId",
                table: "SupplierReturns",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_ReturnedByUserId",
                table: "SupplierReturns",
                column: "ReturnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_ReturnNumber",
                table: "SupplierReturns",
                column: "ReturnNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierReturns_SupplierId",
                table: "SupplierReturns",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerReturnLines");

            migrationBuilder.DropTable(
                name: "SupplierReturnLines");

            migrationBuilder.DropTable(
                name: "CustomerReturns");

            migrationBuilder.DropTable(
                name: "SupplierReturns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_SalesFulfillmentLines_Id_Fulfillment_Order",
                table: "SalesFulfillmentLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesFulfillmentLines_ReturnedQuantity_NonNegative",
                table: "SalesFulfillmentLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalesFulfillmentLines_ReturnedQuantity_NotOverQuantity",
                table: "SalesFulfillmentLines");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PurchaseReceiptLines_Id_Receipt_Order",
                table: "PurchaseReceiptLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReceiptLines_ReturnedQuantity_NonNegative",
                table: "PurchaseReceiptLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseReceiptLines_ReturnedQuantity_NotOverQuantity",
                table: "PurchaseReceiptLines");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                table: "SalesFulfillmentLines");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SalesFulfillmentLines");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                table: "PurchaseReceiptLines");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseReceiptLines");

            migrationBuilder.DropSequence(
                name: "CustomerReturnNumberSequence",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "SupplierReturnNumberSequence",
                schema: "dbo");
        }
    }
}
