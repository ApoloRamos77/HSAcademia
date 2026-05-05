using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixReceiptNumberInitialValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set CurrentReceiptNumber = 43 for existing configs so the FIRST receipt is 000044.
            migrationBuilder.Sql(
                "UPDATE academy_financial_configs SET \"CurrentReceiptNumber\" = 43 WHERE \"CurrentReceiptNumber\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE academy_financial_configs SET \"CurrentReceiptNumber\" = 0 WHERE \"CurrentReceiptNumber\" = 43;");
        }
    }
}
