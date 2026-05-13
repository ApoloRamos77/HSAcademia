using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductSaleForReceiptAndDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "sale_date",
                table: "product_sales",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "receipt_number",
                table: "product_sales",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receipt_number",
                table: "product_sales");

            migrationBuilder.AlterColumn<DateTime>(
                name: "sale_date",
                table: "product_sales",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
