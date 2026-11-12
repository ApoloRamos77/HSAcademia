using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase23SessionCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "session_credit_amount",
                table: "payment_records",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "session_credit_applied",
                table: "payment_records",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "session_credit_used_by_id",
                table: "payment_records",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "session_credit_amount",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "session_credit_applied",
                table: "payment_records");

            migrationBuilder.DropColumn(
                name: "session_credit_used_by_id",
                table: "payment_records");
        }
    }
}
