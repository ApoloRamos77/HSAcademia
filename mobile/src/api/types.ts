// Tipos de las respuestas de la API (JSON camelCase de ASP.NET Core).

export type Role = 'SuperAdmin' | 'AcademyAdmin' | 'Staff' | 'Student' | 'Guardian';

export interface PublicAcademy {
  id: string;
  tenantId: string;
  name: string;
  city?: string | null;
  sport?: string | null;
}

export interface LoginResponse {
  token: string;
  userId: string;
  email: string;
  fullName: string;
  role: Role;
  academyId?: string | null;
  academyName?: string | null;
  requirePasswordChange: boolean;
  categoryId?: string | null;
  permissions: string[];
}

export interface StudentSummary {
  id: string;
  firstName: string;
  lastName: string;
  documentNumber?: string | null;
  phone?: string | null;
  email?: string | null;
  age: number;
  dateOfBirth?: string | null;
  headquarterId: string;
  headquarterName: string;
  categoryId: string;
  categoryName: string;
  guardianId?: string | null;
  guardianName: string;
  guardianFirstName?: string;
  guardianLastName?: string;
  guardianPhone: string;
  guardianEmail?: string;
  isActive: boolean;
  enrollmentDate?: string;
  paymentStartDate?: string | null;
  withdrawalDate?: string | null;
  preferentialFee?: number | null;
  isGuest?: boolean;
  isScholarship?: boolean;
  scholarshipPercentage?: number | null;
}

export interface Headquarter { id: string; name: string; address?: string | null; isActive: boolean }

export interface Category {
  id: string;
  headquarterId: string;
  headquarterName: string;
  name: string;
  startDateOfBirth: string;
  endDateOfBirth: string;
  monthlyFee: number;
  isActive: boolean;
}

export interface GeneratedCredential { role: string; fullName: string; login: string; temporaryPassword: string }

export interface Debt {
  id: string;
  studentId: string;
  studentName: string;
  description: string;
  amount: number;
  amountPaid: number;
  amountPending: number;
  dueDate: string;
  isPaid: boolean;
  status: 'Pagado' | 'Vencido' | 'En Curso';
  daysOverdue: number;
  discountAmount?: number;
  discountDetail?: string | null;
  /** 1 mensualidad, 2 producto, 5 mora… */
  type?: number;
  /** Mensualidad vencida sin regularizar: la familia debe acercarse a administración. */
  requiresRegularization?: boolean;
  regularizedAt?: string | null;
  regularizedByName?: string | null;
  regularizationNote?: string | null;
  lateFeeAmount?: number | null;
  installments: { id: string; amountPaid: number; paidAt: string; method: string; receiptNumber?: string | null }[];
}

export type ProofStatus = 'Pending' | 'Approved' | 'Rejected';

export interface PaymentProof {
  id: string;
  paymentRecordId: string;
  debtDescription: string;
  debtPending: number;
  /** Pagado después del vencimiento y sin regularizar: al aprobar se decide la mora. */
  requiresRegularization?: boolean;
  debtDueDate?: string;
  studentId: string;
  studentName: string;
  submittedByName: string;
  amount: number;
  method: string;
  operationNumber?: string | null;
  voucherUrl: string;
  paidOn: string;
  notes?: string | null;
  status: ProofStatus;
  createdAt: string;
  rejectionReason?: string | null;
  receiptNumber?: string | null;
}

export interface AttendanceMonth {
  month: string;
  yearMonth: string;
  present: number;
  justified: number;
  absent: number;
  total: number;
  records: { attendanceId: string; date: string; status: string; category?: string | null; notes?: string | null }[];
}

export interface MobileEvent {
  id: string;
  title: string;
  date: string; // YYYY-MM-DD
  startTime?: string | null; // HH:mm
  endTime?: string | null;
  type: 'Training' | 'Tournament' | 'Birthday' | 'Other';
  location?: string | null;
  category?: string | null;
  description?: string | null;
  allDay?: boolean;
}

export interface MyCall {
  callId: string;
  eventId: string;
  title: string;
  opponent?: string | null;
  startTime: string;
  endTime: string;
  location?: string | null;
  studentId: string;
  studentName: string;
  isConfirmed: boolean | null;
}

export interface Evaluation {
  id: string;
  period: string;
  evaluationDate: string;
  technical: number;
  tactical: number;
  physical: number;
  attitude: number;
  average: number;
  strengths?: string | null;
  toImprove?: string | null;
  goals?: string | null;
  comments?: string | null;
  isVisibleToFamily: boolean;
  evaluatorName: string;
}

export interface SportsSummary {
  matches: number;
  starts: number;
  minutes: number;
  goals: number;
  assists: number;
  yellowCards: number;
  redCards: number;
  averageRating?: number | null;
  recentMatches: { eventId: string; date: string; title: string; opponent?: string | null; score?: string | null; minutes: number; goals: number; assists: number; rating?: number | null }[];
}

