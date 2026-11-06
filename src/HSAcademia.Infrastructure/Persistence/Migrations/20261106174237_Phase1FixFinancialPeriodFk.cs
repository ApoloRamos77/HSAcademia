using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1FixFinancialPeriodFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialPeriods_academies_AcademyId",
                table: "FinancialPeriods");

            migrationBuilder.DropIndex(
                name: "IX_FinancialPeriods_AcademyId",
                table: "FinancialPeriods");

            // Defensivo: si existiera algún periodo guardado con academies.id, se pasa al tenant (academy_id).
            migrationBuilder.Sql(@"
                UPDATE ""FinancialPeriods"" fp
                SET ""AcademyId"" = a.academy_id
                FROM academies a
                WHERE fp.""AcademyId"" = a.id AND a.id <> a.academy_id;");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPeriods_AcademyId_Year_Month",
                table: "FinancialPeriods",
                columns: new[] { "AcademyId", "Year", "Month" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialPeriods_academies_AcademyId",
                table: "FinancialPeriods",
                column: "AcademyId",
                principalTable: "academies",
                principalColumn: "academy_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialPeriods_academies_AcademyId",
                table: "FinancialPeriods");

            migrationBuilder.DropIndex(
                name: "IX_FinancialPeriods_AcademyId_Year_Month",
                table: "FinancialPeriods");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPeriods_AcademyId",
                table: "FinancialPeriods",
                column: "AcademyId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialPeriods_academies_AcademyId",
                table: "FinancialPeriods",
                column: "AcademyId",
                principalTable: "academies",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
