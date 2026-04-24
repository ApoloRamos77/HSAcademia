using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceEventLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "event_id",
                table: "attendances",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendances_event_id",
                table: "attendances",
                column: "event_id");

            migrationBuilder.AddForeignKey(
                name: "FK_attendances_events_event_id",
                table: "attendances",
                column: "event_id",
                principalTable: "events",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attendances_events_event_id",
                table: "attendances");

            migrationBuilder.DropIndex(
                name: "IX_attendances_event_id",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "event_id",
                table: "attendances");
        }
    }
}
