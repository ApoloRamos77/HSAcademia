using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7LateFeesRegularization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "late_fee_amount",
                table: "payment_records",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "late_fee_record_id",
                table: "payment_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regularization_note",
                table: "payment_records",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "regularized_at",
                table: "payment_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "regularized_by_name",
                table: "payment_records",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "regularized_by_user_id",
                table: "payment_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "late_fee_amount",
                table: "academy_financial_configs",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "late_fee_amount",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "late_fee_record_id",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "regularization_note",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "regularized_at",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "regularized_by_name",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "regularized_by_user_id",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "late_fee_amount",
                table: "academy_financial_configs");
        }
    }
}
