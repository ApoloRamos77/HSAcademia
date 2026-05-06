-- ==============================================================
-- Script: Backfill ReceiptNumber en installments existentes
-- Asigna números secuenciales a partir de 000044 (los más
-- antiguos reciben el número menor).
-- Ejecutar UNA SOLA VEZ contra la BD de producción.
-- ==============================================================

-- PASO 1: Asignar números de recibo a installments existentes sin número,
--         ordenados por fecha de pago (más antiguo = número menor).
WITH numbered AS (
    SELECT
        id,
        ROW_NUMBER() OVER (ORDER BY paid_at ASC) AS rn
    FROM payment_installments
    WHERE "ReceiptNumber" IS NULL
)
UPDATE payment_installments pi
SET "ReceiptNumber" = LPAD((43 + n.rn)::text, 6, '0')
FROM numbered n
WHERE pi.id = n.id;

-- PASO 2: Actualizar el contador de la academia para que siga
--         desde el último número asignado.
--         (Reemplaza el 0 por el número real de installments que tenías)
UPDATE academy_financial_configs
SET "CurrentReceiptNumber" = (
    SELECT COALESCE(MAX(CAST("ReceiptNumber" AS INTEGER)), 43)
    FROM payment_installments
    WHERE "ReceiptNumber" IS NOT NULL
);

-- VERIFICACIÓN: Ver los recibos asignados
SELECT
    pi.id,
    pi.paid_at,
    pi.amount_paid,
    pi."ReceiptNumber",
    pr.description as concepto,
    s."FirstName" || ' ' || s."LastName" as alumno
FROM payment_installments pi
JOIN payment_records pr ON pi.payment_record_id = pr.id
JOIN "Students" s ON pr.student_id = s."Id"
ORDER BY pi.paid_at ASC;
