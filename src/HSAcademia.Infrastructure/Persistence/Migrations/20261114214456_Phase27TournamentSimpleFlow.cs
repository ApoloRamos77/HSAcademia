using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase27TournamentSimpleFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "default_coach_fee",
                table: "tournaments",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "inscription_fee",
                table: "tournaments",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "level",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "modality",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "referee_fee",
                table: "tournaments",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "auto_roster",
                table: "tournament_teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "system_key",
                table: "tournament_fees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "coach_fee",
                table: "events",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "time_pending",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_coach_fee",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "inscription_fee",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "level",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "modality",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "referee_fee",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "auto_roster",
                table: "tournament_teams");

            migrationBuilder.DropColumn(
                name: "system_key",
                table: "tournament_fees");

            migrationBuilder.DropColumn(
                name: "coach_fee",
                table: "events");

            migrationBuilder.DropColumn(
                name: "time_pending",
                table: "events");
        }
    }
}
