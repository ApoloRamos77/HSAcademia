using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HSAcademia.Application.DTOs.Finances;
using HSAcademia.Domain.Entities;
using HSAcademia.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Services;

/// <summary>
/// Crea la cabecera de un comprobante. Lo usan el cobro con varios ítems, el cobro simple de una deuda,
/// la venta de tienda y la validación de comprobantes de la app: así TODO recibo nuevo queda registrado
/// con sus ítems y se puede reimprimir o anular de la misma forma.
/// No guarda: agrega al DbContext para que se guarde en el mismo SaveChanges de la operación.
/// </summary>
public class ReceiptRecorder
{
    /// <summary>Canal de los recibos migrados desde cobros anteriores a los comprobantes (no entran al cuadre de caja).</summary>
    public const string HistoricChannel = "historico";

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public ReceiptRecorder(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public Guid? CurrentUserId
    {
        get
        {
            var user = _http.HttpContext?.User;
            var value = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public async Task<Receipt> CreateAsync(Guid academyId, string number, DateTime issuedAt, string payerName, string? payerDocument,
        Guid? guardianId, PaymentMethod method, string? operationNumber, string? voucherUrl, string? notes, string? channel)
    {
        var userId = CurrentUserId;
        string? createdBy = null;
        if (userId.HasValue)
            createdBy = await _db.Users.Where(u => u.Id == userId.Value)
                .Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync();

        var receipt = new Receipt
        {
            AcademyId = academyId,
            Number = number,
            IssuedAt = issuedAt,
            PayerName = Truncate(string.IsNullOrWhiteSpace(payerName) ? "Público general" : payerName.Trim(), 200)!,
            PayerDocument = Truncate(payerDocument?.Trim(), 20),
            GuardianId = guardianId,
            Method = method,
            OperationNumber = Truncate(operationNumber?.Trim(), 100),
            VoucherUrl = voucherUrl,
            Notes = Truncate(notes?.Trim(), 500),
            Channel = channel,
            CreatedByUserId = userId,
            CreatedByName = createdBy,
            Status = ReceiptStatus.Issued,
            CreatedAt = DateTime.UtcNow
        };
        _db.Receipts.Add(receipt);
        return receipt;
    }

    public static void AddLine(Receipt receipt, ReceiptLine line)
    {
        line.ReceiptId = receipt.Id;
        line.Position = receipt.Lines.Count + 1;
        line.Description = Truncate(line.Description, 300) ?? string.Empty;
        receipt.Lines.Add(line);
        receipt.Subtotal = receipt.Lines.Sum(l => l.UnitPrice * l.Quantity);
        receipt.DiscountTotal = receipt.Lines.Sum(l => l.Discount);
        receipt.Total = receipt.Lines.Sum(l => l.Total);
    }

    /// <summary>Quién paga por defecto: el apoderado del alumno o, si no tiene, el propio alumno.</summary>
    public static (string Name, string? Document, Guid? GuardianId) PayerOf(Student? s)
    {
        if (s == null) return ("Público general", null, null);
        if (s.Guardian != null)
            return ($"{s.Guardian.FirstName} {s.Guardian.LastName}".Trim(), s.Guardian.DocumentNumber, s.GuardianId);
        return ($"{s.FirstName} {s.LastName}".Trim(), s.DocumentNumber, null);
    }

    public static string? Truncate(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
}

/// <summary>Comprobantes con varios ítems: cuenta familiar, cobro, consulta, reimpresión y anulación.</summary>
public class ReceiptService
{
    private const int MaxPaymentParts = 4;

    private readonly AppDbContext _db;
    private readonly FinancesService _finances;
    private readonly FinancialPeriodGuard _periods;
    private readonly AuditService _audit;
    private readonly ReceiptRecorder _recorder;
    private readonly AccessScopeService _scope;

    public ReceiptService(AppDbContext db, FinancesService finances, FinancialPeriodGuard periods, AuditService audit, ReceiptRecorder recorder, AccessScopeService scope)
    {
        _scope = scope;
        _db = db;
        _finances = finances;
        _periods = periods;
        _audit = audit;
        _recorder = recorder;
    }

    // ─────────────────────────────────────────────────────────────
    // Cuenta familiar
    // ─────────────────────────────────────────────────────────────
    public async Task<FamilyAccountDto> GetFamilyAccountAsync(Guid academyId, Guid studentId)
    {
        var student = await _db.Students.Include(s => s.Guardian)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Alumno no encontrado.");

        var family = await _db.Students.Include(s => s.Category)
            .Where(s => s.AcademyId == academyId &&
                        (s.Id == studentId || (student.GuardianId != null && s.GuardianId == student.GuardianId)))
            .OrderBy(s => s.FirstName)
            .ToListAsync();
        var ids = family.Select(s => s.Id).ToList();

        var debts = await _db.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Category)
            .Include(p => p.Installments)
            .Where(p => p.AcademyId == academyId && !p.IsPaid && ids.Contains(p.StudentId))
            .OrderBy(p => p.DueDate)
            .ToListAsync();

        var (payer, document, guardianId) = ReceiptRecorder.PayerOf(student);
        var mapped = debts.Select(FinancesService.MapToDto).ToList();
        return new FamilyAccountDto
        {
            GuardianId = guardianId,
            PayerName = payer,
            PayerDocument = document,
            PayerPhone = student.Guardian?.Phone ?? student.Phone,
            Students = family.Select(s => new FamilyStudentDto
            {
                StudentId = s.Id,
                Name = $"{s.FirstName} {s.LastName}".Trim(),
                CategoryName = s.Category?.Name ?? "",
                IsActive = s.IsActive
            }).ToList(),
            PendingDebts = mapped,
            TotalPending = mapped.Sum(d => d.Amount - d.AmountPaid)
        };
    }

    // ─────────────────────────────────────────────────────────────
    // Cobro con varios ítems
    // ─────────────────────────────────────────────────────────────
    /// <summary>Calcula el comprobante (descuentos incluidos) sin registrar nada: para mostrarlo antes de cobrar.</summary>
    public async Task<ReceiptDto> PreviewAsync(Guid academyId, CheckoutDto dto, bool canChargeDebts, bool canSellProducts)
    {
        ValidateRequest(dto, canChargeDebts, canSellProducts, requireOperation: false);
        try
        {
            var plan = await BuildPlanAsync(academyId, dto, forPreview: true);
            var lines = plan.Debts.Select(d => new ReceiptLineDto
            {
                Type = "Debt",
                Description = d.Record.Description,
                StudentId = d.Record.StudentId,
                StudentName = d.StudentName,
                Quantity = 1,
                UnitPrice = d.Amount + d.TotalDiscount,
                Discount = d.TotalDiscount,
                DiscountLabel = d.Label,
                Total = d.Amount,
                PaymentRecordId = d.Record.Id,
                PendingBefore = d.Pending
            }).Concat(plan.Debts.Where(d => d.LateFee > 0).Select(d => new ReceiptLineDto
            {
                Type = "Debt",
                Description = $"Mora · {d.Record.Description}",
                StudentId = d.Record.StudentId,
                StudentName = d.StudentName,
                Quantity = 1,
                UnitPrice = d.LateFee,
                Total = d.LateFee,
                DiscountLabel = "Pago fuera de fecha"
            })).Concat(plan.Products.Select(p => new ReceiptLineDto
            {
                Type = "Product",
                Description = p.Item.IsGift ? $"{p.Product.Name} (obsequio)" : p.Product.Name,
                StudentId = p.Student?.Id,
                StudentName = p.StudentName,
                Quantity = p.Item.Quantity,
                UnitPrice = p.Product.Price,
                Discount = p.Discount,
                DiscountLabel = p.Label,
                Total = p.Total,
                ProductId = p.Product.Id
            })).ToList();
            return new ReceiptDto
            {
                Number = "",
                IsPreview = true,
                IssuedAt = DateTime.UtcNow,
                PayerName = plan.PayerName,
                PayerDocument = plan.PayerDocument,
                GuardianId = plan.GuardianId,
                Method = dto.Method,
                Subtotal = lines.Sum(l => l.UnitPrice * l.Quantity),
                DiscountTotal = lines.Sum(l => l.Discount),
                Total = lines.Sum(l => l.Total),
                Lines = lines
            };
        }
        finally
        {
            _db.ChangeTracker.Clear(); // la vista previa nunca deja cambios pendientes
        }
    }

    public async Task<ReceiptDto> CheckoutAsync(Guid academyId, CheckoutDto dto, bool canChargeDebts, bool canSellProducts)
    {
        var method = ValidateRequest(dto, canChargeDebts, canSellProducts, requireOperation: true);

        var requestId = string.IsNullOrWhiteSpace(dto.ClientRequestId) ? null : dto.ClientRequestId.Trim();
        if (requestId is { Length: > 64 }) throw new Exception("Clave de solicitud no válida.");
        if (requestId != null)
        {
            var previous = await FindByRequestIdAsync(academyId, requestId);
            if (previous != null) return previous; // reintento de un cobro ya registrado
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await CheckoutOnceAsync(academyId, dto, method, requestId);
            }
            catch (DbUpdateException ex) when (requestId != null && ex is not DbUpdateConcurrencyException)
            {
                // Dos envíos simultáneos con la misma clave: el índice único deja pasar solo uno.
                _db.ChangeTracker.Clear();
                var winner = await FindByRequestIdAsync(academyId, requestId);
                if (winner != null) return winner;
                throw;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 4)
            {
                _db.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                throw new Exception("Otro usuario modificó una de las deudas o el stock mientras se cobraba. Actualiza e inténtalo nuevamente.");
            }
        }
    }

    private static PaymentMethod ValidateRequest(CheckoutDto dto, bool canChargeDebts, bool canSellProducts, bool requireOperation)
    {
        dto.Debts ??= new();
        dto.Products ??= new();
        if (dto.Debts.Count == 0 && dto.Products.Count == 0)
            throw new Exception("Agrega al menos un ítem al comprobante.");
        if (dto.Debts.Count + dto.Products.Count > 40)
            throw new Exception("Un comprobante admite como máximo 40 ítems.");
        if (dto.Debts.Count > 0 && !canChargeDebts)
            throw new UnauthorizedAccessException("No tienes permiso para cobrar mensualidades.");
        if (dto.Products.Count > 0 && !canSellProducts)
            throw new UnauthorizedAccessException("No tienes permiso para vender productos.");
        if (dto.Debts.Select(d => d.PaymentRecordId).Distinct().Count() != dto.Debts.Count)
            throw new Exception("Una deuda aparece dos veces en el comprobante.");

        foreach (var d in dto.Debts)
        {
            if (d.Amount < 0 || d.Discount < 0) throw new Exception("Los montos y descuentos no pueden ser negativos.");
            if (d.Amount.HasValue && d.Amount.Value + d.Discount <= 0) throw new Exception("Cada deuda del comprobante debe tener un monto a cobrar.");
        }
        foreach (var p in dto.Products)
        {
            if (p.Quantity <= 0 || p.Quantity > 999) throw new Exception("La cantidad de cada producto debe ser mayor a cero.");
            if (p.Discount < 0) throw new Exception("El descuento no puede ser negativo.");
        }

        if (dto.Payments is { Count: > 0 })
            return requireOperation ? ValidateMixedPayment(dto) : PaymentMethod.Mixed;

        if (!Enum.TryParse<PaymentMethod>(dto.Method, true, out var method)) method = PaymentMethod.Cash;
        if (method == PaymentMethod.Mixed) throw new Exception("Indica cómo se reparte el pago mixto.");
        if (requireOperation && RequiresOperation(method) && string.IsNullOrWhiteSpace(dto.OperationNumber) && string.IsNullOrWhiteSpace(dto.VoucherUrl))
            throw new Exception("Para pagos con Yape, Plin o transferencia indica el número de operación.");
        return method;
    }

    private static bool RequiresOperation(PaymentMethod method) =>
        method is PaymentMethod.Yape or PaymentMethod.Plin or PaymentMethod.BankTransfer;

    /// <summary>
    /// Revisa el reparto y deja en la cabecera las operaciones y el primer voucher, para que abonos y recibo los muestren.
    /// Que el reparto sume el total se comprueba al armar el recibo, cuando el servidor ya calculó los descuentos.
    /// </summary>
    private static PaymentMethod ValidateMixedPayment(CheckoutDto dto)
    {
        var parts = dto.Payments!;
        if (parts.Count < 2) throw new Exception("Un pago mixto necesita al menos dos medios de pago.");
        if (parts.Count > MaxPaymentParts) throw new Exception($"Un pago mixto admite como máximo {MaxPaymentParts} medios de pago.");

        var cashParts = 0;
        foreach (var part in parts)
        {
            if (!Enum.TryParse<PaymentMethod>(part.Method, true, out var method) || !Enum.IsDefined(method) || method == PaymentMethod.Mixed)
                throw new Exception("Uno de los medios de pago no es válido.");
            if (part.Amount <= 0 || decimal.Round(part.Amount, 2) != part.Amount)
                throw new Exception("Cada medio de pago debe tener un monto mayor a cero, con dos decimales como máximo.");
            if (RequiresOperation(method) && string.IsNullOrWhiteSpace(part.OperationNumber) && string.IsNullOrWhiteSpace(part.VoucherUrl))
                throw new Exception("Para cada pago con Yape, Plin o transferencia indica el número de operación.");
            if (method == PaymentMethod.Cash) cashParts++;
        }
        if (cashParts > 1) throw new Exception("El efectivo debe registrarse en una sola parte del pago.");

        var operations = parts.Select(p => p.OperationNumber?.Trim()).Where(o => !string.IsNullOrEmpty(o)).ToList();
        if (operations.Distinct(StringComparer.OrdinalIgnoreCase).Count() != operations.Count)
            throw new Exception("Un número de operación aparece dos veces en el pago.");

        dto.OperationNumber = operations.Count == 0 ? null : ReceiptRecorder.Truncate(string.Join(" / ", operations), 100);
        dto.VoucherUrl = parts.Select(p => p.VoucherUrl).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return PaymentMethod.Mixed;
    }

    private void AddMixedPayments(Receipt receipt, List<CheckoutPaymentDto> parts)
    {
        var paid = parts.Sum(p => p.Amount);
        var total = Math.Round(receipt.Total, 2);
        if (paid != total)
            throw new Exception($"Los pagos suman S/ {paid:N2} y el total del recibo es S/ {total:N2}. Ajusta los montos.");

        var position = 0;
        foreach (var part in parts)
        {
            _db.ReceiptPayments.Add(new ReceiptPayment
            {
                ReceiptId = receipt.Id,
                Position = ++position,
                Method = Enum.Parse<PaymentMethod>(part.Method, true),
                Amount = part.Amount,
                OperationNumber = ReceiptRecorder.Truncate(part.OperationNumber?.Trim(), 100),
                VoucherUrl = part.VoucherUrl
            });
        }
    }

    private async Task<ReceiptDto?> FindByRequestIdAsync(Guid academyId, string requestId)
    {
        var r = await _db.Receipts.AsNoTracking().Include(x => x.Lines).Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.AcademyId == academyId && x.ClientRequestId == requestId);
        return r == null ? null : Map(r);
    }

    // ── Plan del cobro: todo lo que se va a registrar, ya calculado (no modifica nada) ──
    private sealed class DebtPlan
    {
        public PaymentRecord Record = null!;
        public string StudentName = "";
        public decimal Pending;
        public decimal RuleDiscount;
        public decimal ManualDiscount;
        public decimal Amount;
        public decimal TotalDiscount => RuleDiscount + ManualDiscount;
        public string? Label;
        /// <summary>Decisión de regularización (mensualidad vencida) y mora a cobrar en este recibo.</summary>
        public RegularizeDebtDto? Regularization;
        public decimal LateFee;
    }

    private sealed class ProductPlan
    {
        public CheckoutProductItemDto Item = null!;
        public Product Product = null!;
        public Student? Student;
        public string? StudentName;
        public decimal Base;
        public decimal Discount;
        public decimal Total => Base - Discount;
        public string? Label;
    }

    private sealed class CheckoutPlan
    {
        public List<DebtPlan> Debts = new();
        public List<ProductPlan> Products = new();
        public string PayerName = "";
        public string? PayerDocument;
        public Guid? GuardianId;
        public DiscountRule? Promotion;
    }

    private static string? JoinLabels(params string?[] parts)
    {
        var list = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return list.Count == 0 ? null : string.Join(" · ", list);
    }

    /// <param name="forPreview">Vista previa: tolera la decisión de mora aún incompleta (se exige al cobrar).</param>
    private async Task<CheckoutPlan> BuildPlanAsync(Guid academyId, CheckoutDto dto, bool forPreview = false)
    {
        var today = AcademyClock.Today;
        var plan = new CheckoutPlan();

        // ── Reglas de descuento vigentes ──
        var earlyRules = await _db.DiscountRules
            .Where(r => r.AcademyId == academyId && r.Kind == DiscountKind.EarlyPayment && r.IsActive)
            .ToListAsync();
        earlyRules = earlyRules.Where(r => r.IsValidOn(today)).ToList();
        if (dto.PromotionId.HasValue)
        {
            plan.Promotion = await _db.DiscountRules.FirstOrDefaultAsync(r =>
                    r.Id == dto.PromotionId && r.AcademyId == academyId && r.Kind == DiscountKind.Promotion)
                ?? throw new Exception("La promoción elegida no existe.");
            if (!plan.Promotion.IsValidOn(today)) throw new Exception($"La promoción \"{plan.Promotion.Name}\" no está vigente hoy.");
        }

        // ── Deudas ──
        var debtIds = dto.Debts.Select(d => d.PaymentRecordId).ToList();
        var records = await _db.PaymentRecords
            .Include(p => p.Student).ThenInclude(s => s.Guardian)
            .Where(p => p.AcademyId == academyId && debtIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
        if (records.Count != debtIds.Count)
            throw new Exception("Una de las deudas seleccionadas ya no existe. Actualiza la pantalla.");

        foreach (var item in dto.Debts)
        {
            var record = records[item.PaymentRecordId];
            var studentName = $"{record.Student.FirstName} {record.Student.LastName}".Trim();
            if (record.IsPaid) throw new Exception($"{record.Description} de {studentName} ya está pagada.");

            // Mensualidad vencida: se puede cobrar, pero es obligatorio decidir la mora y dejar observación.
            RegularizeDebtDto? regularization = null;
            decimal lateFee = 0;
            if (FinancesService.RequiresRegularization(record))
            {
                regularization = item.Regularization;
                if (regularization == null && !forPreview) FinancesService.EnsureRegularized(record); // explica qué falta
                if (regularization != null)
                {
                    if (!forPreview && (regularization.Note?.Trim().Length ?? 0) < 5)
                        throw new Exception($"{record.Description} ({studentName}): escribe la observación de la regularización.");
                    if (regularization.ChargeLateFee)
                    {
                        lateFee = regularization.LateFeeAmount ?? (await _finances.GetConfigAsync(academyId)).LateFeeAmount;
                        if (lateFee <= 0 && !forPreview) throw new Exception($"{record.Description} ({studentName}): indica el monto de la mora.");
                        lateFee = Math.Max(0, lateFee);
                        regularization = new RegularizeDebtDto { ChargeLateFee = true, LateFeeAmount = lateFee, Note = regularization.Note };
                    }
                }
            }
            var pending = record.Amount - record.AmountPaid;
            if (item.Discount > pending + 0.01m)
                throw new Exception($"El descuento de {record.Description} ({studentName}) supera el saldo pendiente.");

            // Descuento automático (no acumulable: se aplica el mayor) solo para mensualidades.
            DiscountRule? best = null;
            decimal bestValue = 0;
            if (record.Type == PaymentType.MonthlyFee)
            {
                var candidates = earlyRules
                    .Where(r => today <= record.DueDate.Date.AddDays(-(r.DaysBeforeDue ?? 0)))
                    .ToList();
                if (plan.Promotion != null && plan.Promotion.AppliesToMonthlyFees) candidates.Add(plan.Promotion);
                foreach (var r in candidates.Where(r => r.CategoryId == null || r.CategoryId == record.Student.CategoryId))
                {
                    var v = r.DiscountFor(pending - item.Discount);
                    if (v > bestValue) { best = r; bestValue = v; }
                }
            }

            var netFull = pending - item.Discount - bestValue;
            decimal amount;
            if (!item.Amount.HasValue || item.Amount.Value >= netFull - 0.01m)
            {
                // Cancela la deuda: aplica el descuento automático.
                if (item.Amount.HasValue && item.Amount.Value > pending + 0.01m)
                    throw new Exception($"El monto de {record.Description} ({studentName}) supera el saldo pendiente ({pending:N2}).");
                amount = Math.Max(0, netFull);
            }
            else
            {
                // Abono parcial: sin descuento automático.
                best = null;
                bestValue = 0;
                amount = item.Amount.Value;
            }
            if (amount + item.Discount + bestValue <= 0)
                throw new Exception($"Indica el monto a cobrar de {record.Description} ({studentName}).");

            plan.Debts.Add(new DebtPlan
            {
                Record = record,
                StudentName = studentName,
                Pending = pending,
                RuleDiscount = bestValue,
                ManualDiscount = item.Discount,
                Amount = amount,
                Label = JoinLabels(best?.Label, item.Discount > 0 ? $"Descuento en caja S/ {item.Discount:0.00}" : null),
                Regularization = regularization,
                LateFee = lateFee
            });
        }

        // ── Productos ──
        var productIds = dto.Products.Select(p => p.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => p.AcademyId == academyId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
        if (products.Count != productIds.Count)
            throw new Exception("Uno de los productos seleccionados ya no existe.");
        foreach (var group in dto.Products.GroupBy(p => p.ProductId))
        {
            var product = products[group.Key];
            var qty = group.Sum(p => p.Quantity);
            if (product.Stock < qty)
                throw new Exception($"Stock insuficiente de {product.Name}. Disponible: {product.Stock}.");
        }
        var productStudentIds = dto.Products.Where(p => p.StudentId.HasValue).Select(p => p.StudentId!.Value).Distinct().ToList();
        var productStudents = await _db.Students.Include(s => s.Guardian)
            .Where(s => s.AcademyId == academyId && productStudentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);
        if (productStudents.Count != productStudentIds.Count)
            throw new Exception("Uno de los alumnos de la venta no es válido.");

        foreach (var item in dto.Products)
        {
            var product = products[item.ProductId];
            var student = item.StudentId.HasValue ? productStudents[item.StudentId.Value] : null;
            var baseTotal = product.Price * item.Quantity;
            if (item.IsGift)
            {
                plan.Products.Add(new ProductPlan { Item = item, Product = product, Student = student, StudentName = NameOf(student), Base = baseTotal, Discount = baseTotal, Label = "Obsequio" });
                continue;
            }
            if (item.Discount > baseTotal) throw new Exception($"El descuento de {product.Name} supera su precio.");
            var promo = plan.Promotion != null && plan.Promotion.AppliesToProducts &&
                        (plan.Promotion.CategoryId == null || plan.Promotion.CategoryId == student?.CategoryId)
                ? plan.Promotion : null;
            // Promoción por unidad (monto fijo) o sobre el total (porcentaje).
            var promoValue = promo == null ? 0m
                : promo.ValueType == DiscountValueType.Amount ? Math.Min(promo.Value * item.Quantity, baseTotal) : promo.DiscountFor(baseTotal);
            var discount = Math.Min(baseTotal, promoValue + item.Discount);
            plan.Products.Add(new ProductPlan
            {
                Item = item, Product = product, Student = student, StudentName = NameOf(student),
                Base = baseTotal, Discount = discount,
                Label = JoinLabels(promoValue > 0 ? promo!.Label : null, item.Discount > 0 ? $"Descuento S/ {item.Discount:0.00}" : null)
            });
        }

        // ── Pagador ──
        if (dto.GuardianId.HasValue && dto.GuardianId != Guid.Empty)
        {
            var guardian = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.GuardianId && u.AcademyId == academyId)
                ?? throw new Exception("El apoderado indicado no es válido.");
            plan.GuardianId = guardian.Id;
            plan.PayerName = $"{guardian.FirstName} {guardian.LastName}".Trim();
            plan.PayerDocument = guardian.DocumentNumber;
        }
        else
        {
            var first = records.Values.Select(r => r.Student).FirstOrDefault() ?? productStudents.Values.FirstOrDefault();
            (plan.PayerName, plan.PayerDocument, plan.GuardianId) = ReceiptRecorder.PayerOf(first);
        }
        if (!string.IsNullOrWhiteSpace(dto.PayerName)) plan.PayerName = dto.PayerName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PayerDocument)) plan.PayerDocument = dto.PayerDocument.Trim();
        await EnsureStudentsInScopeAsync(plan);
        return plan;
    }

