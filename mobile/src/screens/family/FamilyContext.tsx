import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { api } from '../../api/client';
import type { StudentSummary } from '../../api/types';
import { clearPaymentReminders } from '../../utils/reminders';

/** Alumno seleccionado (un apoderado puede tener varios hijos): compartido por todas las pestañas. */
interface FamilyState {
  students: StudentSummary[];
  student: StudentSummary | null;
  setStudentId: (id: string) => void;
  loading: boolean;
  reload: () => Promise<void>;
}

const Ctx = createContext<FamilyState | null>(null);

export function FamilyProvider({ children }: { children: React.ReactNode }) {
  const [students, setStudents] = useState<StudentSummary[]>([]);
  const [studentId, setStudentId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const reload = useCallback(async () => {
    try {
      const { data } = await api.get<StudentSummary[]>('/mobile/students/my-students');
      setStudents(data);
      setStudentId(prev => (prev && data.some(s => s.id === prev) ? prev : data[0]?.id ?? null));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { reload(); }, [reload]);

  // Los avisos de pago llegan del servidor: se quitan los que haya programado una versión anterior de la app.
  useEffect(() => { clearPaymentReminders(); }, []);

  const value = useMemo(() => ({
    students,
    student: students.find(s => s.id === studentId) ?? null,
    setStudentId,
    loading,
    reload,
  }), [students, studentId, loading, reload]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useFamily() {
  const c = useContext(Ctx);
  if (!c) throw new Error('useFamily debe usarse dentro de FamilyProvider');
  return c;
}
