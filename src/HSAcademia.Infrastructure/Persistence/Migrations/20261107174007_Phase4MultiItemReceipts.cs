using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4MultiItemReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payer_document = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    guardian_id = table.Column<Guid>(type: "uuid", nullable: true),
                    method = table.Column<int>(type: "integer", nullable: false),
                    operation_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    voucher_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    discount_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    client_request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    voided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipts", x => x.id);
                    table.ForeignKey(
                        name: "FK_receipts_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "receipt_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    discount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    payment_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_installment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_receipt_lines_receipts_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_lines_payment_installment_id",
                table: "receipt_lines",
                column: "payment_installment_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_lines_payment_record_id",
                table: "receipt_lines",
                column: "payment_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_lines_product_sale_id",
                table: "receipt_lines",
                column: "product_sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_lines_receipt_id",
                table: "receipt_lines",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_lines_student_id",
                table: "receipt_lines",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_academy_id_client_request_id",
                table: "receipts",
                columns: new[] { "academy_id", "client_request_id" },
                unique: true,
                filter: "client_request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_receipts_academy_id_issued_at",
                table: "receipts",
                columns: new[] { "academy_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "IX_receipts_academy_id_number",
                table: "receipts",
                columns: new[] { "academy_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_lines");

            migrationBuilder.DropTable(
                name: "receipts");
        }
    }
}
