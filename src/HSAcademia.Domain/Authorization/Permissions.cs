namespace HSAcademia.Domain.Authorization;

/// <summary>Catálogo de permisos por acción. Un rol es un conjunto de estos permisos; los de varios roles se suman.</summary>
public static class Permissions
{
    public const string StudentsView = "students.view";
    public const string StudentsEdit = "students.edit";
    public const string StudentsBilling = "students.billing";
    public const string StudentsPayments = "students.payments";
    public const string StudentsMedical = "students.medical";
    public const string StudentsEvaluate = "students.evaluate";

    public const string AttendanceTake = "attendance.take";
    public const string AttendanceMetrics = "attendance.metrics";
    public const string AttendanceReopen = "attendance.reopen";
    public const string StaffAttendanceMark = "staff_attendance.mark";

    public const string EventsManage = "events.manage";
    public const string AnnouncementsPublish = "announcements.publish";

    public const string CollectionsView = "collections.view";
    public const string CollectionsCollect = "collections.collect";
    public const string CollectionsAdjust = "collections.adjust";
    public const string CollectionsApproveProofs = "collections.approve_proofs";
    public const string CollectionsVoid = "collections.void";
    public const string CollectionsGenerate = "collections.generate";
    public const string CashReceive = "cash.receive";

    public const string FinancesConfig = "finances.config";
    public const string FinancesExpenses = "finances.expenses";
    public const string FinancesStaffPayments = "finances.staff_payments";
    public const string FinancesSummary = "finances.summary";
    public const string FinancesClose = "finances.close";

    public const string StoreSell = "store.sell";
    public const string StoreInventory = "store.inventory";

    public const string ReportsFinance = "reports.finance";
    public const string ReportsStudents = "reports.students";
    public const string ReportsAttendance = "reports.attendance";

    public sealed record Item(string Id, string Label);
    public sealed record Group(string Name, IReadOnlyList<Item> Items);

    /// <summary>Catálogo agrupado por módulo: es la única fuente que usa la pantalla de roles.</summary>
    public static readonly IReadOnlyList<Group> Catalog = new[]
    {
        new Group("Alumnos", new Item[]
        {
            new(StudentsView, "Ver alumnos y apoderados"),
            new(StudentsEdit, "Inscribir y editar alumnos"),
            new(StudentsBilling, "Cambiar becas, tarifas e invitados"),
            new(StudentsPayments, "Ver pagos del alumno"),
            new(StudentsMedical, "Ficha médica y nutrición"),
            new(StudentsEvaluate, "Evaluaciones deportivas")
        }),
        new Group("Asistencia", new Item[]
        {
            new(AttendanceTake, "Tomar lista y escanear QR"),
            new(AttendanceMetrics, "Ver métricas de asistencia"),
            new(AttendanceReopen, "Reabrir listas cerradas"),
            new(StaffAttendanceMark, "Marcar asistencia de entrenadores")
        }),
        new Group("Agenda y comunicación", new Item[]
        {
            new(EventsManage, "Gestionar calendario, torneos y partidos"),
            new(AnnouncementsPublish, "Publicar comunicados")
        }),
        new Group("Cobranza", new Item[]
        {
            new(CollectionsView, "Ver deudas y recibos"),
            new(CollectionsCollect, "Cobrar y rendir su caja"),
            new(CollectionsAdjust, "Recalcular, exonerar y decidir mora"),
            new(CollectionsApproveProofs, "Aprobar comprobantes de las familias"),
            new(CollectionsVoid, "Anular recibos"),
            new(CollectionsGenerate, "Generar mensualidades"),
            new(CashReceive, "Recibir rendiciones de caja")
        }),
        new Group("Finanzas", new Item[]
        {
            new(FinancesConfig, "Configurar cobros y descuentos"),
            new(FinancesExpenses, "Gastos y caja chica"),
            new(FinancesStaffPayments, "Pagos al personal"),
            new(FinancesSummary, "Resumen financiero y metas"),
            new(FinancesClose, "Cerrar y reabrir meses")
        }),
        new Group("Tienda", new Item[]
        {
            new(StoreSell, "Vender productos"),
            new(StoreInventory, "Gestionar inventario")
        }),
        new Group("Reportes", new Item[]
        {
            new(ReportsFinance, "Morosidad y cobros"),
            new(ReportsStudents, "Padrón de alumnos"),
            new(ReportsAttendance, "Asistencia mensual")
        })
    };

