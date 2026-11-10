using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase14AccessModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_owner",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "system_key",
                table: "academy_roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_academy_roles",
                columns: table => new
                {
                    academy_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_academy_roles", x => new { x.academy_role_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_user_academy_roles_academy_roles_academy_role_id",
                        column: x => x.academy_role_id,
                        principalTable: "academy_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_academy_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Cada persona conserva su rol actual, ahora en la asignación que admite varios.
            migrationBuilder.Sql(@"
                INSERT INTO user_academy_roles (academy_role_id, user_id)
                SELECT u.""AcademyRoleId"", u.id
                FROM users u
                JOIN academy_roles r ON r.id = u.""AcademyRoleId""
                WHERE NOT u.is_deleted;");

            migrationBuilder.DropForeignKey(
                name: "FK_users_academy_roles_AcademyRoleId",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_AcademyRoleId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AcademyRoleId",
                table: "users");

            // Cada permiso antiguo se traduce a las acciones que realmente permitía, para que nadie gane
            // ni pierda acceso. Publicar comunicados no estaba restringido: lo conservan todos los roles.
            migrationBuilder.Sql(@"
                UPDATE academy_roles r
                SET updated_at = NOW(),
                    permissions = (
                        SELECT jsonb_agg(DISTINCT x.p)
                        FROM (
                            SELECT 'announcements.publish' AS p
                            UNION ALL
                            SELECT m.current_name
                            FROM jsonb_array_elements_text(r.permissions) old_name
                            JOIN (VALUES
                        ('view_students','students.view'),
                        ('view_students','students.edit'),
                        ('view_students','students.medical'),
                        ('view_students','students.evaluate'),
                        ('view_students','students.payments'),
                        ('view_students','reports.students'),
                        ('view_students','reports.attendance'),
                        ('manage_attendance','attendance.take'),
                        ('manage_attendance','attendance.metrics'),
                        ('manage_attendance','reports.attendance'),
                        ('manage_staff_attendance','staff_attendance.mark'),
                        ('manage_events','events.manage'),
                        ('manage_store','store.sell'),
                        ('manage_store','store.inventory'),
                        ('manage_finances','students.billing'),
                        ('manage_finances','students.payments'),
                        ('manage_finances','collections.view'),
                        ('manage_finances','collections.collect'),
                        ('manage_finances','collections.adjust'),
                        ('manage_finances','collections.approve_proofs'),
                        ('manage_finances','collections.void'),
                        ('manage_finances','collections.generate'),
                        ('manage_finances','cash.receive'),
                        ('manage_finances','finances.config'),
                        ('manage_finances','finances.expenses'),
                        ('manage_finances','finances.staff_payments'),
                        ('manage_finances','finances.summary'),
                        ('manage_finances','reports.finance')
                            ) AS m(legacy_name, current_name) ON m.legacy_name = old_name.value
                        ) x);");

            // El propietario es el administrador más antiguo; se prefiere uno activo.
            migrationBuilder.Sql(@"
                UPDATE users SET is_owner = TRUE
                WHERE id IN (
                    SELECT DISTINCT ON (academy_id) id
                    FROM users
                    WHERE role = 1 AND NOT is_deleted AND academy_id IS NOT NULL
                    ORDER BY academy_id, (status = 1) DESC, created_at);");

            migrationBuilder.CreateIndex(
                name: "IX_users_single_owner",
                table: "users",
                column: "academy_id",
                unique: true,
                filter: "is_owner AND NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "IX_academy_roles_system_key",
                table: "academy_roles",
                columns: new[] { "academy_id", "system_key" },
                unique: true,
                filter: "system_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_user_academy_roles_user_id",
                table: "user_academy_roles",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_academy_roles");

            migrationBuilder.DropIndex(
                name: "IX_users_single_owner",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_academy_roles_system_key",
                table: "academy_roles");

            migrationBuilder.DropColumn(
                name: "is_owner",
                table: "users");

            migrationBuilder.DropColumn(
                name: "system_key",
                table: "academy_roles");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademyRoleId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "AcademyRoleId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_users_AcademyRoleId",
                table: "users",
                column: "AcademyRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_users_academy_roles_AcademyRoleId",
                table: "users",
                column: "AcademyRoleId",
                principalTable: "academy_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
