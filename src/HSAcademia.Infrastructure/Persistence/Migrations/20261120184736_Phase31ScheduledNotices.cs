using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase31ScheduledNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "birthday_message",
                table: "academies",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_birthday_enabled",
                table: "academies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "notify_birthday_hour",
                table: "academies",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<bool>(
                name: "notify_birthday_staff",
                table: "academies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "notify_match_enabled",
                table: "academies",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "notify_match_hours_before",
                table: "academies",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "notify_match_no_time_hour",
                table: "academies",
                type: "integer",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.CreateTable(
                name: "staff_notices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    body = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    pushed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_notices", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_notices_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_notices_dedupe_key",
                table: "staff_notices",
                column: "dedupe_key",
                unique: true,
                filter: "dedupe_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_staff_notices_pending_push",
                table: "staff_notices",
                column: "created_at",
                filter: "pushed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_staff_notices_user_id",
                table: "staff_notices",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_notices");

            migrationBuilder.DropColumn(
                name: "birthday_message",
                table: "academies");

            migrationBuilder.DropColumn(
                name: "notify_birthday_enabled",
                table: "academies");

            migrationBuilder.DropColumn(
                name: "notify_birthday_hour",
                table: "academies");

            migrationBuilder.DropColumn(
                name: "notify_birthday_staff",
                table: "academies");

            migrationBuilder.DropColumn(
                name: "notify_match_enabled",
                table: "academies");

            migrationBuilder.DropColumn(
                name: "notify_match_hours_before",
                table: "academies");

            migrationBuilder.DropColumn(
                name: "notify_match_no_time_hour",
                table: "academies");
        }
    }
}
