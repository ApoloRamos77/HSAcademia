using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Reglas de descuento de la academia:
/// - Hermanos: automático al generar la mensualidad (desde el N-ésimo hijo; los de mayor tarifa pagan completo).
/// - Pronto pago: automático al cancelar la mensualidad completa hasta X días antes del vencimiento.
/// - Promoción: con vigencia; el cajero la elige al cobrar.
/// Los descuentos automáticos del cobro (pronto pago / promoción) NO se acumulan: se aplica el mayor.
/// </summary>
public class DiscountService
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;

    public DiscountService(AppDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ── Configuración ─────────────────────────────────────────────
    public async Task<List<DiscountRuleDto>> ListAsync(Guid academyId, bool onlyActive = false)
    {
        var q = _db.DiscountRules.AsNoTracking().Where(r => r.AcademyId == academyId);
        if (onlyActive) q = q.Where(r => r.IsActive);
        var rules = await q.OrderBy(r => r.Kind).ThenBy(r => r.Name).ToListAsync();
        var catIds = rules.Where(r => r.CategoryId.HasValue).Select(r => r.CategoryId!.Value).Distinct().ToList();
        var cats = await _db.Categories.Where(c => catIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
        var today = AcademyClock.Today;
        return rules.Select(r => Map(r, r.CategoryId.HasValue && cats.TryGetValue(r.CategoryId.Value, out var n) ? n : null, today)).ToList();
    }

    public async Task<DiscountRuleDto> SaveAsync(Guid academyId, Guid? id, SaveDiscountRuleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new Exception("Indica el nombre del descuento.");
        if (!Enum.TryParse<DiscountKind>(dto.Kind, true, out var kind)) throw new Exception("Tipo de descuento no válido.");
        if (!Enum.TryParse<DiscountValueType>(dto.ValueType, true, out var valueType)) valueType = DiscountValueType.Percent;
        if (!Enum.TryParse<DiscountScope>(dto.Scope, true, out var scope)) scope = DiscountScope.MonthlyFees;
        if (dto.Value <= 0) throw new Exception("El valor del descuento debe ser mayor a cero.");
        if (valueType == DiscountValueType.Percent && dto.Value > 100) throw new Exception("El porcentaje no puede superar 100%.");
        if (dto.ValidFrom.HasValue && dto.ValidTo.HasValue && dto.ValidTo.Value.Date < dto.ValidFrom.Value.Date)
            throw new Exception("La fecha de fin de la vigencia es anterior a la de inicio.");

        switch (kind)
        {
            case DiscountKind.Sibling:
                if (dto.FromSiblingNumber is null or < 2 or > 10)
                    throw new Exception("Indica desde qué hijo aplica el descuento (2 = segundo hijo en adelante).");
                scope = DiscountScope.MonthlyFees;
                break;
            case DiscountKind.EarlyPayment:
                if (dto.DaysBeforeDue is null or < 0 or > 31)
                    throw new Exception("Indica hasta cuántos días antes del vencimiento aplica el pronto pago (0 = hasta el día del vencimiento).");
                scope = DiscountScope.MonthlyFees;
                break;
        }

        if (dto.CategoryId.HasValue && !await _db.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.AcademyId == academyId))
            throw new Exception("La categoría no es válida.");

        DiscountRule rule;
        if (id.HasValue)
        {
            rule = await _db.DiscountRules.FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId)
                ?? throw new KeyNotFoundException("Descuento no encontrado.");
        }
        else
        {
            rule = new DiscountRule { AcademyId = academyId };
            _db.DiscountRules.Add(rule);
        }

        rule.Name = dto.Name.Trim();
        rule.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        rule.Kind = kind;
        rule.ValueType = valueType;
        rule.Value = dto.Value;
        rule.Scope = scope;
        rule.FromSiblingNumber = kind == DiscountKind.Sibling ? dto.FromSiblingNumber : null;
        rule.DaysBeforeDue = kind == DiscountKind.EarlyPayment ? dto.DaysBeforeDue : null;
        rule.ValidFrom = dto.ValidFrom?.Date;
        rule.ValidTo = dto.ValidTo?.Date;
        rule.CategoryId = dto.CategoryId;
        rule.IsActive = dto.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;

        _audit.Add(academyId, id.HasValue ? "finances.discount.update" : "finances.discount.create", nameof(DiscountRule), rule.Id,
            new { rule.Name, kind = kind.ToString(), rule.Value, valueType = valueType.ToString(), rule.IsActive });
        await _db.SaveChangesAsync();
        return (await ListAsync(academyId)).First(r => r.Id == rule.Id);
    }

    public async Task DeleteAsync(Guid academyId, Guid id)
    {
        var rule = await _db.DiscountRules.FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Descuento no encontrado.");
        rule.IsDeleted = true;
        rule.IsActive = false;
        rule.UpdatedAt = DateTime.UtcNow;
        _audit.Add(academyId, "finances.discount.delete", nameof(DiscountRule), rule.Id, new { rule.Name });
        await _db.SaveChangesAsync();
    }

    // ── Motor: descuento por hermanos ─────────────────────────────

    /// <summary>
    /// Posición de cada alumno dentro de su familia (mismo apoderado), solo entre hermanos que pagan.
    /// Los de mayor tarifa van primero (pagan completo); el descuento recae en los siguientes.
    /// </summary>
    public static Dictionary<Guid, int> SiblingPositions(IEnumerable<Student> students, Func<Student, decimal> fee)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var family in students
                     .Where(s => s.GuardianId.HasValue && s.IsActive && !s.IsGuest && !s.IsScholarship && fee(s) > 0)
                     .GroupBy(s => s.GuardianId!.Value))
        {
            var ordered = family.OrderByDescending(fee).ThenBy(s => s.EnrollmentDate).ThenBy(s => s.Id).ToList();
            for (var i = 0; i < ordered.Count; i++) result[ordered[i].Id] = i + 1;
        }
        return result;
    }

    /// <summary>Regla de hermanos que corresponde a una posición (la más específica; a igualdad, la de mayor valor).</summary>
    public static DiscountRule? SiblingRuleFor(IEnumerable<DiscountRule> siblingRules, Student student, int position, DateTime periodDate) =>
        siblingRules
            .Where(r => r.Kind == DiscountKind.Sibling && r.IsValidOn(periodDate) && r.FromSiblingNumber <= position &&
                        (r.CategoryId == null || r.CategoryId == student.CategoryId))
            .OrderByDescending(r => r.FromSiblingNumber)
            .ThenByDescending(r => r.Value)
            .FirstOrDefault();

    public Task<List<DiscountRule>> ActiveRulesAsync(Guid academyId, DiscountKind kind) =>
        _db.DiscountRules.Where(r => r.AcademyId == academyId && r.Kind == kind && r.IsActive).ToListAsync();

    /// <summary>Calcula la posición de un alumno entre sus hermanos (para generar una deuda individual).</summary>
    public async Task<int?> SiblingPositionAsync(Guid academyId, Student student)
    {
        if (!student.GuardianId.HasValue) return null;
        var family = await _db.Students.Include(s => s.Category)
            .Where(s => s.AcademyId == academyId && s.GuardianId == student.GuardianId)
            .ToListAsync();
        var positions = SiblingPositions(family, s => s.PreferentialFee ?? s.Category?.MonthlyFee ?? 0m);
        return positions.TryGetValue(student.Id, out var p) ? p : null;
    }

    internal static DiscountRuleDto Map(DiscountRule r, string? categoryName, DateTime today) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        Kind = r.Kind.ToString(),
        ValueType = r.ValueType.ToString(),
        Value = r.Value,
        Scope = r.Scope.ToString(),
        FromSiblingNumber = r.FromSiblingNumber,
        DaysBeforeDue = r.DaysBeforeDue,
        ValidFrom = r.ValidFrom,
        ValidTo = r.ValidTo,
        CategoryId = r.CategoryId,
        CategoryName = categoryName,
        IsActive = r.IsActive,
        IsCurrentlyValid = r.IsValidOn(today),
        Label = r.Label
    };
}
