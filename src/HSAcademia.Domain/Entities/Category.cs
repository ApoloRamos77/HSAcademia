using System;

namespace HSAcademia.Domain.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AcademyId { get; set; } // Tenant Discriminator
    public Guid HeadquarterId { get; set; } // Which headquarter it belongs to

    public string Name { get; set; } = string.Empty; // e.g. "Sub-12"
    // Por edad: franja de nacimiento. Libre: sin franja (null) y, si se quiere, una edad mínima.
    public DateTime? StartDateOfBirth { get; set; }
    public DateTime? EndDateOfBirth { get; set; }
    public bool IsFree { get; set; }
    public int? MinAge { get; set; }

    /// <summary>Apps instaladas que aún no conocen IsFree reciben esta franja amplia en vez de null.</summary>
    public static readonly DateTime FreeRangeStart = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime FreeRangeEnd = new(2100, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>True si la fecha de nacimiento corresponde a la categoría (sin fecha no se puede juzgar).</summary>
    public bool FitsBirthDate(DateTime? birthDate, DateTime today)
    {
        if (!birthDate.HasValue) return true;
        var dob = birthDate.Value.Date;
        if (IsFree)
        {
            if (!MinAge.HasValue) return true;
            var age = today.Year - dob.Year - (dob > today.AddYears(-(today.Year - dob.Year)) ? 1 : 0);
            return age >= MinAge.Value;
        }
        if (!StartDateOfBirth.HasValue || !EndDateOfBirth.HasValue) return true;
        var a = StartDateOfBirth.Value.Date;
        var b = EndDateOfBirth.Value.Date;
        return dob >= (a <= b ? a : b) && dob <= (a <= b ? b : a);
    }
    public decimal MonthlyFee { get; set; } = 0m;
    /// <summary>Precio por sesión para alumnos que pagan por sesión (0 = no ofrece).</summary>
    public decimal SessionFee { get; set; } = 0m;
    /// <summary>Paquete mensual: N sesiones por un precio (ambos o ninguno).</summary>
    public int? PackageSessions { get; set; }
    public decimal? PackagePrice { get; set; }
    public bool IsActive { get; set; } = true;

    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public virtual Academy? Academy { get; set; }
    public virtual Headquarter? Headquarter { get; set; }

    public virtual ICollection<User> StaffMembers { get; set; } = new List<User>();
}
