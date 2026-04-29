using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.FinancesPremium;

namespace HSAcademia.Application.Interfaces;

public interface IFinancesPremiumService
{
    // ── Expenses ──────────────────────────────────────────────────────────────
    Task<List<ExpenseDto>> GetExpensesAsync(Guid academyId, int month, int year);
    Task<ExpenseDto> CreateExpenseAsync(Guid academyId, Guid registeredBy, CreateExpenseDto dto);
    Task DeleteExpenseAsync(Guid expenseId, Guid academyId);

    // ── Petty Cash ────────────────────────────────────────────────────────────
    Task<List<PettyCashDto>> GetPettyCashAsync(Guid academyId, int month, int year);
    Task<PettyCashDto> CreatePettyCashAsync(Guid academyId, CreatePettyCashDto dto);
    Task<PettyCashTransactionDto> AddTransactionAsync(Guid academyId, Guid registeredBy, AddPettyCashTransactionDto dto);

    // ── Staff Payments ────────────────────────────────────────────────────────
    Task<List<StaffPaymentDto>> GetStaffPaymentsAsync(Guid academyId, int month, int year);
    Task<StaffPaymentDto> CreateStaffPaymentAsync(Guid academyId, CreateStaffPaymentDto dto);
    Task<StaffPaymentDto> MarkStaffPaymentPaidAsync(Guid paymentId, Guid academyId);

    // ── Dashboard ─────────────────────────────────────────────────────────────
    Task<FinanceSummaryDto> GetFinanceSummaryAsync(Guid academyId, int month, int year);
}
