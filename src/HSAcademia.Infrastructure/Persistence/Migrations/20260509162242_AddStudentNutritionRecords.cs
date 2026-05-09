using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentNutritionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_nutrition_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    bmi = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    muscle_mass_percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    fat_percentage = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    record_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    registered_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_nutrition_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_nutrition_records_Students_student_id",
                        column: x => x.student_id,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_student_nutrition_records_users_registered_by_id",
                        column: x => x.registered_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_nutrition_records_registered_by_id",
                table: "student_nutrition_records",
                column: "registered_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_student_nutrition_records_student_id",
                table: "student_nutrition_records",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_nutrition_records");
        }
    }
}
