import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Pressable, Share, Switch, Text, View } from 'react-native';
import { CheckCircle2, KeyRound, Share2, Sparkles, UserPlus } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { Category, GeneratedCredential, Headquarter, MedicalRecord, StudentSummary } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Banner, Button, Card, Chips, Input, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';
import { ageFrom, digitsOnly, maskDate, money, parseDmy, toDmy, todayISO } from '../../utils/format';

type Form = {
  firstName: string; lastName: string; documentNumber: string; dob: string; phone: string; email: string;
  headquarterId: string; categoryId: string;
  guardianFirstName: string; guardianLastName: string; guardianPhone: string; guardianEmail: string;
  allergies: string; medicalConditions: string; emergencyName: string; emergencyPhone: string;
  enrollment: string; preferentialFee: string; scholarshipPercentage: string; isGuest: boolean;
  isActive: boolean; withdrawal: string;
};

const EMAIL = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
const validPhone = (p: string) => { const d = digitsOnly(p); return d.length >= 7 && d.length <= 15; };

/** Categorías cuya franja de nacimiento incluye la fecha dada. */
function matchesCategory(c: Category, isoDob: string) {
  const a = c.startDateOfBirth.slice(0, 10), b = c.endDateOfBirth.slice(0, 10);
  const [from, to] = a <= b ? [a, b] : [b, a];
  return isoDob >= from && isoDob <= to;
}

/**
 * Registro y edición de alumnos desde el campo (coordinador / administrador).
 * Al editar se envían TODOS los datos existentes: el backend reemplaza el registro completo,
 * así no se pierden becas, fechas ni la ficha nutricional que la app no muestra.
 */
