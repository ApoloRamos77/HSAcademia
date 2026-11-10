using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase15PushNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "pushed_at",
                table: "family_notices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "pushed_at",
                table: "announcements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_device_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_family_notices_pending_push",
                table: "family_notices",
                column: "created_at",
                filter: "pushed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_pending_push",
                table: "announcements",
                column: "created_at",
                filter: "pushed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_academy_id",
                table: "device_tokens",
                column: "academy_id");

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_token",
                table: "device_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_tokens_user_id",
                table: "device_tokens",
                column: "user_id");

            // Lo anterior a esta versión ya se vio dentro de la app: no se notifica a nadie.
            migrationBuilder.Sql("UPDATE family_notices SET pushed_at = NOW() WHERE pushed_at IS NULL;");
            migrationBuilder.Sql("UPDATE announcements SET pushed_at = NOW() WHERE pushed_at IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_tokens");

            migrationBuilder.DropIndex(
                name: "IX_family_notices_pending_push",
                table: "family_notices");

            migrationBuilder.DropIndex(
                name: "IX_announcements_pending_push",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "pushed_at",
                table: "family_notices");

            migrationBuilder.DropColumn(
                name: "pushed_at",
                table: "announcements");
        }
    }
}