    /// <summary>Quien está limitado a ciertas sedes solo cobra a alumnos de esas sedes.</summary>
    private async Task EnsureStudentsInScopeAsync(CheckoutPlan plan)
    {
        var headquarters = await _scope.StaffHeadquartersAsync();
        if (headquarters == null) return;
        var ids = plan.Debts.Select(d => d.Record.StudentId)
            .Concat(plan.Products.Where(p => p.Student != null).Select(p => p.Student!.Id))
            .Distinct().ToList();
        if (await _db.Students.AsNoTracking().AnyAsync(s => ids.Contains(s.Id) && !headquarters.Contains(s.HeadquarterId)))
            throw new UnauthorizedAccessException("Este cobro incluye alumnos de una sede que no tienes asignada.");
    }

    private static string? NameOf(Student? s) => s == null ? null : $"{s.FirstName} {s.LastName}".Trim();

    private Task<ReceiptDto> CheckoutOnceAsync(Guid academyId, CheckoutDto dto, PaymentMethod method, string? requestId) =>
        _db.InTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;
            // El dinero entra hoy: el periodo de hoy debe estar abierto (aunque la deuda sea de un mes cerrado).
            await _periods.EnsureOpenAsync(academyId, AcademyClock.Today, "registrar el cobro");

            var plan = await BuildPlanAsync(academyId, dto);

