using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Ingreso con celular: el celular de las cuentas queda solo con dígitos y sin +51, y las cuentas
    /// de alumno toman el celular de su ficha (antes, al editar la ficha, el celular no llegaba a la
    /// cuenta y el alumno no podía ingresar con él). Solo datos.
    /// </summary>
    public partial class Phase33PhoneLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Formato único: "960 867 830", "+51 960867830" → "960867830"; sin dígitos → NULL.
            migrationBuilder.Sql(@"
UPDATE users u
SET phone = x.normalized
FROM (
    SELECT id,
           NULLIF(CASE WHEN length(d) = 11 AND left(d, 2) = '51' THEN right(d, 9) ELSE d END, '') AS normalized
    FROM (SELECT id, regexp_replace(phone, '\D', '', 'g') AS d FROM users WHERE phone IS NOT NULL) t
) x
WHERE u.id = x.id AND u.phone IS DISTINCT FROM x.normalized;");

            // 2) La cuenta del alumno toma el celular de su ficha.
            migrationBuilder.Sql(@"
UPDATE users u
SET phone = x.normalized
FROM (
    SELECT s.""UserId"" AS user_id,
           NULLIF(CASE WHEN length(d) = 11 AND left(d, 2) = '51' THEN right(d, 9) ELSE d END, '') AS normalized
    FROM (SELECT ""UserId"", regexp_replace(""Phone"", '\D', '', 'g') AS d
          FROM ""Students""
          WHERE ""UserId"" IS NOT NULL AND ""Phone"" IS NOT NULL AND NOT ""IsDeleted"") s
) x
WHERE u.id = x.user_id AND x.normalized IS NOT NULL AND u.phone IS DISTINCT FROM x.normalized;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo datos: no hay vuelta atrás (el formato anterior no se conservó).
        }
    }
}
