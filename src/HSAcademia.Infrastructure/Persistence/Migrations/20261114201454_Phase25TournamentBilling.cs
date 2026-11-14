using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase25TournamentBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tournament_event_id",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "tournament_id",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tournament_fees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tournament_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    frequency = table.Column<int>(type: "integer", nullable: false),
                    amount_mode = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exempt_scholarship = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_fees", x => x.id);
                    table.ForeignKey(
                        name: "FK_tournament_fees_tournament_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "tournament_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tournament_fees_tournaments_tournament_id",
                        column: x => x.tournament_id,
                        principalTable: "tournaments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tournament_charges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    payment_record_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decided_by_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tournament_charges", x => x.id);
                    table.ForeignKey(
                        name: "FK_tournament_charges_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tournament_charges_payment_records_payment_record_id",
                        column: x => x.payment_record_id,
                        principalTable: "payment_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tournament_charges_tournament_fees_fee_id",
                        column: x => x.fee_id,
                        principalTable: "tournament_fees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_tournament_id",
                table: "expenses",
                column: "tournament_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_charges_academy_id_status",
                table: "tournament_charges",
                columns: new[] { "academy_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_charges_match",
                table: "tournament_charges",
                columns: new[] { "fee_id", "event_id", "student_id" },
                unique: true,
                filter: "event_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_charges_once",
                table: "tournament_charges",
                columns: new[] { "fee_id", "student_id" },
                unique: true,
                filter: "event_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_charges_payment_record_id",
                table: "tournament_charges",
                column: "payment_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_charges_student_id",
                table: "tournament_charges",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_fees_academy_id_tournament_id",
                table: "tournament_fees",
                columns: new[] { "academy_id", "tournament_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tournament_fees_team_id",
                table: "tournament_fees",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_tournament_fees_tournament_id",
                table: "tournament_fees",
                column: "tournament_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tournament_charges");

            migrationBuilder.DropTable(
                name: "tournament_fees");

            migrationBuilder.DropIndex(
                name: "IX_expenses_tournament_id",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "tournament_event_id",
                table: "expenses");

            migrationBuilder.DropColumn(
                name: "tournament_id",
                table: "expenses");
        }
    }
}
