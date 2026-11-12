using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase22Referrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "referral_discount",
                table: "payment_records",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "referral_campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    valid_from = table.Column<DateTime>(type: "date", nullable: false),
                    valid_to = table.Column<DateTime>(type: "date", nullable: false),
                    referrer_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    referred_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_campaigns", x => x.id);
                    table.ForeignKey(
                        name: "FK_referral_campaigns_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "referrals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referrer_student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referred_student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referrer_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    referred_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    earned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    annul_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    annulled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referrals", x => x.id);
                    table.ForeignKey(
                        name: "FK_referrals_Students_referred_student_id",
                        column: x => x.referred_student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_referrals_Students_referrer_student_id",
                        column: x => x.referrer_student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_referrals_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_referrals_referral_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "referral_campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "referral_benefits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referral_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<int>(type: "integer", nullable: false),
                    payment_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reverted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revert_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referral_benefits", x => x.id);
                    table.ForeignKey(
                        name: "FK_referral_benefits_referrals_referral_id",
                        column: x => x.referral_id,
                        principalTable: "referrals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_referral_benefits_payment_record_id",
                table: "referral_benefits",
                column: "payment_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_referral_benefits_referral_id",
                table: "referral_benefits",
                column: "referral_id");

            migrationBuilder.CreateIndex(
                name: "IX_referral_campaigns_academy_id",
                table: "referral_campaigns",
                column: "academy_id");

            migrationBuilder.CreateIndex(
                name: "IX_referrals_academy_id_status",
                table: "referrals",
                columns: new[] { "academy_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_referrals_campaign_id",
                table: "referrals",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "IX_referrals_referred_student_id",
                table: "referrals",
                column: "referred_student_id",
                unique: true,
                filter: "status <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_referrals_referrer_student_id",
                table: "referrals",
                column: "referrer_student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referral_benefits");

            migrationBuilder.DropTable(
                name: "referrals");

            migrationBuilder.DropTable(
                name: "referral_campaigns");

            migrationBuilder.DropColumn(
                name: "referral_discount",
                table: "payment_records");
        }
    }
}