            // ── Número de recibo (si algo falla, la transacción lo revierte: sin saltos) ──
            var number = await _finances.ReserveReceiptNumberAsync(academyId);
            var receipt = await _recorder.CreateAsync(academyId, number, now, plan.PayerName, plan.PayerDocument, plan.GuardianId,
                method, dto.OperationNumber, dto.VoucherUrl, dto.Notes, dto.Channel ?? "web");
            receipt.ClientRequestId = requestId;

            foreach (var d in plan.Debts)
            {
                var record = d.Record;
                PaymentRecord? lateFeeRecord = null;
                if (d.Regularization != null)
                    lateFeeRecord = await _finances.ApplyRegularizationAsync(academyId, record, d.Regularization, _recorder.CurrentUserId);
                if (d.TotalDiscount > 0)
                {
                    record.Amount -= d.TotalDiscount;
                    record.DiscountAmount += d.TotalDiscount;
                    record.DiscountDetail = ReceiptRecorder.Truncate(JoinLabels(record.DiscountDetail, d.Label), 200);
                }

                PaymentInstallment? installment = null;
                if (d.Amount > 0)
                {
                    installment = new PaymentInstallment
                    {
                        PaymentRecordId = record.Id,
                        AmountPaid = d.Amount,
                        PaidAt = now,
                        Method = method,
                        OperationNumber = dto.OperationNumber,
                        VoucherUrl = dto.VoucherUrl,
                        Notes = dto.Notes,
                        ReceiptNumber = number
                    };
                    _db.PaymentInstallments.Add(installment);
                    record.AmountPaid += d.Amount;
                }
                if (record.AmountPaid >= record.Amount - 0.01m)
                {
                    record.AmountPaid = record.Amount;
                    record.IsPaid = true;
                    record.PaidDate = now;
                }
                record.UpdatedAt = now;

                ReceiptRecorder.AddLine(receipt, new ReceiptLine
                {
                    Type = ReceiptLineType.Debt,
                    Description = record.Description,
                    StudentId = record.StudentId,
                    StudentName = d.StudentName,
                    Quantity = 1,
                    UnitPrice = d.Amount + d.TotalDiscount,
                    Discount = d.TotalDiscount,
                    DiscountLabel = ReceiptRecorder.Truncate(d.Label, 150),
                    Total = d.Amount,
                    PaymentRecordId = record.Id,
                    PaymentInstallmentId = installment?.Id
                });

                if (lateFeeRecord != null)
                {
                    // La mora se cobra en este mismo recibo, como ítem aparte (ingreso por mora en reportes).
                    var feeInstallment = new PaymentInstallment
                    {
                        PaymentRecordId = lateFeeRecord.Id,
                        AmountPaid = lateFeeRecord.Amount,
                        PaidAt = now,
                        Method = method,
                        OperationNumber = dto.OperationNumber,
                        VoucherUrl = dto.VoucherUrl,
                        Notes = record.RegularizationNote,
                        ReceiptNumber = number
                    };
                    _db.PaymentInstallments.Add(feeInstallment);
                    lateFeeRecord.AmountPaid = lateFeeRecord.Amount;
                    lateFeeRecord.IsPaid = true;
                    lateFeeRecord.PaidDate = now;
                    ReceiptRecorder.AddLine(receipt, new ReceiptLine
                    {
                        Type = ReceiptLineType.Debt,
                        Description = lateFeeRecord.Description,
                        StudentId = record.StudentId,
                        StudentName = d.StudentName,
                        Quantity = 1,
                        UnitPrice = lateFeeRecord.Amount,
                        Total = lateFeeRecord.Amount,
                        DiscountLabel = "Pago fuera de fecha",
                        PaymentRecordId = lateFeeRecord.Id,
                        PaymentInstallmentId = feeInstallment.Id
                    });
                }
            }

