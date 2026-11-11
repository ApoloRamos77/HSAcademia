using System.Globalization;
using System.Text;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Reportes exportables (CSV compatible con Excel en español: separador ';', UTF-8 con BOM).
/// </summary>
public class ReportsService
{
    private static readonly CultureInfo Es = new("es-PE");
    private readonly AppDbContext _db;

    private readonly AccessScopeService _scope;

    public ReportsService(AppDbContext db, AccessScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    // ── Morosidad con antigüedad de deuda ──────────────────────────
    public async Task<byte[]> OverdueAgingCsvAsync(Guid academyId)
    {
        var today = AcademyClock.Today;
        var todayUtc = DateTime.SpecifyKind(today, DateTimeKind.Utc);
        var headquarters = await _scope.StaffHeadquartersAsync();
        var rows = await _db.PaymentRecords.AsNoTracking()
            .Where(p => p.AcademyId == academyId && !p.IsPaid && p.Type != PaymentType.ProductSale && p.DueDate < todayUtc
                && (headquarters == null || headquarters.Contains(p.Student.HeadquarterId)))
            .Select(p => new
            {
                p.StudentId,
                Student = p.Student.LastName + ", " + p.Student.FirstName,
                p.Student.DocumentNumber,
                Category = p.Student.Category.Name,
                Headquarter = p.Student.Headquarter.Name,
                Guardian = p.Student.Guardian != null ? p.Student.Guardian.FirstName + " " + p.Student.Guardian.LastName : null,
                GuardianPhone = p.Student.Guardian != null ? p.Student.Guardian.Phone : p.Student.Phone,
                p.Student.IsActive,
                Pending = p.Amount - p.AmountPaid,
                p.DueDate
            })
            .ToListAsync();

        var csv = new Csv("Alumno", "DNI", "Categoría", "Sede", "Apoderado", "Teléfono", "Estado",
            "0-30 días", "31-60 días", "61-90 días", "Más de 90 días", "Total vencido", "Cuotas vencidas", "Atraso máximo (días)");

        foreach (var g in rows.GroupBy(r => r.StudentId).OrderByDescending(g => g.Sum(r => r.Pending)))
        {
            decimal B(int min, int max) => g.Where(r => { var d = (today - r.DueDate.Date).Days; return d >= min && d <= max; }).Sum(r => r.Pending);
            var f = g.First();
            csv.Row(f.Student, f.DocumentNumber, f.Category, f.Headquarter, f.Guardian, f.GuardianPhone,
                f.IsActive ? "Activo" : "Retirado",
                B(0, 30), B(31, 60), B(61, 90), B(91, int.MaxValue),
                g.Sum(r => r.Pending), g.Count(), g.Max(r => (today - r.DueDate.Date).Days));
        }
        return csv.ToBytes();
    }

    // ── Padrón de alumnos ──────────────────────────────────────────
    public async Task<byte[]> StudentsCsvAsync(Guid academyId, bool includeInactive)
    {
        var today = AcademyClock.Today;
        var q = _db.Students.AsNoTracking().Where(s => s.AcademyId == academyId);
        if (!includeInactive) q = q.Where(s => s.IsActive);
        q = AccessScopeService.ApplyStudents(q, await _scope.CurrentAsync());
        var rows = await q
            .OrderBy(s => s.Headquarter.Name).ThenBy(s => s.Category.Name).ThenBy(s => s.LastName)
            .Select(s => new
            {
                s.LastName, s.FirstName, s.DocumentNumber, s.DateOfBirth, Category = s.Category.Name,
                Category_Fee = s.Category.MonthlyFee, Headquarter = s.Headquarter.Name, s.Phone, s.Email,
                Guardian = s.Guardian != null ? s.Guardian.FirstName + " " + s.Guardian.LastName : null,
                GuardianPhone = s.Guardian != null ? s.Guardian.Phone : null,
                GuardianEmail = s.Guardian != null ? s.Guardian.Email : null,
                s.EnrollmentDate, s.IsActive, s.WithdrawalDate, s.IsScholarship, s.ScholarshipPercentage, s.IsGuest, s.PreferentialFee,
                EmergencyName = s.MedicalRecord != null ? s.MedicalRecord.EmergencyContactName : null,
                EmergencyPhone = s.MedicalRecord != null ? s.MedicalRecord.EmergencyContactPhone : null,
                Allergies = s.MedicalRecord != null ? s.MedicalRecord.Allergies : null
            })
            .ToListAsync();

        var csv = new Csv("Apellidos", "Nombres", "DNI", "Fecha de nacimiento", "Edad", "Sede", "Categoría", "Celular", "Correo",
            "Apoderado", "Teléfono apoderado", "Correo apoderado", "Fecha de inscripción", "Estado", "Fecha de retiro",
            "Beneficio", "Mensualidad", "Contacto de emergencia", "Teléfono de emergencia", "Alergias");

        foreach (var s in rows)
        {
            int? age = null;
            if (s.DateOfBirth.HasValue)
            {
                var a = today.Year - s.DateOfBirth.Value.Year;
                if (s.DateOfBirth.Value.Date > today.AddYears(-a)) a--;
                age = a;
            }
            var benefit = s.IsGuest ? "Invitado" : s.IsScholarship ? "Beca completa"
                : (s.ScholarshipPercentage ?? 0) > 0 ? $"Beca {s.ScholarshipPercentage:0.#}%" : "";
            var fee = s.IsGuest || s.IsScholarship ? 0m
                : (s.PreferentialFee ?? s.Category_Fee) * (1 - (s.ScholarshipPercentage ?? 0) / 100m);
            csv.Row(s.LastName, s.FirstName, s.DocumentNumber, s.DateOfBirth, age, s.Headquarter, s.Category, s.Phone, s.Email,
                s.Guardian, s.GuardianPhone, s.GuardianEmail, s.EnrollmentDate, s.IsActive ? "Activo" : "Retirado", s.WithdrawalDate,
                benefit, Math.Round(fee, 2), s.EmergencyName, s.EmergencyPhone, s.Allergies);
        }
        return csv.ToBytes();
    }

    // ── Cobros del mes (libro de caja de mensualidades) ────────────
    public async Task<byte[]> PaymentsCsvAsync(Guid academyId, int year, int month)
    {
        var (startUtc, endUtc) = AcademyClock.MonthRangeUtc(year, month);
        var headquarters = await _scope.StaffHeadquartersAsync();
        var rows = await _db.PaymentInstallments.AsNoTracking()
            .Where(i => i.PaymentRecord.AcademyId == academyId && !i.PaymentRecord.IsDeleted && i.PaidAt >= startUtc && i.PaidAt < endUtc
                && (headquarters == null || headquarters.Contains(i.PaymentRecord.Student.HeadquarterId)))
            .OrderBy(i => i.PaidAt)
            .Select(i => new
            {
                i.PaidAt, i.ReceiptNumber, Student = i.PaymentRecord.Student.LastName + ", " + i.PaymentRecord.Student.FirstName,
                i.PaymentRecord.Student.DocumentNumber, Concept = i.PaymentRecord.Description, i.Method, i.OperationNumber, i.AmountPaid, i.Notes
            })
            .ToListAsync();

        var sales = await _db.ProductSales.AsNoTracking()
            .Where(s => s.AcademyId == academyId && s.SaleDate >= startUtc && s.SaleDate < endUtc && !s.IsGift
                && (headquarters == null || s.Student == null || headquarters.Contains(s.Student.HeadquarterId)))
            .OrderBy(s => s.SaleDate)
            .Select(s => new
            {
                s.SaleDate, s.ReceiptNumber, Student = s.Student != null ? s.Student.LastName + ", " + s.Student.FirstName : "Público general",
                Concept = s.Product.Name + " x" + s.Quantity, s.TotalPrice, s.Notes
            })
            .ToListAsync();

        // Un abono de recibo mixto no se puede atribuir a un solo medio: se muestra el reparto del recibo.
        var mixedNumbers = rows.Where(r => r.Method == PaymentMethod.Mixed && r.ReceiptNumber != null)
            .Select(r => r.ReceiptNumber!).Distinct().ToList();
        var mixedReceipts = await _db.Receipts.AsNoTracking().Include(r => r.Payments)
            .Where(r => r.AcademyId == academyId && mixedNumbers.Contains(r.Number))
            .ToListAsync();
        var mixedDetail = mixedReceipts.ToDictionary(
            r => r.Number,
            r => "Mixto (" + string.Join(" + ", r.PaymentParts().Select(p => $"{MethodName(p.Method)} {p.Amount.ToString("0.00", Es)}")) + ")");

        var csv = new Csv("Fecha", "Recibo", "Alumno", "DNI", "Concepto", "Tipo", "Medio de pago", "N° operación", "Monto", "Notas");
        foreach (var r in rows)
            csv.Row(AcademyClock.ToLocalDate(r.PaidAt), r.ReceiptNumber, r.Student, r.DocumentNumber, r.Concept, "Mensualidad",
                r.ReceiptNumber != null && mixedDetail.TryGetValue(r.ReceiptNumber, out var detail) ? detail : MethodName(r.Method),
                r.OperationNumber, r.AmountPaid, r.Notes);
        foreach (var s in sales)
            csv.Row(AcademyClock.ToLocalDate(s.SaleDate), s.ReceiptNumber, s.Student, null, s.Concept, "Tienda", null, null, s.TotalPrice, s.Notes);
        csv.Row(null, null, null, null, null, null, null, "TOTAL", rows.Sum(r => r.AmountPaid) + sales.Sum(s => s.TotalPrice), null);
        return csv.ToBytes();
    }

    // ── Asistencia mensual por alumno ──────────────────────────────
    public async Task<byte[]> AttendanceCsvAsync(Guid academyId, int year, int month, List<Guid>? categoryScope)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);
        var q = _db.Attendances.AsNoTracking()
            .Where(a => a.AcademyId == academyId && a.Date >= from && a.Date < to);
        // Fuera del administrador, solo alumnos activos de sus categorías.
        if (categoryScope != null) q = q.Where(a => a.Student.IsActive && categoryScope.Contains(a.Student.CategoryId));

