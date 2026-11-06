using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1PaymentPeriodAndAttendanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_records_academy_id",
                table: "payment_records");

            migrationBuilder.DropIndex(
                name: "IX_attendances_student_id_date",
                table: "attendances");

            migrationBuilder.AddColumn<int>(
                name: "period_month",
                table: "payment_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "period_year",
                table: "payment_records",
                type: "integer",
                nullable: true);

            // Backfill: el periodo de cada mensualidad existente es el mes de su vencimiento.
            // due_date se guarda como medianoche UTC del día de vencimiento → se evalúa en UTC.
            // (SQL de PostgreSQL; en la migración a SQL Server se regeneran las migraciones.)
            migrationBuilder.Sql(@"
                UPDATE payment_records
                SET period_year  = CAST(EXTRACT(YEAR  FROM (due_date AT TIME ZONE 'UTC')) AS integer),
                    period_month = CAST(EXTRACT(MONTH FROM (due_date AT TIME ZONE 'UTC')) AS integer)
                WHERE type = 1 AND period_year IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_academy_id_student_id_type_period_year_peri~",
                table: "payment_records",
                columns: new[] { "academy_id", "student_id", "type", "period_year", "period_month" });

            migrationBuilder.CreateIndex(
                name: "IX_attendances_student_id_date",
                table: "attendances",
                columns: new[] { "student_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_attendances_student_id_event_id",
                table: "attendances",
                columns: new[] { "student_id", "event_id" },
                unique: true,
                filter: "event_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_records_academy_id_student_id_type_period_year_peri~",
                table: "payment_records");

            migrationBuilder.DropIndex(
                name: "IX_attendances_student_id_date",
                table: "attendances");

            migrationBuilder.DropIndex(
                name: "IX_attendances_student_id_event_id",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "period_month",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "period_year",
                table: "payment_records");

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_academy_id",
                table: "payment_records",
                column: "academy_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendances_student_id_date",
                table: "attendances",
                columns: new[] { "student_id", "date" },
                unique: true);
        }
    }
}
