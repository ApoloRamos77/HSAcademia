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

        var expenses = await _context.Expenses
            .Where(e => e.AcademyId == academyId && e.Date >= startDate && e.Date < endDate)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

        // Load linked products for each expense
        var expenseIds = expenses.Select(e => e.Id).ToList();
        var linkedProducts = await _context.Products
            .Where(p => p.PurchaseExpenseId != null && expenseIds.Contains(p.PurchaseExpenseId.Value))
            .ToListAsync();

        return expenses.Select(e => new ExpenseDto
        {
            Id = e.Id,
            Type = e.Type,
            Amount = e.Amount,
            Date = e.Date,
            Description = e.Description,
            Supplier = e.Supplier,
            VoucherUrl = e.VoucherUrl,
            Products = linkedProducts
                .Where(p => p.PurchaseExpenseId == e.Id)
                .Select(p => new PurchaseProductDto
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    ProductCategory = p.ProductCategory,
                    Quantity = p.Stock,
                    UnitCost = p.CostPrice,
                    SalePrice = p.Price,
                    ForSale = p.IsActive
                }).ToList()
        }).ToList();
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
            Supplier = dto.Supplier,
            VoucherUrl = dto.VoucherUrl,
            RegisteredById = registeredBy
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        // Create any linked products from this purchase
        var createdProducts = new List<PurchaseProductDto>();
        if (dto.Products?.Count > 0)
        {
            foreach (var pd in dto.Products)
            {
                var product = new Domain.Entities.Product
                {
                    AcademyId = academyId,
                    Name = pd.Name,
                    Description = pd.Description,
                    ProductCategory = pd.ProductCategory,
                    CostPrice = pd.UnitCost,
                    Price = pd.SalePrice,
                    Stock = pd.Quantity,
                    IsActive = pd.ForSale && pd.SalePrice > 0, // only active for sale if price is set
                    PurchaseExpenseId = expense.Id
                };
                _context.Products.Add(product);
                createdProducts.Add(new PurchaseProductDto
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    ProductCategory = product.ProductCategory,
                    Quantity = product.Stock,
                    UnitCost = product.CostPrice,
                    SalePrice = product.Price,
                    ForSale = product.IsActive
                });
            }
            await _context.SaveChangesAsync();
        }

        return new ExpenseDto
        {
            Id = expense.Id,
            Type = expense.Type,
            Amount = expense.Amount,
            Date = expense.Date,
            Description = expense.Description,
            Supplier = expense.Supplier,
            VoucherUrl = expense.VoucherUrl,
            Products = createdProducts
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

    public async Task<List<StaffPaymentDto>> GetMyStaffPaymentsAsync(Guid academyId, Guid staffId, int month, int year)
    {
        var query = _context.StaffPayments
            .Include(sp => sp.Staff)
            .Where(sp => sp.AcademyId == academyId && sp.StaffId == staffId && !sp.IsDeleted);

        if (month > 0) query = query.Where(sp => sp.PeriodMonth == month && sp.PeriodYear == year);

        return await query
            .OrderByDescending(sp => sp.PeriodYear).ThenByDescending(sp => sp.PeriodMonth)
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

    public async Task<StaffPaymentCalculationDto> CalculateStaffPaymentAsync(Guid academyId, Guid staffId, int month, int year)
    {
        var staff = await _context.Users.FirstOrDefaultAsync(u => u.Id == staffId && u.AcademyId == academyId);
        if (staff == null) throw new Exception("Personal no encontrado.");

        if (staff.PaymentType == StaffPaymentType.Monthly)
        {
            return new StaffPaymentCalculationDto
            {
                StaffId = staffId,
                BaseAmount = staff.PaymentRate,
                SessionsCount = null
            };
        }
        
        // PerSession calculation
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        // Count distinct events in the month where the teacher was assigned and took attendance
        var sessionsCount = await _context.Events
            .Where(e => e.AcademyId == academyId 
                && e.TeacherId == staffId 
                && e.StartTime >= startDate 
                && e.StartTime < endDate
                && _context.Attendances.Any(a => a.EventId == e.Id))
            .CountAsync();

        return new StaffPaymentCalculationDto
        {
            StaffId = staffId,
            BaseAmount = sessionsCount * staff.PaymentRate,
            SessionsCount = sessionsCount
        };
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

        // Descuentos: becas, exoneraciones (montos que se dejaron de cobrar)
        var totalDiscounts = await _context.PaymentRecords
            .Where(pr => pr.AcademyId == academyId
                && pr.DueDate >= startDate
                && pr.DueDate < endDate
                && pr.DiscountAmount > 0)
            .SumAsync(pr => (decimal?)pr.DiscountAmount) ?? 0m;

        // Tienda: ventas de productos (sin contar obsequios)
        var storeRevenue = await _context.ProductSales
            .Where(ps => ps.AcademyId == academyId
                && !ps.IsGift
                && ps.SaleDate >= startDate
                && ps.SaleDate < endDate)
            .SumAsync(ps => (decimal?)ps.TotalPrice) ?? 0m;

        // Pérdida por obsequios: costo (precio de venta) de artículos entregados gratis
        var giftCost = await _context.ProductSales
            .Where(ps => ps.AcademyId == academyId
                && ps.IsGift
                && ps.SaleDate >= startDate
                && ps.SaleDate < endDate)
            .SumAsync(ps => (decimal?)ps.DiscountAmount) ?? 0m;

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
            TotalStoreRevenue = storeRevenue,
            TotalExpenses = totalExpenses,
            TotalStaffPayments = staffTotal,
            TotalDiscounts = totalDiscounts,
            TotalGiftCost = giftCost,
            NetBalance = (income + storeRevenue) - totalExpenses - staffTotal,
            ExpensesByCategory = expenses
        };
    }

    // ══════════════════════════════════════════
    // TREND DATA (Last N months)
    // ══════════════════════════════════════════
    public async Task<List<MonthlyTrendDto>> GetTrendDataAsync(Guid academyId, int months = 6)
    {
        var result = new List<MonthlyTrendDto>();
        var now = DateTime.UtcNow;

        var shortMonths = new[] { "Ene","Feb","Mar","Abr","May","Jun","Jul","Ago","Sep","Oct","Nov","Dic" };

        for (int i = months - 1; i >= 0; i--)
        {
            var refDate = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            var m = refDate.Month;
            var y = refDate.Year;
            var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);

            var income = await _context.PaymentRecords
                .Where(pr => pr.AcademyId == academyId && pr.IsPaid && pr.DueDate >= start && pr.DueDate < end)
                .SumAsync(pr => (decimal?)pr.AmountPaid) ?? 0m;

            var storeRev = await _context.ProductSales
                .Where(ps => ps.AcademyId == academyId && !ps.IsGift && ps.SaleDate >= start && ps.SaleDate < end)
                .SumAsync(ps => (decimal?)ps.TotalPrice) ?? 0m;

            var discounts = await _context.PaymentRecords
                .Where(pr => pr.AcademyId == academyId && pr.DueDate >= start && pr.DueDate < end && pr.DiscountAmount > 0)
                .SumAsync(pr => (decimal?)pr.DiscountAmount) ?? 0m;

            var exp = await _context.Expenses
                .Where(e => e.AcademyId == academyId && e.Date >= start && e.Date < end)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            var staff = await _context.StaffPayments
                .Where(sp => sp.AcademyId == academyId && sp.PeriodMonth == m && sp.PeriodYear == y
                    && sp.Status == StaffPaymentStatus.Paid && !sp.IsDeleted)
                .SumAsync(sp => (decimal?)sp.TotalPaid) ?? 0m;

            result.Add(new MonthlyTrendDto
            {
                Label = $"{shortMonths[m - 1]} {y}",
                Income = income,
                StoreRevenue = storeRev,
                Expenses = exp,
                StaffPayments = staff,
                Discounts = discounts,
                NetBalance = (income + storeRev) - exp - staff
            });
        }

        return result;
    }

    // ══════════════════════════════════════════
    // FINANCIAL GOALS
    // ══════════════════════════════════════════
    public async Task<FinancialGoalDto?> GetGoalAsync(Guid academyId, int month, int year)
    {
        var goal = await _context.FinancialGoals
            .FirstOrDefaultAsync(g => g.AcademyId == academyId && g.Month == month && g.Year == year);

        if (goal == null) return null;

        var summary = await GetFinanceSummaryAsync(academyId, month, year);

        var incomeProgress = goal.TargetIncome > 0 ? (summary.TotalIncome / goal.TargetIncome) * 100 : 0;
        var profitProgress = goal.TargetProfit > 0 ? (summary.NetBalance / goal.TargetProfit) * 100 : 0;
        
        // Prevent negative progress or missing values if we exceed the goal
        var missingIncome = goal.TargetIncome - summary.TotalIncome;
        if (missingIncome < 0) missingIncome = 0;

        return new FinancialGoalDto
        {
            Id = goal.Id,
            Month = goal.Month,
            Year = goal.Year,
            TargetIncome = goal.TargetIncome,
            TargetProfit = goal.TargetProfit,
            Status = goal.Status.ToString(),
            CurrentIncome = summary.TotalIncome,
            CurrentProfit = summary.NetBalance,
            IncomeProgress = Math.Min(100, Math.Round(incomeProgress, 1)),
            ProfitProgress = Math.Min(100, Math.Round(profitProgress, 1)),
            MissingIncome = missingIncome
        };
    }

    public async Task<FinancialGoalDto> UpsertGoalAsync(Guid academyId, CreateFinancialGoalDto dto)
    {
        var goal = await _context.FinancialGoals
            .FirstOrDefaultAsync(g => g.AcademyId == academyId && g.Month == dto.Month && g.Year == dto.Year);

        if (goal == null)
        {
            goal = new FinancialGoal
            {
                AcademyId = academyId,
                Month = dto.Month,
                Year = dto.Year,
                TargetIncome = dto.TargetIncome,
                TargetProfit = dto.TargetProfit,
                Status = FinancialGoalStatus.InProgress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.FinancialGoals.Add(goal);
        }
        else
        {
            goal.TargetIncome = dto.TargetIncome;
            goal.TargetProfit = dto.TargetProfit;
            goal.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return await GetGoalAsync(academyId, dto.Month, dto.Year) ?? throw new Exception("Error al recuperar la meta.");
    }

    // ══════════════════════════════════════════
    // MONTHLY CLOSINGS
    // ══════════════════════════════════════════
    public async Task<MonthlyClosingDto?> GetMonthlyClosingAsync(Guid academyId, int month, int year)
    {
        var closing = await _context.MonthlyClosings
            .Include(c => c.ClosedBy)
            .FirstOrDefaultAsync(c => c.AcademyId == academyId && c.Month == month && c.Year == year);

        if (closing == null) return null;

        return new MonthlyClosingDto
        {
            Id = closing.Id,
            Month = closing.Month,
            Year = closing.Year,
            TotalIncome = closing.TotalIncome,
            TotalExpenses = closing.TotalExpenses,
            NetProfit = closing.NetProfit,
            PettyCashBalance = closing.PettyCashBalance,
            Status = closing.Status.ToString(),
            ClosedAt = closing.ClosedAt,
            ClosedByName = closing.ClosedBy != null ? $"{closing.ClosedBy.FirstName} {closing.ClosedBy.LastName}" : null,
            Notes = closing.Notes
        };
    }

    public async Task<MonthlyClosingDto> CloseMonthAsync(Guid academyId, Guid closedBy, CloseMonthDto dto)
    {
        var existing = await _context.MonthlyClosings
            .FirstOrDefaultAsync(c => c.AcademyId == academyId && c.Month == dto.Month && c.Year == dto.Year);

        if (existing != null && existing.Status == MonthlyClosingStatus.Closed)
            throw new Exception("El mes ya se encuentra cerrado.");

        var summary = await GetFinanceSummaryAsync(academyId, dto.Month, dto.Year);

        var pettyCashBalance = await _context.PettyCashes
            .Where(p => p.AcademyId == academyId && p.Month == dto.Month && p.Year == dto.Year)
            .SumAsync(p => p.CurrentBalance);

        if (existing == null)
        {
            existing = new MonthlyClosing
            {
                AcademyId = academyId,
                Month = dto.Month,
                Year = dto.Year,
                TotalIncome = summary.TotalIncome,
                TotalExpenses = summary.TotalExpenses + summary.TotalStaffPayments,
                NetProfit = summary.NetBalance,
                PettyCashBalance = pettyCashBalance,
                Status = MonthlyClosingStatus.Closed,
                ClosedAt = DateTime.UtcNow,
                ClosedById = closedBy,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.MonthlyClosings.Add(existing);
        }
        else
        {
            existing.TotalIncome = summary.TotalIncome;
            existing.TotalExpenses = summary.TotalExpenses + summary.TotalStaffPayments;
            existing.NetProfit = summary.NetBalance;
            existing.PettyCashBalance = pettyCashBalance;
            existing.Status = MonthlyClosingStatus.Closed;
            existing.ClosedAt = DateTime.UtcNow;
            existing.ClosedById = closedBy;
            existing.Notes = dto.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return await GetMonthlyClosingAsync(academyId, dto.Month, dto.Year) ?? throw new Exception("Error al recuperar el cierre.");
    }
}