export default function StudentFormScreen({ route, navigation }: any) {
  const editing: StudentSummary | undefined = route.params?.student;
  const { can } = useAuth();
  const canFinance = can('students.billing');

  const [hqs, setHqs] = useState<Headquarter[]>([]);
  const [cats, setCats] = useState<Category[]>([]);
  const [medical, setMedical] = useState<MedicalRecord | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [credentials, setCredentials] = useState<GeneratedCredential[] | null>(null);
  const savedName = useRef('');

  const [f, setF] = useState<Form>({
    firstName: editing?.firstName ?? '', lastName: editing?.lastName ?? '', documentNumber: editing?.documentNumber ?? '',
    dob: toDmy(editing?.dateOfBirth), phone: editing?.phone ?? '', email: editing?.email ?? '',
    headquarterId: editing?.headquarterId ?? '', categoryId: editing?.categoryId ?? '',
    guardianFirstName: editing?.guardianFirstName ?? '', guardianLastName: editing?.guardianLastName ?? '', guardianPhone: editing?.guardianPhone ?? '', guardianEmail: editing?.guardianEmail ?? '',
    allergies: '', medicalConditions: '', emergencyName: '', emergencyPhone: '',
    enrollment: toDmy(editing?.enrollmentDate) || toDmy(todayISO()),
    preferentialFee: editing?.preferentialFee != null ? String(editing.preferentialFee) : '',
    scholarshipPercentage: editing?.isScholarship ? '100' : editing?.scholarshipPercentage ? String(editing.scholarshipPercentage) : '',
    isGuest: !!editing?.isGuest, isActive: editing?.isActive ?? true, withdrawal: toDmy(editing?.withdrawalDate),
  });
  const set = <K extends keyof Form>(k: K, v: Form[K]) => setF(p => ({ ...p, [k]: v }));
  const hasGuardian = !!editing?.guardianId;
  // Sin permiso de salud no se muestra la ficha de un alumno existente; al inscribir sí se registra.
  const canEditMedical = !editing || can('students.medical');

  useEffect(() => {
    navigation.setOptions({ title: editing ? 'Editar alumno' : 'Nuevo alumno' });
    (async () => {
      try {
        const [h, c, m] = await Promise.all([
          api.get<Headquarter[]>('/academy-config/headquarters').then(r => r.data.filter(x => x.isActive)),
          api.get<Category[]>('/academy-config/categories').then(r => r.data.filter(x => x.isActive)),
          editing && canEditMedical ? api.get<MedicalRecord>(`/students/${editing.id}/medical-record`).then(r => r.data).catch(() => null) : Promise.resolve(null),
        ]);
        setHqs(h);
        setCats(c);
        setMedical(m);
        if (m) setF(p => ({ ...p, allergies: m.allergies ?? '', medicalConditions: m.medicalConditions ?? '', emergencyName: m.emergencyContactName ?? '', emergencyPhone: m.emergencyContactPhone ?? '' }));
        // Versiones anteriores de la API solo devolvían el nombre completo del apoderado.
        if (editing?.guardianName && editing.guardianFirstName === undefined) {
          const [gf, ...gl] = editing.guardianName.split(' ');
          setF(p => ({ ...p, guardianFirstName: gf ?? '', guardianLastName: gl.join(' ') }));
        }
        if (!editing && h.length === 1) setF(p => ({ ...p, headquarterId: h[0].id }));
      } catch (e) {
        setError(errorMessage(e, 'No se pudieron cargar las sedes y categorías.'));
      } finally {
        setLoading(false);
      }
    })();
  }, [editing, navigation, canEditMedical]);

  const isoDob = parseDmy(f.dob);
  const hqCats = useMemo(() => cats.filter(c => c.headquarterId === f.headquarterId).sort((a, b) => a.name.localeCompare(b.name)), [cats, f.headquarterId]);
  const suggested = typeof isoDob === 'string' ? hqCats.filter(c => matchesCategory(c, isoDob)) : [];
  const selectedCat = cats.find(c => c.id === f.categoryId);
  const catMismatch = !!selectedCat && typeof isoDob === 'string' && !matchesCategory(selectedCat, isoDob);

  // Sugerencia automática: al escribir la fecha de nacimiento, si hay una sola categoría posible, se elige.
  useEffect(() => {
    if (!editing && suggested.length === 1 && f.categoryId !== suggested[0].id) set('categoryId', suggested[0].id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [f.dob, f.headquarterId, cats.length]);

  const validate = (): string | null => {
    if (!f.firstName.trim() || !f.lastName.trim()) return 'Ingresa los nombres y apellidos del alumno.';
    if (isoDob === 'invalid') return 'La fecha de nacimiento no es válida (DD/MM/AAAA).';
    if (typeof isoDob === 'string' && isoDob > todayISO()) return 'La fecha de nacimiento no puede ser futura.';
    if (f.documentNumber && !/^[A-Za-z0-9]{6,15}$/.test(f.documentNumber.trim())) return 'El documento debe tener entre 6 y 15 letras o números.';
    if (!f.headquarterId) return 'Selecciona la sede.';
    if (!f.categoryId) return 'Selecciona la categoría.';
    if (f.phone && !validPhone(f.phone)) return 'El celular del alumno no es válido.';
    if (f.email && !EMAIL.test(f.email.trim())) return 'El correo del alumno no es válido.';
    const anyGuardian = f.guardianFirstName || f.guardianPhone || f.guardianEmail;
    if (!hasGuardian && anyGuardian && (!f.guardianFirstName.trim() || !f.guardianPhone.trim())) return 'Para registrar al apoderado indica su nombre y celular.';
    if (f.guardianPhone && !validPhone(f.guardianPhone)) return 'El celular del apoderado no es válido.';
    if (f.guardianEmail && !EMAIL.test(f.guardianEmail.trim())) return 'El correo del apoderado no es válido.';
    if (!editing && !f.phone.trim() && !f.guardianPhone.trim()) return 'Registra el celular del apoderado o del alumno para poder contactarlo.';
    if (f.emergencyPhone && !validPhone(f.emergencyPhone)) return 'El teléfono de emergencia no es válido.';
    if (parseDmy(f.enrollment) === 'invalid') return 'La fecha de inscripción no es válida.';
    if (!f.isActive && parseDmy(f.withdrawal) === 'invalid') return 'La fecha de retiro no es válida.';
    const pct = f.scholarshipPercentage ? Number(f.scholarshipPercentage) : 0;
    if (Number.isNaN(pct) || pct < 0 || pct > 100) return 'La beca debe estar entre 0 y 100%.';
    if (f.preferentialFee && (Number.isNaN(Number(f.preferentialFee)) || Number(f.preferentialFee) < 0)) return 'La tarifa preferencial no es válida.';
    return null;
  };

  const save = async () => {
    const problem = validate();
    if (problem) { setError(problem); return; }
    setError(null);
    setSaving(true);
    const pct = f.scholarshipPercentage ? Number(f.scholarshipPercentage) : 0;
    const payload = {
      firstName: f.firstName.trim(),
      lastName: f.lastName.trim(),
      documentNumber: f.documentNumber.trim() || null,
      dateOfBirth: typeof isoDob === 'string' ? isoDob : null,
      phone: digitsOnly(f.phone) || null,
      email: f.email.trim() || null,
      headquarterId: f.headquarterId,
      categoryId: f.categoryId,
      guardianFirstName: f.guardianFirstName.trim() || null,
      guardianLastName: f.guardianLastName.trim() || null,
      guardianPhone: digitsOnly(f.guardianPhone) || null,
      guardianEmail: f.guardianEmail.trim() || null,
      enrollmentDate: (parseDmy(f.enrollment) as string | null) ?? todayISO(),
      // Datos financieros: si el usuario no los ve, se conservan tal como estaban.
      paymentStartDate: editing?.paymentStartDate ?? null,
      preferentialFee: canFinance ? (f.preferentialFee ? Number(f.preferentialFee) : null) : editing?.preferentialFee ?? null,
      isGuest: canFinance ? f.isGuest : !!editing?.isGuest,
      isScholarship: canFinance ? pct === 100 : !!editing?.isScholarship,
      scholarshipPercentage: canFinance ? (pct > 0 && pct < 100 ? pct : null) : editing?.scholarshipPercentage ?? null,
      isActive: f.isActive,
      withdrawalDate: f.isActive ? null : (parseDmy(f.withdrawal) as string | null),
      medicalRecord: {
        allergies: f.allergies.trim() || null,
        medicalConditions: f.medicalConditions.trim() || null,
        emergencyContactName: f.emergencyName.trim() || null,
        emergencyContactPhone: digitsOnly(f.emergencyPhone) || null,
        // Datos nutricionales que esta pantalla no edita: se conservan
        weightKg: medical?.weightKg ?? null,
        heightCm: medical?.heightCm ?? null,
        bmi: medical?.bmi ?? null,
        nutritionPlan: medical?.nutritionPlan ?? null,
        nextNutritionConsultation: medical?.nextNutritionConsultation ?? null,
      },
    };
    try {
      const { data } = editing
        ? await api.put(`/students/${editing.id}`, payload)
        : await api.post('/students', payload);
      savedName.current = `${payload.firstName} ${payload.lastName}`;
      if (data?.generatedCredentials?.length) {
        setCredentials(data.generatedCredentials);
      } else {
        Alert.alert(editing ? 'Alumno actualizado' : 'Alumno registrado', savedName.current);
        navigation.goBack();
      }
    } catch (e) {
      setError(errorMessage(e, 'No se pudo guardar el alumno.'));
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <Loading />;

  if (credentials) {
    return <CredentialsResult name={savedName.current} credentials={credentials} editing={!!editing}
      onDone={() => navigation.goBack()}
      onAnother={() => { setCredentials(null); navigation.replace('StudentForm'); }} />;
  }

  return (
    <Screen>
      <SectionTitle title="Alumno" />
      <Card>
        <Input label="Nombres *" value={f.firstName} onChangeText={v => set('firstName', v)} autoCapitalize="words" maxLength={100} />
        <Input label="Apellidos *" value={f.lastName} onChangeText={v => set('lastName', v)} autoCapitalize="words" maxLength={100} />
        <Row style={{ alignItems: 'flex-start' }}>
          <View style={{ flex: 1 }}>
            <Input label="DNI / documento" value={f.documentNumber} onChangeText={v => set('documentNumber', v.replace(/\s/g, ''))} keyboardType="default" autoCapitalize="characters" maxLength={15} />
          </View>
          <View style={{ flex: 1 }}>
            <Input label="Nacimiento" value={f.dob} onChangeText={v => set('dob', maskDate(v))} placeholder="DD/MM/AAAA" keyboardType="number-pad" maxLength={10}
              hint={typeof isoDob === 'string' ? `${ageFrom(isoDob)} años` : undefined} error={isoDob === 'invalid' && f.dob.length === 10 ? 'Fecha no válida' : undefined} />
          </View>
        </Row>
        <Input label="Celular del alumno" value={f.phone} onChangeText={v => set('phone', v)} keyboardType="phone-pad" maxLength={15} />
        <Input label="Correo del alumno" value={f.email} onChangeText={v => set('email', v)} keyboardType="email-address" autoCapitalize="none"
          hint="Con correo y celular el alumno recibe su propio acceso a la app." />
      </Card>

      <SectionTitle title="Sede y categoría" />
      <Card>
        <Text style={labelStyle}>Sede *</Text>
        <Chips options={hqs.map(h => ({ value: h.id, label: h.name }))} value={f.headquarterId}
          onChange={v => setF(p => ({ ...p, headquarterId: v, categoryId: cats.find(c => c.id === p.categoryId)?.headquarterId === v ? p.categoryId : '' }))} />
        <Text style={[labelStyle, { marginTop: spacing.md }]}>Categoría *</Text>
        {!f.headquarterId ? <Text style={font.small}>Elige primero la sede.</Text> : hqCats.length === 0 ? (
          <Text style={font.small}>Esta sede no tiene categorías activas.</Text>
        ) : (
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {hqCats.map(c => {
              const active = c.id === f.categoryId;
              const isSuggested = suggested.some(s => s.id === c.id);
              return (
                <Pressable key={c.id} onPress={() => set('categoryId', c.id)} accessibilityRole="radio" accessibilityState={{ selected: active }}
                  style={[chipStyle, active && { backgroundColor: colors.primaryDark, borderColor: colors.primaryDark }, !active && isSuggested && { borderColor: colors.accent }]}>
                  <Row style={{ gap: 4 }}>
                    {isSuggested && <Sparkles size={12} color={active ? colors.white : colors.accentDark} />}
                    <Text style={{ fontWeight: '600', color: active ? colors.white : colors.textSecondary }}>{c.name}</Text>
                  </Row>
                  {c.monthlyFee > 0 && <Text style={{ fontSize: 11, color: active ? colors.primaryLight : colors.textMuted }}>{money(c.monthlyFee)}/mes</Text>}
                </Pressable>
              );
            })}
          </View>
        )}
        {suggested.length > 0 && <Text style={[font.tiny, { marginTop: 6 }]}>✨ Sugerida por la fecha de nacimiento.</Text>}
        {catMismatch && <View style={{ marginTop: spacing.sm }}><Banner tone="warning" text="La fecha de nacimiento no corresponde a esta categoría. Puedes guardarlo igual si es una decisión del entrenador." /></View>}
      </Card>

      <SectionTitle title="Apoderado" />
      <Card>
        {hasGuardian && <Text style={[font.small, { marginBottom: spacing.sm }]}>Puedes actualizar los datos de contacto del apoderado.</Text>}
        <Row style={{ alignItems: 'flex-start' }}>
          <View style={{ flex: 1 }}><Input label="Nombres" value={f.guardianFirstName} onChangeText={v => set('guardianFirstName', v)} autoCapitalize="words" /></View>
          <View style={{ flex: 1 }}><Input label="Apellidos" value={f.guardianLastName} onChangeText={v => set('guardianLastName', v)} autoCapitalize="words" /></View>
        </Row>
        <Input label="Celular" value={f.guardianPhone} onChangeText={v => set('guardianPhone', v)} keyboardType="phone-pad" maxLength={15} />
        <Input label="Correo (opcional)" value={f.guardianEmail} onChangeText={v => set('guardianEmail', v)} keyboardType="email-address" autoCapitalize="none"
          hint={hasGuardian ? undefined : 'Sin correo, el apoderado ingresa a la app con su celular.'} />
      </Card>

      {canEditMedical && <SectionTitle title="Salud y emergencia" />}
      {canEditMedical && <Card>
        <Input label="Alergias" value={f.allergies} onChangeText={v => set('allergies', v)} placeholder="Ej. penicilina, maní" />
        <Input label="Condiciones médicas" value={f.medicalConditions} onChangeText={v => set('medicalConditions', v)} placeholder="Ej. asma" />
        <Row style={{ justifyContent: 'space-between', marginBottom: spacing.sm }}>
          <Text style={labelStyle}>Contacto de emergencia</Text>
          {!!(f.guardianPhone || f.guardianFirstName) && (
            <Pressable onPress={() => setF(p => ({ ...p, emergencyName: `${p.guardianFirstName} ${p.guardianLastName}`.trim(), emergencyPhone: p.guardianPhone }))} hitSlop={8}>
              <Text style={[font.small, { color: colors.primaryDark, fontWeight: '700' }]}>Usar al apoderado</Text>
            </Pressable>
          )}
        </Row>
        <Row style={{ alignItems: 'flex-start' }}>
          <View style={{ flex: 1 }}><Input label="Nombre" value={f.emergencyName} onChangeText={v => set('emergencyName', v)} /></View>
          <View style={{ flex: 1 }}><Input label="Teléfono" value={f.emergencyPhone} onChangeText={v => set('emergencyPhone', v)} keyboardType="phone-pad" maxLength={15} /></View>
        </Row>
      </Card>}

      <SectionTitle title="Inscripción" />
      <Card>
        <Input label="Fecha de inscripción" value={f.enrollment} onChangeText={v => set('enrollment', maskDate(v))} placeholder="DD/MM/AAAA" keyboardType="number-pad" maxLength={10}
          hint="Desde esta fecha se generan las mensualidades (prorrateo si es a mitad de mes)." />
        {canFinance && (
          <>
            <Row style={{ alignItems: 'flex-start' }}>
              <View style={{ flex: 1 }}><Input label="Tarifa preferencial (S/)" value={f.preferentialFee} onChangeText={v => set('preferentialFee', v.replace(',', '.'))} keyboardType="decimal-pad" placeholder="Según categoría" /></View>
              <View style={{ flex: 1 }}><Input label="Beca (%)" value={f.scholarshipPercentage} onChangeText={v => set('scholarshipPercentage', v.replace(/\D/g, ''))} keyboardType="number-pad" maxLength={3} placeholder="0" /></View>
            </Row>
            <Row style={{ justifyContent: 'space-between' }}>
              <Text style={font.body}>Invitado (no paga mensualidad)</Text>
              <Switch value={f.isGuest} onValueChange={v => set('isGuest', v)} trackColor={{ true: colors.primary }} />
            </Row>
          </>
        )}
      </Card>

      {editing && (
        <>
          <SectionTitle title="Estado" />
          <Card>
            <Row style={{ justifyContent: 'space-between' }}>
              <View style={{ flex: 1 }}>
                <Text style={font.h3}>{f.isActive ? 'Activo' : 'Retirado'}</Text>
                <Text style={font.small}>{f.isActive ? 'Entrena y genera mensualidades.' : 'No se generan nuevas mensualidades.'}</Text>
              </View>
              <Switch value={f.isActive} onValueChange={v => set('isActive', v)} trackColor={{ true: colors.accent }} />
            </Row>
            {!f.isActive && (
              <View style={{ marginTop: spacing.md }}>
                <Input label="Fecha de retiro" value={f.withdrawal} onChangeText={v => set('withdrawal', maskDate(v))} placeholder="DD/MM/AAAA (vacío = hoy)" keyboardType="number-pad" maxLength={10} />
              </View>
            )}
          </Card>
        </>
      )}

      {error && <Banner tone="danger" text={error} />}
      <Button title={editing ? 'Guardar cambios' : 'Registrar alumno'} icon={<UserPlus color={colors.white} size={18} />} onPress={save} loading={saving} />
    </Screen>
  );
}

/** Resultado del alta: accesos generados, listos para enviar por WhatsApp. */
function CredentialsResult({ name, credentials, editing, onDone, onAnother }: {
  name: string; credentials: GeneratedCredential[]; editing: boolean; onDone: () => void; onAnother: () => void;
}) {
  const share = (c: GeneratedCredential) => Share.share({
    message:
      `¡Hola ${c.fullName}! Ya tienes acceso a Clubio, la app de la academia.\n\n` +
      `Usuario: ${c.login}\nContraseña temporal: ${c.temporaryPassword}\n\n` +
      'Descarga la app, elige la academia e ingresa con estos datos. Te pedirá crear tu propia contraseña.',
  });
  return (
    <Screen>
      <Card style={{ alignItems: 'center', gap: spacing.sm }}>
        <CheckCircle2 color={colors.success} size={48} />
        <Text style={[font.h2, { textAlign: 'center' }]}>{editing ? 'Cambios guardados' : 'Alumno registrado'}</Text>
        <Text style={[font.body, { textAlign: 'center' }]}>{name}</Text>
      </Card>
      <Banner tone="info" icon={<KeyRound color={colors.info} size={18} />} text="Se generaron accesos a la app. Envíalos ahora: la contraseña temporal solo se muestra esta vez." />
      {credentials.map(c => (
        <Card key={c.login}>
          <Text style={font.small}>{c.role}</Text>
          <Text style={font.h3}>{c.fullName}</Text>
          <Text style={[font.body, { marginTop: 4 }]}>Usuario: <Text style={{ fontWeight: '700' }}>{c.login}</Text></Text>
          <Text style={font.body}>Contraseña: <Text style={{ fontWeight: '700', letterSpacing: 1 }}>{c.temporaryPassword}</Text></Text>
          <Button title="Enviar por WhatsApp u otra app" variant="secondary" icon={<Share2 color={colors.primaryDark} size={16} />} style={{ marginTop: spacing.md }} onPress={() => share(c)} />
        </Card>
      ))}
      <Button title="Listo" onPress={onDone} />
      {!editing && <Button title="Registrar otro alumno" variant="ghost" onPress={onAnother} />}
    </Screen>
  );
}

const labelStyle = { ...font.small, fontWeight: '600' as const, color: colors.textSecondary, marginBottom: 6 };
const chipStyle = { paddingHorizontal: 12, paddingVertical: 8, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card };
