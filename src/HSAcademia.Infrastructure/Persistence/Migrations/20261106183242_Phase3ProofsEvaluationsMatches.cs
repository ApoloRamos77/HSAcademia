using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ProofsEvaluationsMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "goals_against",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "goals_for",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "match_notes",
                table: "events",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "series_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "match_player_stats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_starter = table.Column<bool>(type: "boolean", nullable: false),
                    minutes = table.Column<int>(type: "integer", nullable: false),
                    goals = table.Column<int>(type: "integer", nullable: false),
                    assists = table.Column<int>(type: "integer", nullable: false),
                    yellow_cards = table.Column<int>(type: "integer", nullable: false),
                    red_cards = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_player_stats", x => x.id);
                    table.ForeignKey(
                        name: "FK_match_player_stats_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_match_player_stats_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_match_player_stats_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_proofs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    operation_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    voucher_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    paid_on = table.Column<DateTime>(type: "date", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receipt_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_proofs", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_proofs_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_proofs_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_proofs_payment_records_payment_record_id",
                        column: x => x.payment_record_id,
                        principalTable: "payment_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_proofs_users_submitted_by_user_id",
                        column: x => x.submitted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    evaluation_date = table.Column<DateTime>(type: "date", nullable: false),
                    technical = table.Column<int>(type: "integer", nullable: false),
                    tactical = table.Column<int>(type: "integer", nullable: false),
                    physical = table.Column<int>(type: "integer", nullable: false),
                    attitude = table.Column<int>(type: "integer", nullable: false),
                    strengths = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    to_improve = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    goals = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_visible_to_family = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_evaluations_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_evaluations_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_evaluations_users_evaluator_user_id",
                        column: x => x.evaluator_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_series_id",
                table: "events",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "IX_match_player_stats_academy_id_student_id",
                table: "match_player_stats",
                columns: new[] { "academy_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "IX_match_player_stats_event_id_student_id",
                table: "match_player_stats",
                columns: new[] { "event_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_player_stats_student_id",
                table: "match_player_stats",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_proofs_academy_id_status",
                table: "payment_proofs",
                columns: new[] { "academy_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_proofs_payment_record_id",
                table: "payment_proofs",
                column: "payment_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_proofs_student_id",
                table: "payment_proofs",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_proofs_submitted_by_user_id",
                table: "payment_proofs",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_evaluations_academy_id_student_id",
                table: "student_evaluations",
                columns: new[] { "academy_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "IX_student_evaluations_evaluator_user_id",
                table: "student_evaluations",
                column: "evaluator_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_evaluations_student_id",
                table: "student_evaluations",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "match_player_stats");

            migrationBuilder.DropTable(
                name: "payment_proofs");

            migrationBuilder.DropTable(
                name: "student_evaluations");

            migrationBuilder.DropIndex(
                name: "IX_events_series_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "goals_against",
                table: "events");

            migrationBuilder.DropColumn(
                name: "goals_for",
                table: "events");

            migrationBuilder.DropColumn(
                name: "match_notes",
                table: "events");

            migrationBuilder.DropColumn(
                name: "series_id",
                table: "events");
        }
    }
}