    /// <summary>Únicos valores que se aceptan al crear o editar un rol.</summary>
    public static readonly IReadOnlyCollection<string> All = Catalog.SelectMany(g => g.Items).Select(i => i.Id).ToArray();

    /// <summary>Quien atiende cobros o ventas necesita ubicar alumnos de toda su sede, aunque no tenga categorías.</summary>
    public static readonly string[] SedeWideStudentAccess = { CollectionsView, CollectionsCollect, StoreSell };

    // La app 1.0.10 ya instalada pregunta por estos nombres: el servidor los sigue enviando junto a los nuevos.
    private static readonly (string Legacy, string Current)[] LegacyAliases =
    {
        ("view_students", StudentsView),
        ("manage_attendance", AttendanceTake),
        ("manage_staff_attendance", StaffAttendanceMark),
        ("manage_events", EventsManage),
        ("manage_store", StoreSell),
        ("manage_finances", CollectionsCollect)
    };

    public static List<string> WithLegacyAliases(IEnumerable<string> effective)
    {
        var result = effective.Distinct().ToList();
        result.AddRange(LegacyAliases.Where(a => result.Contains(a.Current)).Select(a => a.Legacy));
        return result;
    }
}

/// <summary>Roles que Clubio trae listos para cada academia. No se editan: se clonan para adaptarlos.</summary>
public static class SystemRoles
{
    public sealed record Template(string Key, string Name, string Description, IReadOnlyList<string> Permissions);

    public static readonly IReadOnlyList<Template> All = new[]
    {
        new Template("coordinator", "Coordinador deportivo", "Dirige lo deportivo de sus categorías: alumnos, asistencia y agenda.", new[]
        {
            Permissions.StudentsView, Permissions.StudentsEdit, Permissions.StudentsMedical, Permissions.StudentsEvaluate,
            Permissions.AttendanceTake, Permissions.AttendanceMetrics, Permissions.AttendanceReopen, Permissions.StaffAttendanceMark,
            Permissions.EventsManage, Permissions.AnnouncementsPublish, Permissions.ReportsStudents, Permissions.ReportsAttendance
        }),
        // Debe coincidir con la migración Phase17CoachRole. Sin pagos ni edición de datos del alumno.
        new Template("coach", "Entrenador", "Toma lista, evalúa y ve la ficha médica de sus categorías; gestiona la agenda y publica comunicados. No ve pagos ni edita datos del alumno.", new[]
        {
            Permissions.StudentsView, Permissions.StudentsMedical, Permissions.StudentsEvaluate,
            Permissions.AttendanceTake, Permissions.AttendanceMetrics,
            Permissions.EventsManage, Permissions.AnnouncementsPublish, Permissions.ReportsAttendance
        }),
        new Template("collector", "Cobrador", "Cobra a las familias y rinde su caja.", new[]
        {
            Permissions.CollectionsView, Permissions.CollectionsCollect
        }),
        new Template("accountant", "Contador", "Lleva las finanzas, los cierres y los reportes.", new[]
        {
            Permissions.StudentsBilling, Permissions.StudentsPayments,
            Permissions.CollectionsView, Permissions.CollectionsAdjust, Permissions.CollectionsApproveProofs,
            Permissions.CollectionsVoid, Permissions.CollectionsGenerate, Permissions.CashReceive,
            Permissions.FinancesConfig, Permissions.FinancesExpenses, Permissions.FinancesStaffPayments,
            Permissions.FinancesSummary, Permissions.FinancesClose, Permissions.ReportsFinance
        }),
        new Template("reception", "Recepción", "Inscribe, atiende a las familias y cobra.", new[]
        {
            Permissions.StudentsView, Permissions.StudentsEdit, Permissions.StudentsPayments,
            Permissions.CollectionsView, Permissions.CollectionsCollect, Permissions.AnnouncementsPublish, Permissions.ReportsStudents
        }),
        new Template("store", "Tienda", "Vende productos y administra el inventario.", new[]
        {
            Permissions.StoreSell, Permissions.StoreInventory
        }),
        new Template("health", "Salud", "Registra la ficha médica y la nutrición de los alumnos.", new[]
        {
            Permissions.StudentsView, Permissions.StudentsMedical
        })
    };
}
