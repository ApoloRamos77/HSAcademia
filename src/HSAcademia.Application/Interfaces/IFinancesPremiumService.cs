using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.FinancesPremium;

namespace HSAcademia.Application.Interfaces;

public interface IFinancesPremiumService
{
    Task<List<ExpenseDto>> GetExpensesAsync(Guid academyId, int month, int year);
    Task<ExpenseDto> CreateExpenseAsync(Guid academyId, Guid registeredBy, CreateExpenseDto dto);
    Task DeleteExpenseAsync(Guid expenseId, Guid academyId);
}
