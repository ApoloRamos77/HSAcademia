using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase30SameDayEncounterDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "referee_same_day_discount_percent",
                table: "tournaments",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "same_day_discount_percent",
                table: "tournament_fees",
                type: "numeric(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "referee_same_day_discount_percent",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "same_day_discount_percent",
                table: "tournament_fees");
        }
    }
}