export interface Announcement {
  id: string;
  title: string;
  content: string;
  isPinned: boolean;
  datePosted: string;
  authorName: string;
}

export interface TrainingSession {
  eventId: string;
  title: string;
  date: string; // YYYY-MM-DD
  startTime: string; // HH:mm
  categoryId: string;
  categoryName: string;
  headquarterName: string;
  attendanceClosed: boolean;
  presentCount: number;
  absentCount: number;
  justifiedCount: number;
  totalStudents: number;
  attendanceTaken: boolean;
}

/** AttendanceStatus del backend: 1 Presente, 2 Ausente, 3 Tarde, 4 Justificado. */
export type AttendanceCode = 1 | 2 | 3 | 4;

export interface RosterStudent {
  studentId: string;
  firstName: string;
  lastName: string;
  attendanceId?: string | null;
  status?: AttendanceCode | null;
  notes?: string | null;
}

export interface MedicalRecord {
  allergies?: string | null;
  medicalConditions?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  weightKg?: number | null;
  heightCm?: number | null;
  bmi?: number | null;
  nutritionPlan?: string | null;
  nextNutritionConsultation?: string | null;
}

export interface MatchResult {
  eventId: string;
  title: string;
  opponent?: string | null;
  startTime: string;
  goalsFor?: number | null;
  goalsAgainst?: number | null;
  matchNotes?: string | null;
  players: {
    studentId: string; studentName: string; category: string; called: boolean; played: boolean; isStarter: boolean;
    minutes: number; goals: number; assists: number; yellowCards: number; redCards: number; rating?: number | null; notes?: string | null;
  }[];
}

export interface StaffPayment {
  id: string;
  periodMonth: number;
  periodYear: number;
  baseAmount: number;
  bonuses: number;
  deductions: number;
  totalPaid: number;
  status: number; // 0 pendiente, 1 pagado
  paidAt?: string | null;
  notes?: string | null;
}

export interface Dashboard {
  students: { active: number; newThisMonth: number; withdrawnThisMonth: number; byCategory: { categoryId: string; category: string; headquarter: string; count: number }[] };
  finance?: {
    billed: number; collected: number; pending: number; collectionRate: number; overdueAmount: number; overdueStudents: number;
    topOverdue: { studentId: string; name: string; category: string; amount: number; maxDaysOverdue: number }[];
  } | null;
  attendance: { rateLast30Days?: number | null; recordsLast30Days: number; lowAttendance: { studentId: string; name: string; category: string; rate: number }[] };
  upcomingEvents: { id: string; title: string; type: number; startTime: string; categories?: string | null; opponent?: string | null }[];
  birthdays: { studentId: string; name: string; category: string; date: string; turnsAge: number; isToday: boolean }[];
  alerts: { level: 'danger' | 'warning' | 'info'; message: string }[];
}

export interface Product { id: string; name: string; price: number; stock: number; isActive: boolean; productCategory?: string }

/** Cuenta familiar: apoderado, hermanos y todo lo pendiente (GET /receipts/family/{studentId}). */
export interface FamilyAccount {
  guardianId?: string | null;
  payerName: string;
  payerDocument?: string | null;
  payerPhone?: string | null;
  students: { studentId: string; name: string; categoryName: string; isActive: boolean }[];
  pendingDebts: Debt[];
  totalPending: number;
}

export interface Receipt {
  id?: string | null;
  number: string;
  issuedAt: string;
  payerName: string;
  method: string;
  operationNumber?: string | null;
  notes?: string | null;
  subtotal: number;
  discountTotal: number;
  total: number;
  status: 'Issued' | 'Voided';
  isPreview?: boolean;
  cashHandoverId?: string | null;
  lines: {
    type: 'Debt' | 'Product'; description: string; studentId?: string | null; studentName?: string | null; quantity: number;
    unitPrice: number; discount: number; discountLabel?: string | null; total: number; paymentRecordId?: string | null; productId?: string | null;
  }[];
}

export interface DiscountRule {
  id: string; name: string; kind: 'Sibling' | 'EarlyPayment' | 'Promotion'; valueType: 'Percent' | 'Amount'; value: number;
  scope: 'MonthlyFees' | 'Products' | 'All'; isActive: boolean; isCurrentlyValid: boolean; label: string; categoryName?: string | null;
}

export interface CashHandover {
  id: string; collectorName: string; declaredAt: string; receiptsCount: number; expectedCash: number; digitalTotal: number;
  declaredCash: number; declaredDifference: number; notes?: string | null; status: 'Pending' | 'Received';
  receivedByName?: string | null; receivedAt?: string | null; receivedCash?: number | null; receivedDifference?: number | null; receivedNotes?: string | null;
}

export interface MyCash {
  collectorName: string; pendingReceipts: number; pendingCash: number; pendingDigital: number;
  byMethod: { method: string; count: number; total: number }[];
  receipts: Receipt[];
  recentHandovers: CashHandover[];
}
