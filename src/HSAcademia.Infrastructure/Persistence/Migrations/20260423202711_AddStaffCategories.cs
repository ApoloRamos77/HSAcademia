using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryUser",
                columns: table => new
                {
                    AssignedCategoriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMembersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryUser", x => new { x.AssignedCategoriesId, x.StaffMembersId });
                    table.ForeignKey(
                        name: "FK_CategoryUser_categories_AssignedCategoriesId",
                        column: x => x.AssignedCategoriesId,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoryUser_users_StaffMembersId",
                        column: x => x.StaffMembersId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryUser_StaffMembersId",
                table: "CategoryUser",
                column: "StaffMembersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryUser");
        }
    }
}
