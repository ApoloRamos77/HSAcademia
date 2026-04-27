using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentInstallmentsAndProration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "amount_paid",
                table: "payment_records",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "exclusion_note",
                table: "payment_records",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "exclusion_type",
                table: "payment_records",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_prorated",
                table: "payment_records",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "prorated_days_charged",
                table: "payment_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "prorated_start_date",
                table: "payment_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "prorated_total_days",
                table: "payment_records",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_installments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_paid = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    operation_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    voucher_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_installments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_installments_payment_records_payment_record_id",
                        column: x => x.payment_record_id,
                        principalTable: "payment_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_installments_payment_record_id",
                table: "payment_installments",
                column: "payment_record_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_installments");

            migrationBuilder.DropColumn(
                name: "amount_paid",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "exclusion_note",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "exclusion_type",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "is_prorated",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "prorated_days_charged",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "prorated_start_date",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "prorated_total_days",
                table: "payment_records");
        }
    }
}
