using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase32TournamentChargeAll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Torneos: se cobra a todos y la exoneración de becados se decide al cobrar (decisión del usuario, 2026-11-20).
            // Lo que ya quedó exonerado por beca se conserva como exonerado (decisión manual) para no crear deudas
            // sorpresa de encuentros ya jugados; desde ahora ningún concepto exonera solo.
            migrationBuilder.Sql(@"UPDATE tournament_charges
                SET status = 3, manual_waiver = TRUE, note = 'Exonerado (becado o invitado)', decided_by_name = 'Clubio',
                    decided_at = COALESCE(decided_at, NOW()), updated_at = NOW()
                WHERE status = 4;");
            migrationBuilder.Sql(@"UPDATE tournament_fees SET exempt_scholarship = FALSE, updated_at = NOW() WHERE exempt_scholarship = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo datos: no se revierte (no se sabe qué conceptos tenían la exoneración automática).

        }
    }
}
