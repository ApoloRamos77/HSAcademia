import React from 'react';
import { Text, View } from 'react-native';
import { Clock, Wallet } from 'lucide-react-native';
import { api } from '../../api/client';
import type { MyPayroll, StaffPayment } from '../../api/types';
import { Badge, Card, Divider, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { money, monthName } from '../../utils/format';

const dm = (iso: string) => { const [, m, d] = iso.slice(0, 10).split('-'); return `${d}/${m}`; };
const STATUS: Record<string, { tone: 'success' | 'warning'; text: string }> = {
  Paid: { tone: 'success', text: 'Pagado' },
  Approved: { tone: 'warning', text: 'Por pagar' },
};

/**
 * Mis horas y pagos: lo dictado en el periodo en curso con su monto estimado, los pagos recibidos
 * y los adelantos pendientes. Con un servidor anterior muestra solo los pagos.
 */
export default function MyPaymentsScreen() {
  const { data, loading, refreshing, refresh } = useApi(async () => {
    try {
      return { payroll: (await api.get<MyPayroll>('/payroll/me')).data, legacy: null as StaffPayment[] | null };
    } catch {
      const legacy = (await api.get<StaffPayment[]>('/finances-premium/my-payments', { params: { month: -1, year: 0 } })).data;
      return { payroll: null as MyPayroll | null, legacy };
    }
  }, []);
  if (loading && !data) return <Loading />;
  const p = data?.payroll;

  if (!p) {
    const legacy = data?.legacy ?? [];
    return (
      <Screen refreshing={refreshing} onRefresh={refresh}>
        {legacy.length === 0 ? <Card><Empty icon={<Wallet color={colors.textMuted} />} text="Aún no hay pagos registrados." /></Card>
          : legacy.map(x => (
            <Card key={x.id}>
              <Row style={{ justifyContent: 'space-between' }}>
                <Text style={[font.h3, { textTransform: 'capitalize' }]}>{monthName(x.periodMonth)} {x.periodYear}</Text>
                <Text style={{ fontSize: 18, fontWeight: '700', color: colors.text }}>{money(x.totalPaid)}</Text>
              </Row>
            </Card>
          ))}
      </Screen>
    );
  }

  const c = p.current;
  const advances = p.pendingAdvances.reduce((s, a) => s + a.amount, 0);
  const estimate = c ? c.baseAmount + c.unitsAmount - advances : 0;

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Text style={font.small}>{p.scheme} · pago {p.frequency.toLowerCase()}</Text>

      {c && (
        <>
          <SectionTitle title={`Periodo en curso · ${dm(c.periodStart)} – ${dm(c.periodEnd)}`} />
          <Card>
            {c.hours > 0 || c.sessions > 0 ? (
              <Text style={font.h3}>{c.hours} h en {c.sessions} {c.sessions === 1 ? 'sesión' : 'sesiones'}</Text>
            ) : <Text style={font.h3}>Sin sesiones registradas aún</Text>}
            {!!c.rateLabel && <Text style={[font.tiny, { marginTop: 2 }]}>{c.rateLabel}</Text>}
            <Divider />
            {c.baseAmount > 0 && <Line label="Sueldo fijo" value={money(c.baseAmount)} />}
            {(c.unitsAmount > 0 || c.baseAmount === 0) && <Line label="Por horas / sesiones" value={money(c.unitsAmount)} />}
            {p.pendingAdvances.map(a => <Line key={a.id} label={`Adelanto ${dm(a.date)} · ${a.concept}`} value={`− ${money(a.amount)}`} />)}
            <Line label="Estimado a recibir" value={money(estimate)} bold />
            <Text style={[font.tiny, { marginTop: spacing.sm }]}>
              Estimado con la asistencia registrada hasta hoy. Bonos y descuentos se agregan al liquidar.
            </Text>
            {c.warnings.map(w => <Text key={w} style={[font.tiny, { color: colors.warning, marginTop: 4 }]}>⚠ {w}</Text>)}
          </Card>

          {c.days.length > 0 && (
            <Card>
              {c.days.map(d => (
                <View key={d.date} style={{ marginBottom: spacing.sm }}>
                  <Row style={{ justifyContent: 'space-between' }}>
                    <Text style={[font.body, { fontWeight: '700', color: colors.text }]}>{new Date(d.date).toLocaleDateString('es-PE', { weekday: 'short', day: '2-digit', month: '2-digit', timeZone: 'UTC' })}</Text>
                    <Text style={font.body}>{money(d.amount)}</Text>
                  </Row>
                  {d.items.map(i => (
                    <Text key={i.eventId} style={font.tiny}>
                      {i.time} · {i.title}{i.role === 'Reemplazo' ? ' (reemplazo)' : ''} · {i.hours} h · {money(i.amount)}
                    </Text>
                  ))}
                </View>
              ))}
            </Card>
          )}
        </>
      )}

      <SectionTitle title="Pagos" />
      {p.payments.length === 0 ? <Card><Empty icon={<Clock color={colors.textMuted} />} text="Aún no hay pagos registrados." /></Card>
        : p.payments.map(x => (
          <Card key={x.id}>
            <Row style={{ justifyContent: 'space-between' }}>
              <View style={{ flex: 1 }}>
                <Text style={font.h3}>{dm(x.periodStart)} – {dm(x.periodEnd)}</Text>
                <Text style={font.tiny}>{x.hours != null ? `${x.hours} h · ` : ''}{x.frequency}</Text>
              </View>
              <View style={{ alignItems: 'flex-end', gap: 4 }}>
                <Text style={{ fontSize: 18, fontWeight: '700', color: colors.text }}>{money(x.total)}</Text>
                <Badge tone={STATUS[x.status]?.tone ?? 'warning'} text={STATUS[x.status]?.text ?? x.status} />
              </View>
            </Row>
            {x.lines.map(l => (
              <Text key={l.id} style={font.tiny}>{l.kind === 'Bonus' ? '+ Bono · ' : l.kind === 'Advance' ? '− ' : '− Descuento · '}{l.concept} · {money(l.amount)}</Text>
            ))}
          </Card>
        ))}
    </Screen>
  );
}

function Line({ label, value, bold }: { label: string; value: string; bold?: boolean }) {
  return (
    <Row style={{ justifyContent: 'space-between', marginTop: 4 }}>
      <Text style={[font.small, bold && { fontWeight: '700', color: colors.text }]}>{label}</Text>
      <Text style={[font.small, bold && { fontWeight: '700', color: colors.text }]}>{value}</Text>
    </Row>
  );
}
