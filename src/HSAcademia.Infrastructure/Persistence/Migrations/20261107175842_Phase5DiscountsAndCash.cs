using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5DiscountsAndCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cash_handover_id",
                table: "receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discount_label",
                table: "receipt_lines",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discount_detail",
                table: "payment_records",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cash_handovers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collector_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collector_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    declared_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    receipts_count = table.Column<int>(type: "integer", nullable: false),
                    expected_cash = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    digital_total = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    declared_cash = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    received_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    received_cash = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    received_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_handovers", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_handovers_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "discount_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    value_type = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    from_sibling_number = table.Column<int>(type: "integer", nullable: true),
                    days_before_due = table.Column<int>(type: "integer", nullable: true),
                    valid_from = table.Column<DateTime>(type: "date", nullable: true),
                    valid_to = table.Column<DateTime>(type: "date", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discount_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_discount_rules_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receipts_academy_id_created_by_user_id_cash_handover_id",
                table: "receipts",
                columns: new[] { "academy_id", "created_by_user_id", "cash_handover_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipts_cash_handover_id",
                table: "receipts",
                column: "cash_handover_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_handovers_academy_id_collector_user_id_declared_at",
                table: "cash_handovers",
                columns: new[] { "academy_id", "collector_user_id", "declared_at" });

            migrationBuilder.CreateIndex(
                name: "IX_cash_handovers_academy_id_status",
                table: "cash_handovers",
                columns: new[] { "academy_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_discount_rules_academy_id_kind",
                table: "discount_rules",
                columns: new[] { "academy_id", "kind" });

            migrationBuilder.AddForeignKey(
                name: "FK_receipts_cash_handovers_cash_handover_id",
                table: "receipts",
                column: "cash_handover_id",
                principalTable: "cash_handovers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_receipts_cash_handovers_cash_handover_id",
                table: "receipts");

            migrationBuilder.DropTable(
                name: "cash_handovers");

            migrationBuilder.DropTable(
                name: "discount_rules");

            migrationBuilder.DropIndex(
                name: "IX_receipts_academy_id_created_by_user_id_cash_handover_id",
                table: "receipts");

            migrationBuilder.DropIndex(
                name: "IX_receipts_cash_handover_id",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "cash_handover_id",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "discount_label",
                table: "receipt_lines");

            migrationBuilder.DropColumn(
                name: "discount_detail",
                table: "payment_records");
        }
    }
}
