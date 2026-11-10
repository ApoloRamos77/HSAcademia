using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11StaffAttendanceBySession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_attendances_staff_id_date",
                table: "staff_attendances");

            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                table: "staff_attendances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "marked_at",
                table: "staff_attendances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marked_by_name",
                table: "staff_attendances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "marked_by_user_id",
                table: "staff_attendances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "substitute_staff_id",
                table: "staff_attendances",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_attendances_event_id",
                table: "staff_attendances",
                column: "event_id",
                unique: true,
                filter: "event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_staff_attendances_staff_id_date",
                table: "staff_attendances",
                columns: new[] { "staff_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_staff_attendances_event_id",
                table: "staff_attendances");

            migrationBuilder.DropIndex(
                name: "IX_staff_attendances_staff_id_date",
                table: "staff_attendances");

            migrationBuilder.DropColumn(
                name: "event_id",
                table: "staff_attendances");

            migrationBuilder.DropColumn(
                name: "marked_at",
                table: "staff_attendances");

            migrationBuilder.DropColumn(
                name: "marked_by_name",
                table: "staff_attendances");

            migrationBuilder.DropColumn(
                name: "marked_by_user_id",
                table: "staff_attendances");

            migrationBuilder.DropColumn(
                name: "substitute_staff_id",
                table: "staff_attendances");

            migrationBuilder.CreateIndex(
                name: "IX_staff_attendances_staff_id_date",
                table: "staff_attendances",
                columns: new[] { "staff_id", "date" },
                unique: true);
        }
    }
}
