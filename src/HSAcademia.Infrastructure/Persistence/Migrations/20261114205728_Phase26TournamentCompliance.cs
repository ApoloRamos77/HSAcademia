using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase26TournamentCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "authorization_text",
                table: "tournaments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "red_card_suspension_matches",
                table: "tournaments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "required_documents",
                table: "tournaments",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<bool>(
                name: "requires_authorization",
                table: "tournaments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "yellow_cards_for_suspension",
                table: "tournaments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "match_fee",
                table: "tournament_teams",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivered_documents",
                table: "tournament_players",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.CreateTable(
                name: "tournament_authorizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tournament_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_by_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    text_snapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_authorizations", x => x.id);
                    table.ForeignKey(
                        name: "FK_tournament_authorizations_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tournament_authorizations_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tournament_suspensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    overridden = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_suspensions", x => x.id);
                    table.ForeignKey(
                        name: "FK_tournament_suspensions_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tournament_suspensions_tournament_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "tournament_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_authorizations_student_id",
                table: "tournament_authorizations",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_authorizations_tournament_id_student_id",
                table: "tournament_authorizations",
                columns: new[] { "tournament_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_suspensions_event_id_student_id",
                table: "tournament_suspensions",
                columns: new[] { "event_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_suspensions_student_id",
                table: "tournament_suspensions",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_suspensions_team_id",
                table: "tournament_suspensions",
                column: "team_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tournament_authorizations");

            migrationBuilder.DropTable(
                name: "tournament_suspensions");

            migrationBuilder.DropColumn(
                name: "authorization_text",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "red_card_suspension_matches",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "required_documents",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "requires_authorization",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "yellow_cards_for_suspension",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "match_fee",
                table: "tournament_teams");

            migrationBuilder.DropColumn(
                name: "delivered_documents",
                table: "tournament_players");
        }
    }
}
