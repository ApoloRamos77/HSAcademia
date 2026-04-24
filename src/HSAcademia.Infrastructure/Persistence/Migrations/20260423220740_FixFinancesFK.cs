using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixFinancesFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_academy_financial_configs_academies_academy_id",
                table: "academy_financial_configs");

            migrationBuilder.AddForeignKey(
                name: "FK_academy_financial_configs_academies_academy_id",
                table: "academy_financial_configs",
                column: "academy_id",
                principalTable: "academies",
                principalColumn: "academy_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_academy_financial_configs_academies_academy_id",
                table: "academy_financial_configs");

            migrationBuilder.AddForeignKey(
                name: "FK_academy_financial_configs_academies_academy_id",
                table: "academy_financial_configs",
                column: "academy_id",
                principalTable: "academies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
