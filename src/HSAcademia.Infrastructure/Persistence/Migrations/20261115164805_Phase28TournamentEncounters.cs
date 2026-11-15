using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase28TournamentEncounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "manual_waiver",
                table: "tournament_charges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "referee_waived",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "referee_waived_note",
                table: "events",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reference_category_id",
                table: "events",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "manual_waiver",
                table: "tournament_charges");

            migrationBuilder.DropColumn(
                name: "referee_waived",
                table: "events");

            migrationBuilder.DropColumn(
                name: "referee_waived_note",
                table: "events");

            migrationBuilder.DropColumn(
                name: "reference_category_id",
                table: "events");
        }
    }
}
