using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase16PaymentReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dedupe_key",
                table: "family_notices",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "payment_record_id",
                table: "family_notices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "app_version",
                table: "device_tokens",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_family_notices_dedupe_key",
                table: "family_notices",
                column: "dedupe_key",
                unique: true,
                filter: "dedupe_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_family_notices_dedupe_key",
                table: "family_notices");

            migrationBuilder.DropColumn(
                name: "dedupe_key",
                table: "family_notices");

            migrationBuilder.DropColumn(
                name: "payment_record_id",
                table: "family_notices");

            migrationBuilder.DropColumn(
                name: "app_version",
                table: "device_tokens");
        }
    }
}
