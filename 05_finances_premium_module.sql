-- ========================================================================================
-- HSAcademia: Módulo de Finanzas Premium
-- Script para PostgreSQL
-- Motor: EF Core (Snake Case Schema)
-- ========================================================================================

-- 1. Tabla de Egresos Generales (Expenses)
CREATE TABLE expenses (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    academy_id character varying(100) NOT NULL,
    type integer NOT NULL, -- Ej: 1=Operativo, 2=Marketing, 3=Equipamiento, 4=Alquiler
    amount numeric(10,2) NOT NULL,
    date date NOT NULL,
    description character varying(500) NOT NULL,
    voucher_url character varying(500),
    registered_by uuid, -- FK a users
    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
    updated_at timestamp with time zone NOT NULL DEFAULT NOW(),
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at timestamp with time zone,
    
    CONSTRAINT pk_expenses PRIMARY KEY (id),
    CONSTRAINT fk_expenses_academies FOREIGN KEY (academy_id) REFERENCES academies (academy_id) ON DELETE RESTRICT,
    CONSTRAINT fk_expenses_users FOREIGN KEY (registered_by) REFERENCES users (id) ON DELETE SET NULL
);
CREATE INDEX ix_expenses_academy_id ON expenses (academy_id);
CREATE INDEX ix_expenses_date ON expenses (date);

-- 2. Tabla de Caja Chica (Petty Cash)
CREATE TABLE petty_cash (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    academy_id character varying(100) NOT NULL,
    headquarter_id uuid, -- Opcional, si la caja chica es por sede
    month integer NOT NULL,
    year integer NOT NULL,
    assigned_amount numeric(10,2) NOT NULL DEFAULT 0,
    current_balance numeric(10,2) NOT NULL DEFAULT 0,
    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
    updated_at timestamp with time zone NOT NULL DEFAULT NOW(),
    
    CONSTRAINT pk_petty_cash PRIMARY KEY (id),
    CONSTRAINT fk_petty_cash_academies FOREIGN KEY (academy_id) REFERENCES academies (academy_id) ON DELETE RESTRICT,
    CONSTRAINT fk_petty_cash_headquarters FOREIGN KEY (headquarter_id) REFERENCES headquarters (id) ON DELETE SET NULL,
    CONSTRAINT uq_petty_cash_period UNIQUE (academy_id, headquarter_id, month, year)
);

-- 3. Tabla de Transacciones de Caja Chica
CREATE TABLE petty_cash_transactions (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    petty_cash_id uuid NOT NULL,
    type integer NOT NULL, -- 1 = Ingreso (Reposición), 2 = Egreso (Gasto)
    amount numeric(10,2) NOT NULL,
    concept character varying(255) NOT NULL,
    date timestamp with time zone NOT NULL DEFAULT NOW(),
    registered_by uuid,
    
    CONSTRAINT pk_petty_cash_transactions PRIMARY KEY (id),
    CONSTRAINT fk_pct_petty_cash FOREIGN KEY (petty_cash_id) REFERENCES petty_cash (id) ON DELETE CASCADE,
    CONSTRAINT fk_pct_users FOREIGN KEY (registered_by) REFERENCES users (id) ON DELETE SET NULL
);
CREATE INDEX ix_petty_cash_transactions_petty_cash_id ON petty_cash_transactions (petty_cash_id);

-- 4. Tabla de Pagos a Personal (Staff Payments / Nómina)
CREATE TABLE staff_payments (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    academy_id character varying(100) NOT NULL,
    staff_id uuid NOT NULL, -- FK a users (Role = Teacher/Admin)
    period_month integer NOT NULL,
    period_year integer NOT NULL,
    base_amount numeric(10,2) NOT NULL DEFAULT 0,
    bonuses numeric(10,2) NOT NULL DEFAULT 0,
    deductions numeric(10,2) NOT NULL DEFAULT 0,
    total_paid numeric(10,2) NOT NULL DEFAULT 0,
    status integer NOT NULL DEFAULT 0, -- 0=Pendiente, 1=Pagado
    paid_at timestamp with time zone,
    notes character varying(500),
    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
    updated_at timestamp with time zone NOT NULL DEFAULT NOW(),
    is_deleted boolean NOT NULL DEFAULT FALSE,
    deleted_at timestamp with time zone,
    
    CONSTRAINT pk_staff_payments PRIMARY KEY (id),
    CONSTRAINT fk_staff_payments_academies FOREIGN KEY (academy_id) REFERENCES academies (academy_id) ON DELETE RESTRICT,
    CONSTRAINT fk_staff_payments_users FOREIGN KEY (staff_id) REFERENCES users (id) ON DELETE RESTRICT,
    CONSTRAINT uq_staff_payments_period UNIQUE (academy_id, staff_id, period_month, period_year)
);

-- 5. Tabla de Metas Financieras (Financial Goals)
CREATE TABLE financial_goals (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    academy_id character varying(100) NOT NULL,
    headquarter_id uuid, -- Opcional, si las metas son por sede
    month integer NOT NULL,
    year integer NOT NULL,
    target_income numeric(10,2) NOT NULL DEFAULT 0,
    target_profit numeric(10,2) NOT NULL DEFAULT 0,
    status integer NOT NULL DEFAULT 0, -- 0=En Progreso, 1=Alcanzado, 2=No Alcanzado
    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
    updated_at timestamp with time zone NOT NULL DEFAULT NOW(),
    
    CONSTRAINT pk_financial_goals PRIMARY KEY (id),
    CONSTRAINT fk_financial_goals_academies FOREIGN KEY (academy_id) REFERENCES academies (academy_id) ON DELETE RESTRICT,
    CONSTRAINT uq_financial_goals_period UNIQUE (academy_id, headquarter_id, month, year)
);

-- 6. Tabla de Cierres Mensuales (Monthly Closings)
CREATE TABLE monthly_closings (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    academy_id character varying(100) NOT NULL,
    month integer NOT NULL,
    year integer NOT NULL,
    total_income numeric(10,2) NOT NULL DEFAULT 0,
    total_expenses numeric(10,2) NOT NULL DEFAULT 0,
    net_profit numeric(10,2) NOT NULL DEFAULT 0,
    petty_cash_balance numeric(10,2) NOT NULL DEFAULT 0,
    status integer NOT NULL DEFAULT 0, -- 0=Abierto, 1=Cerrado (Data Congelada)
    closed_at timestamp with time zone,
    closed_by uuid,
    notes character varying(1000),
    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
    updated_at timestamp with time zone NOT NULL DEFAULT NOW(),
    
    CONSTRAINT pk_monthly_closings PRIMARY KEY (id),
    CONSTRAINT fk_monthly_closings_academies FOREIGN KEY (academy_id) REFERENCES academies (academy_id) ON DELETE RESTRICT,
    CONSTRAINT fk_monthly_closings_users FOREIGN KEY (closed_by) REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT uq_monthly_closings_period UNIQUE (academy_id, month, year)
);
