using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancesPremiumModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    date = table.Column<DateTime>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    voucher_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    registered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.id);
                    table.ForeignKey(
                        name: "FK_expenses_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expenses_users_registered_by",
                        column: x => x.registered_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "financial_goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headquarter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    month = table.Column<int>(type: "integer", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    target_income = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    target_profit = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_goals", x => x.id);
                    table.ForeignKey(
                        name: "FK_financial_goals_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_financial_goals_headquarters_headquarter_id",
                        column: x => x.headquarter_id,
                        principalTable: "headquarters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "monthly_closings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    total_income = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    total_expenses = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    net_profit = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    petty_cash_balance = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monthly_closings", x => x.id);
                    table.ForeignKey(
                        name: "FK_monthly_closings_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monthly_closings_users_closed_by",
                        column: x => x.closed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "petty_cash",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headquarter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    month = table.Column<int>(type: "integer", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    assigned_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    current_balance = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash", x => x.id);
                    table.ForeignKey(
                        name: "FK_petty_cash_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_petty_cash_headquarters_headquarter_id",
                        column: x => x.headquarter_id,
                        principalTable: "headquarters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "staff_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_month = table.Column<int>(type: "integer", nullable: false),
                    period_year = table.Column<int>(type: "integer", nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    bonuses = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    deductions = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    total_paid = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_staff_payments_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_staff_payments_users_staff_id",
                        column: x => x.staff_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "petty_cash_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    petty_cash_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    concept = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    registered_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_petty_cash_transactions_petty_cash_petty_cash_id",
                        column: x => x.petty_cash_id,
                        principalTable: "petty_cash",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_petty_cash_transactions_users_registered_by",
                        column: x => x.registered_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_academy_id",
                table: "expenses",
                column: "academy_id");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_date",
                table: "expenses",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_registered_by",
                table: "expenses",
                column: "registered_by");

            migrationBuilder.CreateIndex(
                name: "IX_financial_goals_academy_id_headquarter_id_month_year",
                table: "financial_goals",
                columns: new[] { "academy_id", "headquarter_id", "month", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_financial_goals_headquarter_id",
                table: "financial_goals",
                column: "headquarter_id");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_closings_academy_id_month_year",
                table: "monthly_closings",
                columns: new[] { "academy_id", "month", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_closings_closed_by",
                table: "monthly_closings",
                column: "closed_by");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_academy_id_headquarter_id_month_year",
                table: "petty_cash",
                columns: new[] { "academy_id", "headquarter_id", "month", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_headquarter_id",
                table: "petty_cash",
                column: "headquarter_id");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_transactions_petty_cash_id",
                table: "petty_cash_transactions",
                column: "petty_cash_id");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_transactions_registered_by",
                table: "petty_cash_transactions",
                column: "registered_by");

            migrationBuilder.CreateIndex(
                name: "IX_staff_payments_academy_id_staff_id_period_month_period_year",
                table: "staff_payments",
                columns: new[] { "academy_id", "staff_id", "period_month", "period_year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_payments_staff_id",
                table: "staff_payments",
                column: "staff_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "financial_goals");

            migrationBuilder.DropTable(
                name: "monthly_closings");

            migrationBuilder.DropTable(
                name: "petty_cash_transactions");

            migrationBuilder.DropTable(
                name: "staff_payments");

            migrationBuilder.DropTable(
                name: "petty_cash");
        }
    }
}
