using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class FinancesService
{
    private readonly AppDbContext _context;

    public FinancesService(AppDbContext context)
    {
        _context = context;
    }

    // --- Configuration ---
    public async Task<FinancialConfigDto> GetConfigAsync(Guid academyId)
    {
        var config = await _context.AcademyFinancialConfigs.FirstOrDefaultAsync(c => c.AcademyId == academyId);
        if (config == null)
        {
            config = new AcademyFinancialConfig { AcademyId = academyId, DefaultPaymentDay = 5 };
            _context.AcademyFinancialConfigs.Add(config);
            await _context.SaveChangesAsync();
        }
        return new FinancialConfigDto { DefaultPaymentDay = config.DefaultPaymentDay };
    }

    public async Task<FinancialConfigDto> UpdateConfigAsync(Guid academyId, UpdateFinancialConfigDto dto)
    {
        var config = await _context.AcademyFinancialConfigs.FirstOrDefaultAsync(c => c.AcademyId == academyId);
        if (config == null)
        {
            config = new AcademyFinancialConfig { AcademyId = academyId, DefaultPaymentDay = dto.DefaultPaymentDay };
            _context.AcademyFinancialConfigs.Add(config);
        }
        else
        {
            config.DefaultPaymentDay = dto.DefaultPaymentDay;
        }
        await _context.SaveChangesAsync();
        return new FinancialConfigDto { DefaultPaymentDay = config.DefaultPaymentDay };
    }

    // --- Motor de Generación de Deudas ---
    public async Task<int> GenerateMonthlyDebtsAsync(Guid academyId)
    {
        var config = await GetConfigAsync(academyId);
        var today = DateTime.UtcNow;
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueDate = new DateTime(today.Year, today.Month, config.DefaultPaymentDay, 0, 0, 0, DateTimeKind.Utc);

        var students = await _context.Students
            .Include(s => s.Category)
            .Where(s => s.AcademyId == academyId && s.IsActive && !s.IsGuest && !s.IsScholarship)
            .ToListAsync();

        int generatedCount = 0;

        foreach (var student in students)
        {
            // Skip if payment start date is in the future
            if (student.PaymentStartDate.HasValue && student.PaymentStartDate.Value > today)
                continue;

            // Check if debt already exists for this month and this student
            string description = $"Mensualidad {today.ToString("MMMM yyyy")}";
            
            var existingDebt = await _context.PaymentRecords.FirstOrDefaultAsync(p => 
                p.AcademyId == academyId && 
                p.StudentId == student.Id && 
                p.Type == PaymentType.MonthlyFee && 
                p.Description == description);

            if (existingDebt == null)
            {
                var amount = student.PreferentialFee ?? student.Category.MonthlyFee;
                if (amount > 0)
                {
                    _context.PaymentRecords.Add(new PaymentRecord
                    {
                        AcademyId = academyId,
                        StudentId = student.Id,
                        Description = description,
                        Amount = amount,
                        DueDate = dueDate,
                        IsPaid = false,
                        Type = PaymentType.MonthlyFee
                    });
                    generatedCount++;
                }
            }
        }

        if (generatedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return generatedCount;
    }

    // --- List Debts ---
    public async Task<List<PaymentRecordDto>> GetPendingDebtsAsync(Guid academyId)
    {
        var today = DateTime.UtcNow;

        var debts = await _context.PaymentRecords
            .Include(p => p.Student)
            .ThenInclude(s => s.Category)
            .Where(p => p.AcademyId == academyId && !p.IsPaid)
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        return debts.Select(p => new PaymentRecordDto
        {
            Id = p.Id,
            StudentId = p.StudentId,
            StudentName = p.Student.FirstName + " " + p.Student.LastName,
            CategoryName = p.Student.Category.Name,
            Description = p.Description,
            Amount = p.Amount,
            DueDate = p.DueDate,
            IsPaid = p.IsPaid,
            Type = (int)p.Type,
            DaysOverdue = today > p.DueDate ? (today - p.DueDate).Days : 0
        }).ToList();
    }

    public async Task<PaymentRecordDto> MarkAsPaidAsync(Guid academyId, Guid paymentId)
    {
        var record = await _context.PaymentRecords
            .Include(p => p.Student)
            .ThenInclude(s => s.Category)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.AcademyId == academyId);

        if (record == null) throw new Exception("Registro no encontrado.");

        record.IsPaid = true;
        record.PaidDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new PaymentRecordDto
        {
            Id = record.Id,
            StudentId = record.StudentId,
            StudentName = record.Student.FirstName + " " + record.Student.LastName,
            CategoryName = record.Student.Category.Name,
            Description = record.Description,
            Amount = record.Amount,
            DueDate = record.DueDate,
            IsPaid = record.IsPaid,
            PaidDate = record.PaidDate,
            Type = (int)record.Type,
            DaysOverdue = 0
        };
    }
}
