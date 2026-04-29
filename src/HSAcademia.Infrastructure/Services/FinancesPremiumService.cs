using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.FinancesPremium;
using HSAcademia.Application.Interfaces;
using HSAcademia.Domain.Entities;
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

    public async Task<List<ExpenseDto>> GetExpensesAsync(Guid academyId, int month, int year)
    {
        // Calcular inicio y fin del mes de manera segura
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        var expenses = await _context.Expenses
            .Where(e => e.AcademyId == academyId && e.Date >= startDate && e.Date < endDate)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        return expenses.Select(e => new ExpenseDto
        {
            Id = e.Id,
            Type = e.Type,
            Amount = e.Amount,
            Date = e.Date,
            Description = e.Description,
            VoucherUrl = e.VoucherUrl
        }).ToList();
    }

    public async Task<ExpenseDto> CreateExpenseAsync(Guid academyId, Guid registeredBy, CreateExpenseDto dto)
    {
        var expense = new Expense
        {
            AcademyId = academyId,
            Type = dto.Type,
            Amount = dto.Amount,
            Date = dto.Date,
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
}
