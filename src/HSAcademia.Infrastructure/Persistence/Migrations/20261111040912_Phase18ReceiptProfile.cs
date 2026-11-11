using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18ReceiptProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "receipt_address",
                table: "academies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "receipt_phone",
                table: "academies",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Se parte de la dirección propia de la academia, nunca de la de una sede.
            migrationBuilder.Sql(@"
                UPDATE academies SET receipt_address = left(btrim(address), 200)
                WHERE receipt_address IS NULL AND btrim(coalesce(address, '')) <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receipt_address",
                table: "academies");

            migrationBuilder.DropColumn(
                name: "receipt_phone",
                table: "academies");
        }
    }
}
