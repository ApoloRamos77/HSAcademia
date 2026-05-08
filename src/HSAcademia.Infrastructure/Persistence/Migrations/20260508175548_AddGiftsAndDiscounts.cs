using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftsAndDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "product_sales",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsGift",
                table: "product_sales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "payment_records",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "product_sales");

            migrationBuilder.DropColumn(
                name: "IsGift",
                table: "product_sales");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "payment_records");
        }
    }
}
