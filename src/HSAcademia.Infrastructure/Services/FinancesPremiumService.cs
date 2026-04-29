using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.FinancesPremium;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Entities;
using HSAcademia.Domain.Enums;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

public class FinancesPremiumService : IFinancesPremiumService
{
    private readonly AppDbContext _context;

    public FinancesPremiumService(AppDbContext context)
    {
        _context = context;
    }

    // ══════════════════════════════════════════
    // EXPENSES
    // ══════════════════════════════════════════
    public async Task<List<ExpenseDto>> GetExpensesAsync(Guid academyId, int month, int year)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        return await _context.Expenses
            .Where(e => e.AcademyId == academyId && e.Date >= startDate && e.Date < endDate)
            .OrderByDescending(e => e.Date)
            .Select(e => new ExpenseDto
            {
                Id = e.Id,
                Type = e.Type,
                Amount = e.Amount,
                Date = e.Date,
                Description = e.Description,
                VoucherUrl = e.VoucherUrl
            })
            .ToListAsync();
    }

    public async Task<ExpenseDto> CreateExpenseAsync(Guid academyId, Guid registeredBy, CreateExpenseDto dto)
    {
        var expense = new Expense
        {
            AcademyId = academyId,
            Type = dto.Type,
            Amount = dto.Amount,
            Date = dto.Date.ToUniversalTime(),
            Description = dto.Description,
            VoucherUrl = dto.VoucherUrl,
            RegisteredById = registeredBy
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        return new ExpenseDto
        {
            Id = expense.Id,
            Type = expense.Type,
            Amount = expense.Amount,
            Date = expense.Date,
            Description = expense.Description,
            VoucherUrl = expense.VoucherUrl
        };
    }

    public async Task DeleteExpenseAsync(Guid expenseId, Guid academyId)
    {
        var expense = await _context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.AcademyId == academyId);

        if (expense == null) throw new Exception("Gasto no encontrado");

        expense.IsDeleted = true;
        expense.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    // ══════════════════════════════════════════
    // PETTY CASH (CAJA CHICA)
    // ══════════════════════════════════════════
    public async Task<List<PettyCashDto>> GetPettyCashAsync(Guid academyId, int month, int year)
    {
        return await _context.PettyCashes
            .Include(p => p.Transactions)
                .ThenInclude(t => t.RegisteredBy)
            .Include(p => p.Headquarter)
            .Where(p => p.AcademyId == academyId && p.Month == month && p.Year == year)
            .Select(p => new PettyCashDto
            {
                Id = p.Id,
                HeadquarterId = p.HeadquarterId,
                HeadquarterName = p.Headquarter != null ? p.Headquarter.Name : "General",
                Month = p.Month,
                Year = p.Year,
                AssignedAmount = p.AssignedAmount,
                CurrentBalance = p.CurrentBalance,
                Transactions = p.Transactions.OrderByDescending(t => t.Date).Select(t => new PettyCashTransactionDto
                {
                    Id = t.Id,
                    Type = t.Type,
                    Amount = t.Amount,
                    Concept = t.Concept,
                    Date = t.Date,
                    RegisteredByName = t.RegisteredBy != null ? t.RegisteredBy.FirstName + " " + t.RegisteredBy.LastName : null
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<PettyCashDto> CreatePettyCashAsync(Guid academyId, CreatePettyCashDto dto)
    {
        // Prevent duplicates for same month/year/headquarters
        var existing = await _context.PettyCashes
            .FirstOrDefaultAsync(p => p.AcademyId == academyId
                && p.Month == dto.Month
                && p.Year == dto.Year
                && p.HeadquarterId == dto.HeadquarterId);

        if (existing != null)
            throw new Exception("Ya existe una Caja Chica para ese período en esa sede.");

        var pettyCash = new PettyCash
        {
            AcademyId = academyId,
            HeadquarterId = dto.HeadquarterId,
            Month = dto.Month,
            Year = dto.Year,
            AssignedAmount = dto.AssignedAmount,
            CurrentBalance = dto.AssignedAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PettyCashes.Add(pettyCash);
        await _context.SaveChangesAsync();

        return new PettyCashDto
        {
            Id = pettyCash.Id,
            HeadquarterId = pettyCash.HeadquarterId,
            Month = pettyCash.Month,
            Year = pettyCash.Year,
            AssignedAmount = pettyCash.AssignedAmount,
            CurrentBalance = pettyCash.CurrentBalance
        };
    }

    public async Task<PettyCashTransactionDto> AddTransactionAsync(Guid academyId, Guid registeredBy, AddPettyCashTransactionDto dto)
    {
        var pettyCash = await _context.PettyCashes
            .FirstOrDefaultAsync(p => p.Id == dto.PettyCashId && p.AcademyId == academyId);

        if (pettyCash == null) throw new Exception("Caja Chica no encontrada.");

        var transaction = new PettyCashTransaction
        {
            PettyCashId = pettyCash.Id,
            Type = dto.Type,
            Amount = dto.Amount,
            Concept = dto.Concept,
            Date = dto.Date.ToUniversalTime(),
            RegisteredById = registeredBy
        };

        // Update balance
        if (dto.Type == PettyCashTransactionType.Expense)
            pettyCash.CurrentBalance -= dto.Amount;
        else
            pettyCash.CurrentBalance += dto.Amount;

        pettyCash.UpdatedAt = DateTime.UtcNow;

        _context.PettyCashTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        return new PettyCashTransactionDto
        {
            Id = transaction.Id,
            Type = transaction.Type,
            Amount = transaction.Amount,
            Concept = transaction.Concept,
            Date = transaction.Date
        };
    }

    // ══════════════════════════════════════════
    // STAFF PAYMENTS (NÓMINA)
    // ══════════════════════════════════════════
    public async Task<List<StaffPaymentDto>> GetStaffPaymentsAsync(Guid academyId, int month, int year)
    {
        return await _context.StaffPayments
            .Include(sp => sp.Staff)
            .Where(sp => sp.AcademyId == academyId
                && sp.PeriodMonth == month
                && sp.PeriodYear == year
                && !sp.IsDeleted)
            .OrderBy(sp => sp.Staff.FirstName)
            .Select(sp => new StaffPaymentDto
            {
                Id = sp.Id,
                StaffId = sp.StaffId,
                StaffName = sp.Staff.FirstName + " " + sp.Staff.LastName,
                PeriodMonth = sp.PeriodMonth,
                PeriodYear = sp.PeriodYear,
                BaseAmount = sp.BaseAmount,
                Bonuses = sp.Bonuses,
                Deductions = sp.Deductions,
                TotalPaid = sp.TotalPaid,
                Status = sp.Status,
                PaidAt = sp.PaidAt,
                Notes = sp.Notes
            })
            .ToListAsync();
    }

    public async Task<StaffPaymentDto> CreateStaffPaymentAsync(Guid academyId, CreateStaffPaymentDto dto)
    {
        var payment = new StaffPayment
        {
            AcademyId = academyId,
            StaffId = dto.StaffId,
            PeriodMonth = dto.PeriodMonth,
            PeriodYear = dto.PeriodYear,
            BaseAmount = dto.BaseAmount,
            Bonuses = dto.Bonuses,
            Deductions = dto.Deductions,
            TotalPaid = dto.BaseAmount + dto.Bonuses - dto.Deductions,
            Status = StaffPaymentStatus.Pending,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.StaffPayments.Add(payment);
        await _context.SaveChangesAsync();

        var staff = await _context.Users.FindAsync(dto.StaffId);

        return new StaffPaymentDto
        {
            Id = payment.Id,
            StaffId = payment.StaffId,
            StaffName = staff != null ? $"{staff.FirstName} {staff.LastName}" : "—",
            PeriodMonth = payment.PeriodMonth,
            PeriodYear = payment.PeriodYear,
            BaseAmount = payment.BaseAmount,
            Bonuses = payment.Bonuses,
            Deductions = payment.Deductions,
            TotalPaid = payment.TotalPaid,
            Status = payment.Status,
            Notes = payment.Notes
        };
    }

    public async Task<StaffPaymentDto> MarkStaffPaymentPaidAsync(Guid paymentId, Guid academyId)
    {
        var payment = await _context.StaffPayments
            .Include(sp => sp.Staff)
            .FirstOrDefaultAsync(sp => sp.Id == paymentId && sp.AcademyId == academyId);

        if (payment == null) throw new Exception("Pago de nómina no encontrado.");

        payment.Status = StaffPaymentStatus.Paid;
        payment.PaidAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new StaffPaymentDto
        {
            Id = payment.Id,
            StaffId = payment.StaffId,
            StaffName = $"{payment.Staff.FirstName} {payment.Staff.LastName}",
            PeriodMonth = payment.PeriodMonth,
            PeriodYear = payment.PeriodYear,
            BaseAmount = payment.BaseAmount,
            Bonuses = payment.Bonuses,
            Deductions = payment.Deductions,
            TotalPaid = payment.TotalPaid,
            Status = payment.Status,
            PaidAt = payment.PaidAt,
            Notes = payment.Notes
        };
    }

    // ══════════════════════════════════════════
    // FINANCE SUMMARY DASHBOARD
    // ══════════════════════════════════════════
    public async Task<FinanceSummaryDto> GetFinanceSummaryAsync(Guid academyId, int month, int year)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        // Ingresos: pagos de mensualidades cobrados ese mes
        var income = await _context.PaymentRecords
            .Where(pr => pr.AcademyId == academyId
                && pr.IsPaid
                && pr.DueDate >= startDate
                && pr.DueDate < endDate)
            .SumAsync(pr => (decimal?)pr.AmountPaid) ?? 0m;

        // Egresos generales
        var expenses = await _context.Expenses
            .Where(e => e.AcademyId == academyId && e.Date >= startDate && e.Date < endDate)
            .GroupBy(e => e.Type)
            .Select(g => new ExpenseByCategoryDto
            {
                Category = g.Key.ToString(),
                Total = g.Sum(e => e.Amount)
            })
            .ToListAsync();

        var totalExpenses = expenses.Sum(e => e.Total);

        // Nómina pagada
        var staffTotal = await _context.StaffPayments
            .Where(sp => sp.AcademyId == academyId
                && sp.PeriodMonth == month
                && sp.PeriodYear == year
                && sp.Status == StaffPaymentStatus.Paid
                && !sp.IsDeleted)
            .SumAsync(sp => (decimal?)sp.TotalPaid) ?? 0m;

        return new FinanceSummaryDto
        {
            Month = month,
            Year = year,
            TotalIncome = income,
            TotalExpenses = totalExpenses,
            TotalStaffPayments = staffTotal,
            NetBalance = income - totalExpenses - staffTotal,
            ExpensesByCategory = expenses
        };
    }
}
