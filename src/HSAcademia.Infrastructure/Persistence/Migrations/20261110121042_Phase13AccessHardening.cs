using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase13AccessHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_headquarters",
                columns: table => new
                {
                    headquarter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_headquarters", x => new { x.headquarter_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_user_headquarters_headquarters_headquarter_id",
                        column: x => x.headquarter_id,
                        principalTable: "headquarters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_headquarters_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_headquarters_user_id",
                table: "user_headquarters",
                column: "user_id");

            // Las sedes que ya estaban guardadas pasan a la nueva asignación.
            migrationBuilder.Sql(@"
                INSERT INTO user_headquarters (headquarter_id, user_id)
                SELECT u.""HeadquarterId"", u.id
                FROM users u
                JOIN headquarters h ON h.id = u.""HeadquarterId"" AND NOT h.is_deleted
                WHERE NOT u.is_deleted;");

            // Desde esta versión el servidor exige manage_attendance, que antes solo ocultaba el menú.
            // Quien hoy puede tomar lista (tiene categorías o dicta sesiones) conserva esa capacidad.
            const string takesAttendance = @"
                u.role = 2 AND NOT u.is_deleted AND (
                    EXISTS (SELECT 1 FROM ""CategoryUser"" cu WHERE cu.""StaffMembersId"" = u.id)
                    OR EXISTS (SELECT 1 FROM events e WHERE e.teacher_id = u.id))";

            migrationBuilder.Sql($@"
                UPDATE academy_roles r
                SET permissions = r.permissions || '[""manage_attendance""]'::jsonb, updated_at = NOW()
                WHERE NOT r.permissions @> '[""manage_attendance""]'::jsonb
                  AND EXISTS (SELECT 1 FROM users u WHERE u.""AcademyRoleId"" = r.id AND {takesAttendance});");

            // Personal sin rol: se crea un rol que solo permite la asistencia, sin sumar ningún otro acceso.
            const string migratedRole = "Entrenador (solo asistencia)";
            migrationBuilder.Sql($@"
                INSERT INTO academy_roles (id, academy_id, name, description, permissions, is_active, created_at, updated_at, is_deleted)
                SELECT md5(random()::text || clock_timestamp()::text || x.academy_id::text)::uuid, x.academy_id, '{migratedRole}',
                       'Creado automáticamente para el personal que tomaba asistencia sin rol asignado.',
                       '[""manage_attendance""]'::jsonb, TRUE, NOW(), NOW(), FALSE
                FROM (SELECT DISTINCT u.academy_id FROM users u WHERE u.""AcademyRoleId"" IS NULL AND {takesAttendance}) x;");

            migrationBuilder.Sql($@"
                UPDATE users u
                SET ""AcademyRoleId"" = r.id
                FROM academy_roles r
                WHERE r.academy_id = u.academy_id AND r.name = '{migratedRole}'
                  AND u.""AcademyRoleId"" IS NULL AND {takesAttendance};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_headquarters");
        }
    }
}
