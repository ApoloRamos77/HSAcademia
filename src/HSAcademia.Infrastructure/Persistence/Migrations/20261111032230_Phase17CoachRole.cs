using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Un solo rol "Entrenador": el personal de los roles propios llamados "Entrenador" o
    /// "Entrenador (solo asistencia)" pasa al predefinido, y esos roles propios se eliminan.
    /// </summary>
    public partial class Phase17CoachRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string coachPermissions = @"'[""students.view"",""students.medical"",""students.evaluate"",""attendance.take"",""attendance.metrics"",""events.manage"",""announcements.publish"",""reports.attendance""]'::jsonb";
            const string coachDescription = "Toma lista, evalúa y ve la ficha médica de sus categorías; gestiona la agenda y publica comunicados. No ve pagos ni edita datos del alumno.";
            const string duplicates = @"r.system_key IS NULL AND NOT r.is_deleted
                AND lower(btrim(r.name)) IN ('entrenador', 'entrenador (solo asistencia)')";

            // Academias con un rol duplicado que aún no tienen el predefinido.
            migrationBuilder.Sql($@"
                INSERT INTO academy_roles (id, academy_id, name, description, permissions, is_active, created_at, updated_at, is_deleted, system_key)
                SELECT md5(random()::text || clock_timestamp()::text || x.academy_id::text)::uuid, x.academy_id, 'Entrenador',
                       '{coachDescription}', {coachPermissions}, TRUE, NOW(), NOW(), FALSE, 'coach'
                FROM (SELECT DISTINCT r.academy_id FROM academy_roles r WHERE {duplicates}) x
                WHERE NOT EXISTS (SELECT 1 FROM academy_roles c WHERE c.academy_id = x.academy_id AND c.system_key = 'coach');");

            migrationBuilder.Sql($@"
                UPDATE academy_roles
                SET name = 'Entrenador', description = '{coachDescription}', permissions = {coachPermissions}, is_active = TRUE, is_deleted = FALSE, deleted_at = NULL, updated_at = NOW()
                WHERE system_key = 'coach';");

            migrationBuilder.Sql($@"
                INSERT INTO user_academy_roles (academy_role_id, user_id)
                SELECT DISTINCT c.id, ur.user_id
                FROM user_academy_roles ur
                JOIN academy_roles r ON r.id = ur.academy_role_id
                JOIN academy_roles c ON c.academy_id = r.academy_id AND c.system_key = 'coach'
                WHERE {duplicates}
                ON CONFLICT DO NOTHING;");

            migrationBuilder.Sql($@"
                DELETE FROM user_academy_roles ur
                USING academy_roles r
                WHERE r.id = ur.academy_role_id AND {duplicates};");

            migrationBuilder.Sql($@"
                UPDATE academy_roles r
                SET is_deleted = TRUE, deleted_at = NOW(), updated_at = NOW()
                WHERE {duplicates};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
