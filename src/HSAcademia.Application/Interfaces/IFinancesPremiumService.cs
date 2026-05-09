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
    Task<ExpenseDto> UpdateExpenseAsync(Guid academyId, Guid expenseId, UpdateExpenseDto dto);
    Task DeleteExpenseAsync(Guid expenseId, Guid academyId);

    // ── Petty Cash ────────────────────────────────────────────────────────────
    Task<List<PettyCashDto>> GetPettyCashAsync(Guid academyId, int month, int year);
    Task<PettyCashDto> CreatePettyCashAsync(Guid academyId, CreatePettyCashDto dto);
    Task<PettyCashTransactionDto> AddTransactionAsync(Guid academyId, Guid registeredBy, AddPettyCashTransactionDto dto);

    // ── Staff Payments ────────────────────────────────────────────────────────
    Task<List<StaffPaymentDto>> GetStaffPaymentsAsync(Guid academyId, int month, int year);
    Task<List<StaffPaymentDto>> GetMyStaffPaymentsAsync(Guid academyId, Guid staffId, int month, int year);
    Task<StaffPaymentDto> CreateStaffPaymentAsync(Guid academyId, CreateStaffPaymentDto dto);
    Task<StaffPaymentDto> MarkStaffPaymentPaidAsync(Guid paymentId, Guid academyId);
    Task<StaffPaymentCalculationDto> CalculateStaffPaymentAsync(Guid academyId, Guid staffId, int month, int year);

    // ── Dashboard ─────────────────────────────────────────────────────────────
    Task<FinanceSummaryDto> GetFinanceSummaryAsync(Guid academyId, int month, int year);
    Task<List<MonthlyTrendDto>> GetTrendDataAsync(Guid academyId, int months);

    // ── Financial Goals ──────────────────────────────────────────────────────
    Task<FinancialGoalDto?> GetGoalAsync(Guid academyId, int month, int year);
    Task<FinancialGoalDto> UpsertGoalAsync(Guid academyId, CreateFinancialGoalDto dto);

    // ── Monthly Closings ─────────────────────────────────────────────────────
    Task<MonthlyClosingDto?> GetMonthlyClosingAsync(Guid academyId, int month, int year);
    Task<MonthlyClosingDto> CloseMonthAsync(Guid academyId, Guid closedBy, CloseMonthDto dto);
    // ── Financial Close ───────────────────────────────────────────────────────
    Task<FinancialPeriodDto> GetPeriodStatusAsync(Guid academyId, int month, int year);
    Task<FinancialPeriodDto> TogglePeriodCloseAsync(Guid academyId, int month, int year, Guid userId);
}
