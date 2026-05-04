using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeCategoryIdsNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<Guid>>(
                name: "category_ids",
                table: "events",
                type: "jsonb",
                nullable: true,
                defaultValueSql: "'[]'::jsonb",
                oldClrType: typeof(List<Guid>),
                oldType: "jsonb",
                oldDefaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<List<Guid>>(
                name: "category_ids",
                table: "events",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb",
                oldClrType: typeof(List<Guid>),
                oldType: "jsonb",
                oldNullable: true,
                oldDefaultValueSql: "'[]'::jsonb");
        }
    }
}
