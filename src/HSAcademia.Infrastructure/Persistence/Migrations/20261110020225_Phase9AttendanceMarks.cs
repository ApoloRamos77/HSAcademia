using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9AttendanceMarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "marked_at",
                table: "attendances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marked_by_name",
                table: "attendances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "marked_by_user_id",
                table: "attendances",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "attendances",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "marked_at",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "marked_by_name",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "marked_by_user_id",
                table: "attendances");

            migrationBuilder.DropColumn(
                name: "source",
                table: "attendances");
        }
    }
}
