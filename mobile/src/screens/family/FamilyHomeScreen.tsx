import React, { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { Camera, Bell, CalendarDays, CheckCircle2, CreditCard, Megaphone, Pin, QrCode, Trophy, ChevronRight, UserCircle2 } from 'lucide-react-native';
import { api } from '../../api/client';
import type { Announcement, AttendanceMonth, Debt, FamilyNotice, MyCall, MyEncounter, MyTournament } from '../../api/types';
import { ATT, capitalize } from './HistoryScreens';
import { longDay } from '../../utils/format';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Banner, Card, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, radius, spacing } from '../../theme';
import { clockTime, firstName, greeting, instantDate, money, sedeLabel } from '../../utils/format';
import { StudentAvatar, pickStudentPhoto, removeStudentPhoto, uploadStudentPhoto } from '../../components/StudentPhoto';
import { errorMessage } from '../../api/client';
import ExpandableText from '../../components/ExpandableText';
import { useFamily } from './FamilyContext';
import StudentPicker from './StudentPicker';

interface NextEvent { title: string; date: string; location: string; type: string }

export default function FamilyHomeScreen({ navigation }: any) {
  const { session } = useAuth();
  const { student, loading: loadingFamily, reload: reloadFamily } = useFamily();
  const [photoBusy, setPhotoBusy] = useState(false);

  // El apoderado pone o cambia la foto de su hijo: cámara o galería (reducida a 600 px antes de subir).
  const run = async (task: () => Promise<unknown>, done: string) => {
    setPhotoBusy(true);
    try {
      await task();
      await reloadFamily();
      Alert.alert('Foto', done);
    } catch (e) {
      Alert.alert('Foto', errorMessage(e, 'No se pudo guardar la foto. Inténtalo de nuevo con mejor señal.'));
    } finally {
      setPhotoBusy(false);
    }
  };
  const choose = async (source: 'camera' | 'library') => {
    if (!student) return;
    try {
      const asset = await pickStudentPhoto(source);
      if (asset?.uri) await run(() => uploadStudentPhoto(student.id, asset, true), 'Foto actualizada.');
    } catch (e: any) {
      Alert.alert('Foto', e?.message ?? 'No se pudo obtener la foto.');
    }
  };
  const changePhoto = () => {
    if (!student || photoBusy) return;
    const title = `Foto de ${student.firstName}`;
    // Android muestra como máximo 3 botones: con foto, "Quitar" reemplaza a "Cancelar" (se cierra tocando fuera).
    Alert.alert(title, 'Se verá en su carné y en la lista del entrenador.', student.photoUrl ? [
      { text: 'Quitar', style: 'destructive', onPress: () => run(() => removeStudentPhoto(student.id, true), 'Foto quitada.') },
      { text: 'Galería', onPress: () => choose('library') },
      { text: 'Tomar foto', onPress: () => choose('camera') },
    ] : [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Galería', onPress: () => choose('library') },
      { text: 'Tomar foto', onPress: () => choose('camera') },
    ], { cancelable: true });
  };

  const { data, loading, refreshing, refresh } = useApi(async () => {
    if (!student) return null;
    const [debts, calls, history, news, next, notices, tournaments] = await Promise.all([
      api.get<Debt[]>('/finances/my-debts').then(r => r.data),
      api.get<MyCall[]>('/calendar/mobile/my-calls').then(r => r.data).catch(() => [] as MyCall[]),
      api.get<AttendanceMonth[]>('/attendance/my-history', { params: { studentId: student.id, months: 2 } }).then(r => r.data).catch(() => []),
      api.get<Announcement[]>('/announcement').then(r => r.data).catch(() => [] as Announcement[]),
      api.get<NextEvent>('/calendar/mobile/next', { params: { categoryId: student.categoryId, studentId: student.id } }).then(r => (r.status === 204 ? null : r.data)).catch(() => null),
      api.get<FamilyNotice[]>('/notices/mine').then(r => r.data).catch(() => [] as FamilyNotice[]),
      api.get<MyTournament[]>('/tournaments/mobile/mine', { params: { studentId: student.id } }).then(r => r.data).catch(() => [] as MyTournament[]),
    ]);
    if (notices.some(n => !n.isRead)) api.post('/notices/mine/read').catch(() => {});
    return { debts, calls, history, news, next, notices, tournaments };
  }, [student?.id]);

  if (loadingFamily || (loading && !data)) return <Loading text="Cargando…" />;
  if (!student) {
    return <Screen><Empty icon={<UserCircle2 color={colors.textMuted} size={40} />} text="Tu cuenta aún no tiene alumnos vinculados. Comunícate con la academia." /></Screen>;
  }

  const pending = (data?.debts ?? []).filter(d => d.studentId === student.id && !d.isPaid);
  const totalPending = pending.reduce((s, d) => s + d.amountPending, 0);
  const overdue = pending.filter(d => d.status === 'Vencido');
  const toRegularize = pending.filter(d => d.requiresRegularization);
  const callsToAnswer = (data?.calls ?? []).filter(c => c.studentId === student.id && c.isConfirmed === null);
  const month = data?.history?.[0];
  // Lo último: el registro de asistencia más reciente y el último encuentro ya jugado o con asistencia.
  const encounterDays = new Set((data?.tournaments ?? []).flatMap(t => (t.encounters ?? []).map(e => e.startTime.slice(0, 10))));
  const lastAttendance = (data?.history ?? []).flatMap(m => m.records)
    .filter(r => r.status !== 'Pendiente' && !encounterDays.has(r.date.slice(0, 10)))
    .sort((a, b) => b.date.localeCompare(a.date))[0];
  const lastEncounter = (data?.tournaments ?? []).filter(t => t.studentId === student.id)
    .flatMap(t => (t.encounters ?? []).map(e => ({ ...e, tournament: t.name })))
    .filter(e => e.goalsFor != null || e.attendance != null)
    .sort((a, b) => b.startTime.localeCompare(a.startTime))[0] as (MyEncounter & { tournament: string }) | undefined;
  const attended = month ? month.present : 0;
  const counted = month ? month.present + month.absent : 0;
  const rate = counted ? Math.round((100 * attended) / counted) : null;

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Row style={{ gap: spacing.md, alignItems: 'center' }}>
        <Pressable onPress={changePhoto} disabled={photoBusy} hitSlop={6} accessibilityRole="button"
          accessibilityLabel={student.photoUrl ? `Cambiar la foto de ${student.firstName}` : `Agregar una foto de ${student.firstName}`}>
          <StudentAvatar photoUrl={student.photoUrl} name={`${student.firstName} ${student.lastName}`} size={56} />
          <View style={styles.photoBadge}>
            {photoBusy ? <ActivityIndicator size="small" color={colors.white} /> : <Camera color={colors.white} size={13} />}
          </View>
        </Pressable>
        <View style={{ flex: 1, minWidth: 0 }}>
          <Text style={font.h1}>{greeting()}, {firstName(session?.fullName)}</Text>
          <Text style={font.small}>{[`${student.firstName} ${student.lastName}`, student.categoryName, sedeLabel(student.headquarterName)].filter(Boolean).join(' · ')}</Text>
        </View>
      </Row>
      <StudentPicker />

      {/* Avisos de llegada */}
      {(data?.notices ?? []).slice(0, 3).map(n => (
        <Card key={n.id} style={n.isRead ? undefined : styles.cardNew}>
          <Row style={{ justifyContent: 'space-between' }}>
            <Row style={{ flex: 1 }}><Bell color={colors.primaryDark} size={18} /><Text style={[font.h3, { flexShrink: 1 }]}>{n.title}</Text></Row>
            {!n.isRead && <Badge tone="info" text="Nuevo" />}
          </Row>
          <Text style={[font.body, { marginTop: 4 }]}>{n.body}</Text>
          <Text style={[font.tiny, { marginTop: 6 }]}>{instantDate(n.createdAt)} · {clockTime(n.createdAt)}</Text>
        </Card>
      ))}

      {/* Estado de cuenta */}
      <Card onPress={() => navigation.navigate('Pagos')} style={pending.length ? (overdue.length ? styles.cardDanger : undefined) : styles.cardOk}>
        <Row style={{ justifyContent: 'space-between' }}>
          <Row>
            {pending.length ? <CreditCard color={overdue.length ? colors.danger : colors.primaryDark} /> : <CheckCircle2 color={colors.success} />}
            <Text style={font.h3}>{pending.length ? 'Pagos pendientes' : '¡Todo al día!'}</Text>
          </Row>
          <ChevronRight color={colors.textMuted} />
        </Row>
        {pending.length > 0 ? (
          <>
            <Text style={styles.big}>{money(totalPending)}</Text>
            <Text style={font.small}>
              {toRegularize.length
                ? `${toRegularize.length} pago(s) vencido(s): puede aplicarse mora. Toca para pagar o comunícate con administración.`
                : `${overdue.length ? `${overdue.length} vencida(s). ` : ''}Toca para pagar con Yape, Plin o transferencia y enviar tu comprobante.`}
            </Text>
          </>
        ) : <Text style={font.small}>No hay mensualidades pendientes.</Text>}
      </Card>

      {/* Convocatorias por responder */}
      {callsToAnswer.length > 0 && (
        <Pressable onPress={() => navigation.navigate('Agenda')}>
          <Banner tone="warning" icon={<Trophy color={colors.warning} size={20} />}
            text={`${student.firstName} tiene ${callsToAnswer.length} convocatoria(s) por confirmar. Toca para responder.`} />
        </Pressable>
      )}

      {/* Próximo evento */}
      <Card onPress={() => navigation.navigate('Agenda')}>
        <Row><CalendarDays color={colors.primaryDark} /><Text style={font.h3}>Próxima actividad</Text></Row>
        {data?.next ? (
          <View style={{ marginTop: spacing.sm }}>
            <Text style={font.h2}>{data.next.title}</Text>
            <Text style={font.body}>{data.next.date}{data.next.location ? ` · ${data.next.location}` : ''}</Text>
          </View>
        ) : <Text style={[font.small, { marginTop: spacing.sm }]}>No hay actividades programadas.</Text>}
      </Card>

      {/* Lo último: entrenamiento y encuentro (el historial está en Progreso) */}
      {(lastAttendance || lastEncounter) && (
        <Card onPress={() => navigation.navigate('Progreso')}>
          <Row style={{ justifyContent: 'space-between' }}>
            <Row><CheckCircle2 color={colors.primaryDark} /><Text style={font.h3}>Lo último</Text></Row>
            <ChevronRight color={colors.textMuted} size={18} />
          </Row>
          {lastAttendance && (
            <Row style={{ justifyContent: 'space-between', marginTop: spacing.sm }}>
              <View style={{ flex: 1 }}>
                <Text style={font.small}>Último entrenamiento</Text>
                <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{capitalize(longDay(lastAttendance.date))}</Text>
              </View>
              <Badge tone={ATT[lastAttendance.status]?.tone ?? 'muted'} text={ATT[lastAttendance.status]?.label ?? lastAttendance.status} />
            </Row>
          )}
          {lastEncounter && (
            <Row style={{ justifyContent: 'space-between', marginTop: spacing.sm }}>
              <View style={{ flex: 1 }}>
                <Text style={font.small}>Último encuentro · {lastEncounter.tournament}</Text>
                <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>
                  {[lastEncounter.round, lastEncounter.opponent ? `vs ${lastEncounter.opponent}` : null].filter(Boolean).join(' ') || 'Encuentro'}
                </Text>
              </View>
              {lastEncounter.goalsFor != null && lastEncounter.goalsAgainst != null ? (
                <Badge tone={lastEncounter.goalsFor > lastEncounter.goalsAgainst ? 'success' : lastEncounter.goalsFor < lastEncounter.goalsAgainst ? 'danger' : 'info'}
                  text={`${lastEncounter.goalsFor > lastEncounter.goalsAgainst ? 'Ganó' : lastEncounter.goalsFor < lastEncounter.goalsAgainst ? 'Perdió' : 'Empató'} ${lastEncounter.goalsFor}-${lastEncounter.goalsAgainst}`} />
              ) : lastEncounter.attendance ? (
                <Badge tone={lastEncounter.attendance === 'absent' ? 'danger' : 'success'} text={lastEncounter.attendance === 'absent' ? 'Faltó' : 'Asistió'} />
              ) : null}
            </Row>
          )}
        </Card>
      )}

      {/* Accesos rápidos */}
      <View style={styles.grid}>
        <Quick icon={<QrCode color={colors.primaryDark} />} label="Carné QR" onPress={() => navigation.navigate('Carné')} />
        <Quick icon={<Trophy color={colors.primaryDark} />} label="Progreso" onPress={() => navigation.navigate('Progreso')} />
        <Quick icon={<CalendarDays color={colors.primaryDark} />} label="Asistencia"
          extra={rate != null ? `${rate}% este mes` : undefined} onPress={() => navigation.navigate('Progreso')} />
        <Quick icon={<UserCircle2 color={colors.primaryDark} />} label="Mi cuenta" onPress={() => navigation.navigate('Account')} />
      </View>

      {/* Comunicados */}
      <SectionTitle title="Comunicados" action={(data?.news?.length ?? 0) > 3 ? (
        <Text style={styles.link} onPress={() => navigation.navigate('News')}>Ver todos</Text>) : undefined} />
      {(data?.news ?? []).length === 0 ? (
        <Card><Empty icon={<Megaphone color={colors.textMuted} />} text="Sin comunicados recientes." /></Card>
      ) : (data?.news ?? []).slice(0, 3).map(n => (
        <Card key={n.id}>
          <Row>
            {n.isPinned && <Pin color={colors.primaryDark} size={16} />}
            <Text style={[font.h3, { flex: 1 }]}>{n.title}</Text>
          </Row>
          <View style={{ marginTop: 4 }}><ExpandableText text={n.content} lines={3} style={font.body} /></View>
          <Text style={[font.tiny, { marginTop: 6 }]}>{instantDate(n.datePosted)}</Text>
        </Card>
      ))}
      {overdue.length > 0 && <Badge tone="danger" text="Tienes pagos vencidos" />}
    </Screen>
  );
}

