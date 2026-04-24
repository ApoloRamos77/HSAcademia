using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsScholarship",
                table: "Students",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentStartDate",
                table: "Students",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PreferentialFee",
                table: "Students",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyFee",
                table: "categories",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "academy_financial_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_payment_day = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_financial_configs", x => x.id);
                    table.ForeignKey(
                        name: "FK_academy_financial_configs_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_paid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    paid_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    product_sale_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_records_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_records_academies_academy_id",
                        column: x => x.academy_id,
                        principalTable: "academies",
                        principalColumn: "academy_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_records_product_sales_product_sale_id",
                        column: x => x.product_sale_id,
                        principalTable: "product_sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_academy_financial_configs_academy_id",
                table: "academy_financial_configs",
                column: "academy_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_academy_id",
                table: "payment_records",
                column: "academy_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_product_sale_id",
                table: "payment_records",
                column: "product_sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_student_id",
                table: "payment_records",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academy_financial_configs");

            migrationBuilder.DropTable(
                name: "payment_records");

            migrationBuilder.DropColumn(
                name: "IsGuest",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsScholarship",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PaymentStartDate",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PreferentialFee",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MonthlyFee",
                table: "categories");
        }
    }
}
