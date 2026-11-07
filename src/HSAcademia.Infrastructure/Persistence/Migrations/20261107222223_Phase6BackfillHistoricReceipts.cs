using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSAcademia.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Convierte los cobros anteriores a los comprobantes con varios ítems en recibos del historial.
    /// - Agrupa abonos (payment_installments) y ventas (product_sales) por academia + número de recibo:
    ///   un cobro combinado (mensualidad + producto) queda como UN recibo con dos ítems.
    /// - Solo crea registros (receipts / receipt_lines); no modifica pagos, deudas ni ventas.
    /// - Canal "historico": no entra al cuadre de caja como efectivo pendiente (ya se entregó).
    /// - Idempotente: omite números que ya tienen recibo. Down() elimina solo los recibos históricos.
    /// </summary>
    public partial class Phase6BackfillHistoricReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TEMP TABLE hist_items ON COMMIT DROP AS
SELECT pr.academy_id,
       i.""ReceiptNumber""            AS number,
       i.paid_at                      AS at,
       1                              AS line_type,
       pr.description                 AS description,
       pr.student_id                  AS student_id,
       pr.id                          AS payment_record_id,
       i.id                           AS installment_id,
       NULL::uuid                     AS sale_id,
       NULL::uuid                     AS product_id,
       1                              AS quantity,
       i.amount_paid                  AS unit_price,
       0::numeric                     AS discount,
       i.amount_paid                  AS total,
       i.method                       AS method,
       i.operation_number             AS operation_number,
       i.voucher_url                  AS voucher_url,
       i.notes                        AS notes
FROM payment_installments i
JOIN payment_records pr ON pr.id = i.payment_record_id
WHERE i.""ReceiptNumber"" IS NOT NULL AND i.""ReceiptNumber"" <> '' AND NOT pr.is_deleted
UNION ALL
SELECT s.academy_id,
       s.receipt_number,
       s.sale_date,
       2,
       p.name || CASE WHEN s.""IsGift"" THEN ' (obsequio)' ELSE '' END,
       s.student_id,
       NULL::uuid,
       NULL::uuid,
       s.id,
       s.product_id,
       s.quantity,
       s.unit_price,
       s.""DiscountAmount"",
       s.total_price,
       NULL::integer,
       NULL,
       NULL,
       s.""Notes""
FROM product_sales s
JOIN products p ON p.id = s.product_id
WHERE s.receipt_number IS NOT NULL AND s.receipt_number <> '' AND NOT s.is_deleted;

-- Solo números que aún no tienen recibo
DELETE FROM hist_items h
WHERE EXISTS (SELECT 1 FROM receipts r WHERE r.academy_id = h.academy_id AND r.number = h.number);

CREATE TEMP TABLE hist_headers ON COMMIT DROP AS
SELECT gen_random_uuid() AS id, g.*
FROM (
    SELECT DISTINCT ON (h.academy_id, h.number)
           h.academy_id,
           h.number,
           h.at                                           AS issued_at,
           h.student_id                                   AS first_student_id
    FROM hist_items h
    ORDER BY h.academy_id, h.number, h.at, h.line_type
) g;

INSERT INTO receipts (id, academy_id, number, issued_at, payer_name, payer_document, guardian_id, method,
                      operation_number, voucher_url, notes, subtotal, discount_total, total, channel,
                      status, created_at)
SELECT hh.id,
       hh.academy_id,
       hh.number,
       hh.issued_at,
       LEFT(COALESCE(NULLIF(TRIM(u.first_name || ' ' || u.last_name), ''),
                     NULLIF(TRIM(st.""FirstName"" || ' ' || st.""LastName""), ''),
                     'Público general'), 200),
       LEFT(COALESCE(u.""DocumentNumber"", st.""DocumentNumber""), 20),
       u.id,
       COALESCE((SELECT h.method FROM hist_items h
                 WHERE h.academy_id = hh.academy_id AND h.number = hh.number AND h.method IS NOT NULL
                 ORDER BY h.at LIMIT 1), 1),
       (SELECT LEFT(h.operation_number, 100) FROM hist_items h
        WHERE h.academy_id = hh.academy_id AND h.number = hh.number AND h.operation_number IS NOT NULL
        ORDER BY h.at LIMIT 1),
       (SELECT LEFT(h.voucher_url, 500) FROM hist_items h
        WHERE h.academy_id = hh.academy_id AND h.number = hh.number AND h.voucher_url IS NOT NULL
        ORDER BY h.at LIMIT 1),
       (SELECT LEFT(h.notes, 500) FROM hist_items h
        WHERE h.academy_id = hh.academy_id AND h.number = hh.number AND h.notes IS NOT NULL AND h.notes <> ''
        ORDER BY h.at LIMIT 1),
       (SELECT COALESCE(SUM(h.unit_price * h.quantity), 0) FROM hist_items h WHERE h.academy_id = hh.academy_id AND h.number = hh.number),
       (SELECT COALESCE(SUM(h.discount), 0) FROM hist_items h WHERE h.academy_id = hh.academy_id AND h.number = hh.number),
       (SELECT COALESCE(SUM(h.total), 0) FROM hist_items h WHERE h.academy_id = hh.academy_id AND h.number = hh.number),
       'historico',
       1,
       NOW()
FROM hist_headers hh
LEFT JOIN ""Students"" st ON st.""Id"" = hh.first_student_id
LEFT JOIN users u ON u.id = st.""GuardianId"";

INSERT INTO receipt_lines (id, receipt_id, position, type, description, student_id, student_name, quantity,
                           unit_price, discount, total, payment_record_id, payment_installment_id,
                           product_sale_id, product_id)
SELECT gen_random_uuid(),
       hh.id,
       ROW_NUMBER() OVER (PARTITION BY hh.id ORDER BY h.line_type, h.at),
       h.line_type,
       LEFT(COALESCE(h.description, 'Cobro'), 300),
       h.student_id,
       LEFT(NULLIF(TRIM(COALESCE(st.""FirstName"", '') || ' ' || COALESCE(st.""LastName"", '')), ''), 200),
       h.quantity,
       h.unit_price,
       h.discount,
       h.total,
       h.payment_record_id,
       h.installment_id,
       h.sale_id,
       h.product_id
FROM hist_items h
JOIN hist_headers hh ON hh.academy_id = h.academy_id AND hh.number = h.number
LEFT JOIN ""Students"" st ON st.""Id"" = h.student_id;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM receipts WHERE channel = 'historico';");
        }
    }
}
