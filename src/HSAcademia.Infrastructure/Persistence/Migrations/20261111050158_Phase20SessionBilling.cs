using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase20SessionBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingPlan",
                table: "Students",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PreferentialSessionFee",
                table: "Students",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "session_date",
                table: "payment_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "session_event_id",
                table: "payment_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sessions_count",
                table: "payment_records",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "package_price",
                table: "categories",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "package_sessions",
                table: "categories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "session_fee",
                table: "categories",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingPlan",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PreferentialSessionFee",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "session_date",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "session_event_id",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "sessions_count",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "package_price",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "package_sessions",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "session_fee",
                table: "categories");
        }
    }
}