        var rows = await q.Select(a => new
        {
            a.StudentId, Student = a.Student.LastName + ", " + a.Student.FirstName,
            Category = a.Student.Category.Name, Headquarter = a.Student.Headquarter.Name, a.Status
        }).ToListAsync();

        var csv = new Csv("Alumno", "Sede", "Categoría", "Presente", "Tarde", "Falta", "Justificada", "Registros", "% Asistencia", "Alerta (<70%)");
        foreach (var g in rows.GroupBy(r => r.StudentId).OrderBy(g => g.First().Category).ThenBy(g => g.First().Student))
        {
            var f = g.First();
            int P(AttendanceStatus st) => g.Count(r => r.Status == st);
            var countable = g.Count(r => r.Status != AttendanceStatus.Excused);
            decimal? pct = countable == 0 ? null : Math.Round(100m * (P(AttendanceStatus.Present) + P(AttendanceStatus.Late)) / countable, 1);
            csv.Row(f.Student, f.Headquarter, f.Category, P(AttendanceStatus.Present), P(AttendanceStatus.Late),
                P(AttendanceStatus.Absent), P(AttendanceStatus.Excused), g.Count(), pct, pct < 70 ? "Sí" : "");
        }
        return csv.ToBytes();
    }

    private static string MethodName(PaymentMethod m) => m switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.BankTransfer => "Transferencia",
        PaymentMethod.Yape => "Yape",
        PaymentMethod.Plin => "Plin",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.Mixed => "Mixto",
        _ => "Otro"
    };

    /// <summary>Generador CSV mínimo para Excel (es-PE).</summary>
    private sealed class Csv
    {
        private readonly StringBuilder _sb = new();

        public Csv(params string[] headers) => Row(headers.Cast<object?>().ToArray());

        public void Row(params object?[] values) =>
            _sb.Append(string.Join(';', values.Select(Format))).Append("\r\n");

        private static string Format(object? v)
        {
            var s = v switch
            {
                null => "",
                DateTime d => d.ToString("dd/MM/yyyy", Es),
                decimal m => m.ToString("0.00", Es),
                double dbl => dbl.ToString("0.##", Es),
                _ => Convert.ToString(v, Es) ?? ""
            };
            // Evita inyección de fórmulas al abrir en Excel (=, +, -, @ al inicio de un texto).
            if (v is string && s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
            return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }

        public byte[] ToBytes() => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_sb.ToString())).ToArray();
    }
}