            foreach (var p in plan.Products)
            {
                p.Product.Stock -= p.Item.Quantity;
                var sale = new ProductSale
                {
                    AcademyId = academyId,
                    ProductId = p.Product.Id,
                    StudentId = p.Student?.Id,
                    Quantity = p.Item.Quantity,
                    UnitPrice = p.Product.Price,
                    TotalPrice = p.Total,
                    IsGift = p.Item.IsGift,
                    DiscountAmount = p.Discount,
                    SaleDate = now,
                    ReceiptNumber = number,
                    Notes = dto.Notes
                };
                _db.ProductSales.Add(sale);

                ReceiptRecorder.AddLine(receipt, new ReceiptLine
                {
                    Type = ReceiptLineType.Product,
                    Description = p.Item.IsGift ? $"{p.Product.Name} (obsequio)" : p.Product.Name,
                    StudentId = p.Student?.Id,
                    StudentName = p.StudentName,
                    Quantity = p.Item.Quantity,
                    UnitPrice = p.Product.Price,
                    Discount = p.Discount,
                    DiscountLabel = ReceiptRecorder.Truncate(p.Label, 150),
                    Total = p.Total,
                    ProductSaleId = sale.Id,
                    ProductId = p.Product.Id
                });
            }

            if (method == PaymentMethod.Mixed) AddMixedPayments(receipt, dto.Payments!);

