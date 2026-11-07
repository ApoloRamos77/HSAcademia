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
    private readonly AppDbContext _db;
    private readonly FinancesService _finances;
    private readonly FinancialPeriodGuard _periods;
    private readonly AuditService _audit;
    private readonly ReceiptRecorder _recorder;

    public ReceiptService(AppDbContext db, FinancesService finances, FinancialPeriodGuard periods, AuditService audit, ReceiptRecorder recorder)
    {
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
    public async Task<ReceiptDto> CheckoutAsync(Guid academyId, CheckoutDto dto, bool canChargeDebts, bool canSellProducts)
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
            if (d.Amount + d.Discount <= 0) throw new Exception("Cada deuda del comprobante debe tener un monto a cobrar.");
        }
        foreach (var p in dto.Products)
        {
            if (p.Quantity <= 0 || p.Quantity > 999) throw new Exception("La cantidad de cada producto debe ser mayor a cero.");
            if (p.Discount < 0) throw new Exception("El descuento no puede ser negativo.");
        }

        if (!Enum.TryParse<PaymentMethod>(dto.Method, true, out var method)) method = PaymentMethod.Cash;
        var isDigital = method is PaymentMethod.Yape or PaymentMethod.Plin or PaymentMethod.BankTransfer;
        if (isDigital && string.IsNullOrWhiteSpace(dto.OperationNumber) && string.IsNullOrWhiteSpace(dto.VoucherUrl))
            throw new Exception("Para pagos con Yape, Plin o transferencia indica el número de operación.");

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

    private async Task<ReceiptDto?> FindByRequestIdAsync(Guid academyId, string requestId)
    {
        var r = await _db.Receipts.AsNoTracking().Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.AcademyId == academyId && x.ClientRequestId == requestId);
        return r == null ? null : Map(r);
    }

    private Task<ReceiptDto> CheckoutOnceAsync(Guid academyId, CheckoutDto dto, PaymentMethod method, string? requestId) =>
        _db.InTransactionAsync(async () =>
        {
            var now = DateTime.UtcNow;
            // El dinero entra hoy: el periodo de hoy debe estar abierto (aunque la deuda sea de un mes cerrado).
            await _periods.EnsureOpenAsync(academyId, AcademyClock.Today, "registrar el cobro");

            // ── Cargar y validar todo antes de reservar el número ──
            var debtIds = dto.Debts.Select(d => d.PaymentRecordId).ToList();
            var records = await _db.PaymentRecords
                .Include(p => p.Student).ThenInclude(s => s.Guardian)
                .Where(p => p.AcademyId == academyId && debtIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
            if (records.Count != debtIds.Count)
                throw new Exception("Una de las deudas seleccionadas ya no existe. Actualiza la pantalla.");

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

            // ── Pagador ──
            string payerName;
            string? payerDocument;
            Guid? guardianId;
            if (dto.GuardianId.HasValue && dto.GuardianId != Guid.Empty)
            {
                var guardian = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.GuardianId && u.AcademyId == academyId)
                    ?? throw new Exception("El apoderado indicado no es válido.");
                guardianId = guardian.Id;
                payerName = $"{guardian.FirstName} {guardian.LastName}".Trim();
                payerDocument = guardian.DocumentNumber;
            }
            else
            {
                var first = records.Values.Select(r => r.Student).FirstOrDefault() ?? productStudents.Values.FirstOrDefault();
                (payerName, payerDocument, guardianId) = ReceiptRecorder.PayerOf(first);
            }
            if (!string.IsNullOrWhiteSpace(dto.PayerName)) payerName = dto.PayerName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.PayerDocument)) payerDocument = dto.PayerDocument.Trim();

            // ── Número de recibo (si algo falla, la transacción lo revierte: sin saltos) ──
            var number = await _finances.ReserveReceiptNumberAsync(academyId);
            var receipt = await _recorder.CreateAsync(academyId, number, now, payerName, payerDocument, guardianId,
                method, dto.OperationNumber, dto.VoucherUrl, dto.Notes, dto.Channel ?? "web");
            receipt.ClientRequestId = requestId;

            // ── Deudas ──
            foreach (var item in dto.Debts)
            {
                var record = records[item.PaymentRecordId];
                var studentName = $"{record.Student.FirstName} {record.Student.LastName}".Trim();
                if (record.IsPaid) throw new Exception($"{record.Description} de {studentName} ya está pagada.");

                var pending = record.Amount - record.AmountPaid;
                if (item.Discount > 0)
                {
                    if (item.Discount > pending + 0.01m)
                        throw new Exception($"El descuento de {record.Description} ({studentName}) supera el saldo pendiente.");
                    record.Amount -= item.Discount;
                    record.DiscountAmount += item.Discount;
                    pending -= item.Discount;
                }
                if (item.Amount > pending + 0.01m)
                    throw new Exception($"El monto de {record.Description} ({studentName}) supera el saldo pendiente ({pending:N2}).");

                PaymentInstallment? installment = null;
                if (item.Amount > 0)
                {
                    installment = new PaymentInstallment
                    {
                        PaymentRecordId = record.Id,
                        AmountPaid = item.Amount,
                        PaidAt = now,
                        Method = method,
                        OperationNumber = dto.OperationNumber,
                        VoucherUrl = dto.VoucherUrl,
                        Notes = dto.Notes,
                        ReceiptNumber = number
                    };
                    _db.PaymentInstallments.Add(installment);
                    record.AmountPaid += item.Amount;
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
                    StudentName = studentName,
                    Quantity = 1,
                    UnitPrice = item.Amount + item.Discount,
                    Discount = item.Discount,
                    Total = item.Amount,
                    PaymentRecordId = record.Id,
                    PaymentInstallmentId = installment?.Id
                });
            }

            // ── Productos ──
            foreach (var item in dto.Products)
            {
                var product = products[item.ProductId];
                var student = item.StudentId.HasValue ? productStudents[item.StudentId.Value] : null;
                var baseTotal = product.Price * item.Quantity;
                var discount = item.IsGift ? baseTotal : item.Discount;
                if (discount > baseTotal) throw new Exception($"El descuento de {product.Name} supera su precio.");

                product.Stock -= item.Quantity;
                var sale = new ProductSale
                {
                    AcademyId = academyId,
                    ProductId = product.Id,
                    StudentId = student?.Id,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice = baseTotal - discount,
                    IsGift = item.IsGift,
                    DiscountAmount = discount,
                    SaleDate = now,
                    ReceiptNumber = number,
                    Notes = dto.Notes
                };
                _db.ProductSales.Add(sale);

                ReceiptRecorder.AddLine(receipt, new ReceiptLine
                {
                    Type = ReceiptLineType.Product,
                    Description = item.IsGift ? $"{product.Name} (obsequio)" : product.Name,
                    StudentId = student?.Id,
                    StudentName = student != null ? $"{student.FirstName} {student.LastName}".Trim() : null,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    Discount = discount,
                    Total = baseTotal - discount,
                    ProductSaleId = sale.Id,
                    ProductId = product.Id
                });
            }

            _audit.Add(academyId, "finances.receipt.create", nameof(Receipt), receipt.Id,
                new { number, receipt.Total, method = method.ToString(), dto.OperationNumber, lines = receipt.Lines.Count, payerName });

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
        var q = _db.Receipts.AsNoTracking().Include(r => r.Lines).Where(r => r.AcademyId == academyId);
        if (from.HasValue) { var start = LocalDayStartUtc(from.Value); q = q.Where(r => r.IssuedAt >= start); }
        if (to.HasValue) { var end = LocalDayStartUtc(to.Value.AddDays(1)); q = q.Where(r => r.IssuedAt < end); }
        if (studentId.HasValue) q = q.Where(r => r.Lines.Any(l => l.StudentId == studentId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            q = q.Where(r => EF.Functions.ILike(r.Number, term) || EF.Functions.ILike(r.PayerName, term) ||
                             r.Lines.Any(l => l.StudentName != null && EF.Functions.ILike(l.StudentName, term)));
        }
        var list = await q.OrderByDescending(r => r.IssuedAt).Take(300).ToListAsync();
        return list.Select(Map).ToList();
    }

    public async Task<ReceiptDto> GetAsync(Guid academyId, Guid id)
    {
        var receipt = await _db.Receipts.AsNoTracking().Include(r => r.Lines)
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

        var receipt = await _db.Receipts.AsNoTracking().Include(r => r.Lines)
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
            Total = l.Total
        }).ToList()
    };
}
