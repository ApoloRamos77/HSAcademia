import React from 'react';
import { Text, View } from 'react-native';
import { Wallet } from 'lucide-react-native';
import { api } from '../../api/client';
import type { StaffPayment } from '../../api/types';
import { Badge, Card, Empty, Loading, Row, Screen } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font } from '../../theme';
import { money, monthName } from '../../utils/format';

/** Pagos de honorarios del entrenador / personal. */
export default function MyPaymentsScreen() {
  const { data, loading, refreshing, refresh } = useApi(
    () => api.get<StaffPayment[]>('/finances-premium/my-payments', { params: { month: -1, year: 0 } }).then(r => r.data), []);
  if (loading && !data) return <Loading />;
  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      {(data ?? []).length === 0 ? (
        <Card><Empty icon={<Wallet color={colors.textMuted} />} text="Aún no hay pagos registrados." /></Card>
      ) : data!.map(p => (
        <Card key={p.id}>
          <Row style={{ justifyContent: 'space-between' }}>
            <View>
              <Text style={[font.h3, { textTransform: 'capitalize' }]}>{monthName(p.periodMonth)} {p.periodYear}</Text>
              <Text style={font.tiny}>Base {money(p.baseAmount)}{p.bonuses ? ` · bonos ${money(p.bonuses)}` : ''}{p.deductions ? ` · descuentos ${money(p.deductions)}` : ''}</Text>
            </View>
            <View style={{ alignItems: 'flex-end', gap: 4 }}>
              <Text style={{ fontSize: 18, fontWeight: '700', color: colors.text }}>{money(p.totalPaid)}</Text>
              <Badge tone={p.status === 1 ? 'success' : 'warning'} text={p.status === 1 ? 'Pagado' : 'Pendiente'} />
            </View>
          </Row>
          {p.notes ? <Text style={[font.small, { marginTop: 6 }]}>{p.notes}</Text> : null}
        </Card>
      ))}
    </Screen>
  );
}
