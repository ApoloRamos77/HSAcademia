using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase21Payroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_payments_academy_id_staff_id_period_month_period_year",
                table: "staff_payments");

            migrationBuilder.AddColumn<int>(
                name: "pay_frequency",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "pay_scale_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "staff_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approved_by_name",
                table: "staff_payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "detail",
                table: "staff_payments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "frequency",
                table: "staff_payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "hours",
                table: "staff_payments",
                type: "numeric(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "staff_payments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "period_end",
                table: "staff_payments",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "period_start",
                table: "staff_payments",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "units_amount",
                table: "staff_payments",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "staff_payment_id",
                table: "staff_attendances",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pay_scales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    unit = table.Column<int>(type: "integer", nullable: false),
                    tiers = table.Column<string>(type: "jsonb", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pay_scales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_advances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    concept = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    staff_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_advances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "staff_payment_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    concept = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    staff_advance_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_payment_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_payment_lines_staff_payments_staff_payment_id",
                        column: x => x.staff_payment_id,
                        principalTable: "staff_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "pay_scale_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_staff_payments_academy_id_staff_id_period_start",
                table: "staff_payments",
                columns: new[] { "academy_id", "staff_id", "period_start" });

            migrationBuilder.CreateIndex(
                name: "IX_pay_scales_academy_id",
                table: "pay_scales",
                column: "academy_id");

            migrationBuilder.CreateIndex(
                name: "IX_staff_advances_academy_id_staff_id",
                table: "staff_advances",
                columns: new[] { "academy_id", "staff_id" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_payment_lines_staff_payment_id",
                table: "staff_payment_lines",
                column: "staff_payment_id");

            // Liquidaciones anteriores: periodo = mes calendario; sus bonos y descuentos pasan a líneas con concepto.
            migrationBuilder.Sql(@"
                UPDATE staff_payments
                SET period_start = make_date(period_year, period_month, 1),
                    period_end = (make_date(period_year, period_month, 1) + interval '1 month - 1 day')::date,
                    frequency = 0
                WHERE period_start = '0001-01-01' OR period_start IS NULL;
                INSERT INTO staff_payment_lines (id, staff_payment_id, kind, concept, amount, staff_advance_id)
                SELECT gen_random_uuid(), id, 0, 'Bonos (registro anterior)', bonuses, NULL FROM staff_payments WHERE bonuses > 0;
                INSERT INTO staff_payment_lines (id, staff_payment_id, kind, concept, amount, staff_advance_id)
                SELECT gen_random_uuid(), id, 1, 'Descuentos (registro anterior)', deductions, NULL FROM staff_payments WHERE deductions > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pay_scales");

            migrationBuilder.DropTable(
                name: "staff_advances");

            migrationBuilder.DropTable(
                name: "staff_payment_lines");

            migrationBuilder.DropIndex(
                name: "IX_staff_payments_academy_id_staff_id_period_start",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "pay_frequency",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pay_scale_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "approved_by_name",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "detail",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "frequency",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "hours",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "period_end",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "period_start",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "units_amount",
                table: "staff_payments");

            migrationBuilder.DropColumn(
                name: "staff_payment_id",
                table: "staff_attendances");

            migrationBuilder.CreateIndex(
                name: "IX_staff_payments_academy_id_staff_id_period_month_period_year",
                table: "staff_payments",
                columns: new[] { "academy_id", "staff_id", "period_month", "period_year" },
                unique: true);
        }
    }
}