function Quick({ icon, label, extra, onPress }: { icon: React.ReactNode; label: string; extra?: string; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} style={({ pressed }) => [styles.quick, pressed && { opacity: 0.8 }]} accessibilityRole="button">
      <View style={styles.quickIcon}>{icon}</View>
      <Text style={[font.small, { color: colors.text, fontWeight: '600' }]}>{label}</Text>
      {extra && <Text style={font.tiny}>{extra}</Text>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  photoBadge: { position: 'absolute', right: -2, bottom: -2, width: 24, height: 24, borderRadius: radius.pill, backgroundColor: colors.primaryDark, borderWidth: 2, borderColor: colors.card, alignItems: 'center', justifyContent: 'center' },
  big: { fontSize: 28, fontWeight: '700', color: colors.text, marginTop: spacing.sm },
  cardDanger: { borderColor: colors.danger, borderLeftWidth: 4 },
  cardOk: { borderColor: colors.successBg },
  cardNew: { borderLeftWidth: 4, borderLeftColor: colors.primary },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  quick: { width: '48%', flexGrow: 1, backgroundColor: colors.card, borderRadius: radius.lg, padding: spacing.md, borderWidth: 1, borderColor: colors.border, gap: 6 },
  quickIcon: { width: 40, height: 40, borderRadius: radius.md, backgroundColor: colors.primaryLight, alignItems: 'center', justifyContent: 'center' },
  link: { ...font.small, color: colors.primaryDark, fontWeight: '600' },
});

