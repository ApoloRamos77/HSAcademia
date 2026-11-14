using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase24Tournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contact_name",
                table: "tournaments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "tournaments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "end_date",
                table: "tournaments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "format",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "play_days",
                table: "tournaments",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "rules",
                table: "tournaments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "schedule_mode",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "start_date",
                table: "tournaments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "tournaments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "call_time",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "location",
                table: "events",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "match_status",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "round",
                table: "events",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tournament_team_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tournament_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tournament_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category_ids = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    head_coach_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assistant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    birth_year_from = table.Column<int>(type: "integer", nullable: true),
                    birth_year_to = table.Column<int>(type: "integer", nullable: true),
                    max_players = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_tournament_teams_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tournament_teams_users_assistant_id",
                        column: x => x.assistant_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tournament_teams_users_head_coach_id",
                        column: x => x.head_coach_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tournament_players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shirt_number = table.Column<int>(type: "integer", nullable: true),
                    position = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_players", x => x.id);
                    table.ForeignKey(
                        name: "FK_tournament_players_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tournament_players_tournament_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "tournament_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_tournament_team_id",
                table: "events",
                column: "tournament_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_players_academy_id_student_id",
                table: "tournament_players",
                columns: new[] { "academy_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_players_student_id",
                table: "tournament_players",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_players_team_id_student_id",
                table: "tournament_players",
                columns: new[] { "team_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_academy_id_tournament_id",
                table: "tournament_teams",
                columns: new[] { "academy_id", "tournament_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_assistant_id",
                table: "tournament_teams",
                column: "assistant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_head_coach_id",
                table: "tournament_teams",
                column: "head_coach_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_teams_tournament_id",
                table: "tournament_teams",
                column: "tournament_id");

            migrationBuilder.AddForeignKey(
                name: "FK_events_tournament_teams_tournament_team_id",
                table: "events",
                column: "tournament_team_id",
                principalTable: "tournament_teams",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Torneos existentes: fechas tomadas de sus partidos; estado según si ya terminaron.
            migrationBuilder.Sql(@"
UPDATE tournaments t SET
    start_date = r.min_start::date,
    end_date   = r.max_start::date,
    schedule_mode = CASE WHEN r.min_start::date = r.max_start::date THEN 1 ELSE 2 END,
    status = CASE WHEN r.max_start::date < (NOW() AT TIME ZONE 'America/Lima')::date THEN 3 ELSE 2 END
FROM (
    SELECT tournament_id, MIN(start_time) AS min_start, MAX(start_time) AS max_start
    FROM events WHERE tournament_id IS NOT NULL AND is_deleted = false
    GROUP BY tournament_id
) r
WHERE r.tournament_id = t.id;
UPDATE tournaments SET schedule_mode = 3, status = 2 WHERE start_date IS NULL;
UPDATE events SET match_status = CASE WHEN goals_for IS NOT NULL THEN 2 ELSE 1 END
WHERE type IN (2, 3) AND match_status IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_tournament_teams_tournament_team_id",
                table: "events");

            migrationBuilder.DropTable(
                name: "tournament_players");

            migrationBuilder.DropTable(
                name: "tournament_teams");

            migrationBuilder.DropIndex(
                name: "IX_events_tournament_team_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "contact_name",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "end_date",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "format",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "play_days",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "rules",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "schedule_mode",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "start_date",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "status",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "call_time",
                table: "events");

            migrationBuilder.DropColumn(
                name: "location",
                table: "events");

            migrationBuilder.DropColumn(
                name: "match_status",
                table: "events");

            migrationBuilder.DropColumn(
                name: "round",
                table: "events");

            migrationBuilder.DropColumn(
                name: "tournament_team_id",
                table: "events");
        }
    }
}