            _audit.Add(academyId, "finances.receipt.create", nameof(Receipt), receipt.Id,
                new { number, receipt.Total, receipt.DiscountTotal, promotion = plan.Promotion?.Name, method = method.ToString(),
                      dto.OperationNumber, payments = dto.Payments?.Select(p => new { p.Method, p.Amount, p.OperationNumber }),
                      lines = receipt.Lines.Count, payer = plan.PayerName });

            await _db.SaveChangesAsync();
            return Map(receipt);
        });

    // ─────────────────────────────────────────────────────────────
    // Anulación (revierte todos los ítems)
    // ─────────────────────────────────────────────────────────────
    public async Task<ReceiptDto> VoidAsync(Guid academyId, Guid receiptId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new Exception("Indica el motivo de la anulación (mínimo 5 caracteres).");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await _db.InTransactionAsync(async () =>
                {
                    var receipt = await _db.Receipts.Include(r => r.Lines)
                        .FirstOrDefaultAsync(r => r.Id == receiptId && r.AcademyId == academyId)
                        ?? throw new KeyNotFoundException("Comprobante no encontrado.");
                    await VoidLoadedAsync(academyId, receipt, reason.Trim());
                    await _db.SaveChangesAsync();
                    return Map(receipt);
                });
            }
            catch (DbUpdateConcurrencyException) when (attempt < 4)
            {
                _db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Revierte los ítems de un comprobante ya cargado (con Lines). No guarda.</summary>
    internal async Task VoidLoadedAsync(Guid academyId, Receipt receipt, string reason)
    {
        if (receipt.Status == ReceiptStatus.Voided) throw new Exception($"El recibo N° {receipt.Number} ya está anulado.");
        await _periods.EnsureOpenAsync(academyId, AcademyClock.ToLocalDate(receipt.IssuedAt), "anular el comprobante");

        foreach (var line in receipt.Lines)
        {
            if (line.Type == ReceiptLineType.Debt && line.PaymentRecordId.HasValue)
            {
                var record = await _db.PaymentRecords.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.Id == line.PaymentRecordId && p.AcademyId == academyId);
                if (record == null) continue;
                if (line.PaymentInstallmentId.HasValue)
                {
                    var installment = await _db.PaymentInstallments.FirstOrDefaultAsync(i => i.Id == line.PaymentInstallmentId);
                    if (installment != null)
                    {
                        record.AmountPaid -= installment.AmountPaid;
                        _db.PaymentInstallments.Remove(installment);
                    }
                }
                if (line.Discount > 0)
                {
                    record.Amount += line.Discount;
                    record.DiscountAmount = Math.Max(0, record.DiscountAmount - line.Discount);
                    if (!string.IsNullOrEmpty(line.DiscountLabel) && record.DiscountDetail != null)
                    {
                        var detail = record.DiscountDetail.Replace(" · " + line.DiscountLabel, "").Replace(line.DiscountLabel, "").Trim(' ', '·');
                        record.DiscountDetail = string.IsNullOrWhiteSpace(detail) ? null : detail;
                    }
                }
                record.AmountPaid = Math.Max(0, record.AmountPaid);
                if (record.AmountPaid < record.Amount - 0.01m)
                {
                    record.IsPaid = false;
                    record.PaidDate = null;
                }
                record.UpdatedAt = DateTime.UtcNow;
            }
            else if (line.Type == ReceiptLineType.Product && line.ProductSaleId.HasValue)
            {
                var sale = await _db.ProductSales.FirstOrDefaultAsync(s => s.Id == line.ProductSaleId && s.AcademyId == academyId);
                if (sale == null) continue; // ya anulada
                sale.IsDeleted = true;
                sale.DeletedAt = DateTime.UtcNow;
                var product = await _db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == sale.ProductId);
                if (product != null) product.Stock += sale.Quantity;
            }
        }

        // Si el pago venía de un comprobante enviado por la familia, se informa en la app.
        var proofs = await _db.PaymentProofs
            .Where(p => p.AcademyId == academyId && p.ReceiptNumber == receipt.Number && p.Status == PaymentProofStatus.Approved)
            .ToListAsync();
        foreach (var proof in proofs)
        {
            proof.Status = PaymentProofStatus.Rejected;
            proof.RejectionReason = ReceiptRecorder.Truncate($"Recibo N° {receipt.Number} anulado: {reason}", 500);
            proof.UpdatedAt = DateTime.UtcNow;
        }

        // Si el recibo estaba en una rendición de caja aún no recibida, sale de ella.
        await CashService.DetachVoidedAsync(_db, receipt);

        receipt.Status = ReceiptStatus.Voided;
        receipt.VoidedAt = DateTime.UtcNow;
        receipt.VoidedByUserId = _recorder.CurrentUserId;
        receipt.VoidReason = ReceiptRecorder.Truncate(reason, 500);

        _audit.Add(academyId, "finances.receipt.void", nameof(Receipt), receipt.Id,
            new { receipt.Number, receipt.Total, reason });
    }

    // ─────────────────────────────────────────────────────────────
    // Consulta
    // ─────────────────────────────────────────────────────────────
    public async Task<List<ReceiptDto>> ListAsync(Guid academyId, DateTime? from, DateTime? to, string? search, Guid? studentId)
    {
        var q = _db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments).Where(r => r.AcademyId == academyId);
        if (from.HasValue) { var start = LocalDayStartUtc(from.Value); q = q.Where(r => r.IssuedAt >= start); }
        if (to.HasValue) { var end = LocalDayStartUtc(to.Value.AddDays(1)); q = q.Where(r => r.IssuedAt < end); }
        if (studentId.HasValue) q = q.Where(r => r.Lines.Any(l => l.StudentId == studentId));
        var headquarters = await _scope.StaffHeadquartersAsync();
        if (headquarters != null)
            q = q.Where(r => !r.Lines.Any(l => l.StudentId != null)
                || r.Lines.Any(l => _db.Students.Any(s => s.Id == l.StudentId && headquarters.Contains(s.HeadquarterId))));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            q = q.Where(r => EF.Functions.ILike(r.Number, term) || EF.Functions.ILike(r.PayerName, term) ||
                             r.Lines.Any(l => l.StudentName != null && EF.Functions.ILike(l.StudentName, term)));
        }
        var list = await q.OrderByDescending(r => r.IssuedAt).Take(300).ToListAsync();
        return list.Select(Map).ToList();
    }

    /// <summary>Oculta un recibo de otra sede como si no existiera. Las ventas al público no pertenecen a ninguna.</summary>
    public async Task EnsureVisibleAsync(ReceiptDto receipt)
    {
        var headquarters = await _scope.StaffHeadquartersAsync();
        if (headquarters == null) return;
        var studentIds = receipt.Lines.Where(l => l.StudentId != null).Select(l => l.StudentId!.Value).Distinct().ToList();
        if (studentIds.Count == 0) return;
        if (!await _db.Students.AsNoTracking().AnyAsync(s => studentIds.Contains(s.Id) && headquarters.Contains(s.HeadquarterId)))
            throw new KeyNotFoundException("Comprobante no encontrado.");
    }

    public async Task<ReceiptDto> GetAsync(Guid academyId, Guid id)
    {
        var receipt = await _db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.Id == id && r.AcademyId == academyId)
            ?? throw new KeyNotFoundException("Comprobante no encontrado.");
        return Map(receipt);
    }

    /// <summary>
    /// Busca por número. Los recibos emitidos antes de esta versión no tienen cabecera:
    /// se reconstruyen desde sus abonos y ventas para poder reimprimirlos completos.
    /// </summary>
    public async Task<ReceiptDto> GetByNumberAsync(Guid academyId, string number)
    {
        number = (number ?? "").Trim();
        if (number.All(char.IsDigit) && number.Length is > 0 and < 6) number = number.PadLeft(6, '0');

        var receipt = await _db.Receipts.AsNoTracking().Include(r => r.Lines).Include(r => r.Payments)
            .FirstOrDefaultAsync(r => r.AcademyId == academyId && r.Number == number);
        if (receipt != null) return Map(receipt);

        var installments = await _db.PaymentInstallments.AsNoTracking()
            .Include(i => i.PaymentRecord).ThenInclude(p => p.Student).ThenInclude(s => s.Guardian)
            .Where(i => i.ReceiptNumber == number && i.PaymentRecord.AcademyId == academyId)
            .ToListAsync();
        var sales = await _db.ProductSales.AsNoTracking()
            .Include(s => s.Product).Include(s => s.Student).ThenInclude(s => s!.Guardian)
            .Where(s => s.AcademyId == academyId && s.ReceiptNumber == number)
            .ToListAsync();
        if (installments.Count == 0 && sales.Count == 0)
            throw new KeyNotFoundException($"No se encontró el recibo N° {number}.");

        var firstStudent = installments.Select(i => i.PaymentRecord.Student).FirstOrDefault() ?? sales.Select(s => s.Student).FirstOrDefault();
        var (payer, document, guardianId) = ReceiptRecorder.PayerOf(firstStudent);
        var lines = installments.Select(i => new ReceiptLineDto
        {
            Type = "Debt",
            Description = i.PaymentRecord.Description,
            StudentId = i.PaymentRecord.StudentId,
            StudentName = $"{i.PaymentRecord.Student.FirstName} {i.PaymentRecord.Student.LastName}".Trim(),
            Quantity = 1,
            UnitPrice = i.AmountPaid,
            Total = i.AmountPaid
        }).Concat(sales.Select(s => new ReceiptLineDto
        {
            Type = "Product",
            Description = s.Product?.Name ?? "Producto",
            StudentId = s.StudentId,
            StudentName = s.Student != null ? $"{s.Student.FirstName} {s.Student.LastName}".Trim() : null,
            Quantity = s.Quantity,
            UnitPrice = s.UnitPrice,
            Discount = s.DiscountAmount,
            Total = s.TotalPrice
        })).ToList();
        var firstInstallment = installments.FirstOrDefault();
        return new ReceiptDto
        {
            Number = number,
            IssuedAt = firstInstallment?.PaidAt ?? sales.First().SaleDate,
            PayerName = payer,
            PayerDocument = document,
            GuardianId = guardianId,
            Method = firstInstallment?.Method.ToString() ?? "Cash",
            OperationNumber = firstInstallment?.OperationNumber,
            Notes = firstInstallment?.Notes ?? sales.FirstOrDefault()?.Notes,
            Subtotal = lines.Sum(l => l.UnitPrice * l.Quantity),
            DiscountTotal = lines.Sum(l => l.Discount),
            Total = lines.Sum(l => l.Total),
            Status = "Issued",
            IsLegacy = true,
            Lines = lines
        };
    }

    private static DateTime LocalDayStartUtc(DateTime localDate) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified), AcademyClock.Zone);

    internal static ReceiptDto Map(Receipt r) => new()
    {
        Id = r.Id,
        Number = r.Number,
        IssuedAt = r.IssuedAt,
        PayerName = r.PayerName,
        PayerDocument = r.PayerDocument,
        GuardianId = r.GuardianId,
        Method = r.Method.ToString(),
        OperationNumber = r.OperationNumber,
        VoucherUrl = r.VoucherUrl,
        Notes = r.Notes,
        Subtotal = r.Subtotal,
        DiscountTotal = r.DiscountTotal,
        Total = r.Total,
        Channel = r.Channel,
        CreatedByName = r.CreatedByName,
        Status = r.Status.ToString(),
        VoidedAt = r.VoidedAt,
        VoidReason = r.VoidReason,
        Lines = r.Lines.OrderBy(l => l.Position).Select(l => new ReceiptLineDto
        {
            Type = l.Type.ToString(),
            Description = l.Description,
            StudentId = l.StudentId,
            StudentName = l.StudentName,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            Discount = l.Discount,
            DiscountLabel = l.DiscountLabel,
            Total = l.Total,
            PaymentRecordId = l.PaymentRecordId,
            ProductId = l.ProductId
        }).ToList(),
        CashHandoverId = r.CashHandoverId,
        Payments = r.Payments.OrderBy(p => p.Position).Select(p => new ReceiptPaymentDto
        {
            Method = p.Method.ToString(),
            Amount = p.Amount,
            OperationNumber = p.OperationNumber,
            VoucherUrl = p.VoucherUrl
        }).ToList()
    };
}
