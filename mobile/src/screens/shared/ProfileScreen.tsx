import React, { useEffect, useState } from 'react';
import { Text, View } from 'react-native';
import { MapPin, ShieldCheck, Smartphone } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { Profile } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Banner, Button, Card, Divider, Input, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { shortDay } from '../../utils/format';

/** Mis datos, qué puedo hacer en la academia y mis últimos ingresos. */
export default function ProfileScreen() {
  const { refreshSession } = useAuth();
  const { data, loading } = useApi(() => api.get<Profile>('/auth/profile').then(r => r.data), []);
  const [form, setForm] = useState({ firstName: '', lastName: '', phone: '' });
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ tone: 'success' | 'danger'; text: string } | null>(null);

  useEffect(() => {
    if (data) setForm({ firstName: data.firstName, lastName: data.lastName, phone: data.phone ?? '' });
  }, [data]);

  const save = async () => {
    setMessage(null);
    setSaving(true);
    try {
      await api.put('/auth/profile', form);
      await refreshSession();
      setMessage({ tone: 'success', text: 'Datos actualizados.' });
    } catch (e) {
      setMessage({ tone: 'danger', text: errorMessage(e) });
    } finally {
      setSaving(false);
    }
  };

  if (loading && !data) return <Loading />;
  if (!data) return <Screen><Banner tone="danger" text="No se pudo cargar tu perfil." /></Screen>;

  const isTeam = data.role === 'Staff' || data.role === 'AcademyAdmin';

  return (
    <Screen>
      {message && <Banner tone={message.tone} text={message.text} />}
      <SectionTitle title="Mis datos" />
      <Card>
        <Input label="Nombre" value={form.firstName} onChangeText={firstName => setForm(f => ({ ...f, firstName }))} />
        <Input label="Apellido" value={form.lastName} onChangeText={lastName => setForm(f => ({ ...f, lastName }))} />
        <Input label="Teléfono" value={form.phone} keyboardType="phone-pad" onChangeText={phone => setForm(f => ({ ...f, phone }))} />
        {!!data.email && <Text style={font.small}>Correo de ingreso: {data.email}</Text>}
        <Button title="Guardar" onPress={save} loading={saving} style={{ marginTop: spacing.md }} />
      </Card>

      {isTeam && (
        <>
          <SectionTitle title="Mi acceso" />
          <Card>
            <Row style={{ flexWrap: 'wrap', gap: 6 }}>
              <ShieldCheck color={colors.primaryDark} size={18} />
              {data.isOwner && <Badge tone="info" text="Propietario" />}
              {!data.isOwner && data.fullAccess && <Badge tone="info" text="Administrador" />}
              {data.roles.map(r => <Badge key={r} text={r} />)}
              {!data.fullAccess && data.roles.length === 0 && <Badge tone="danger" text="Sin rol asignado" />}
            </Row>
            {data.fullAccess ? (
              <Text style={[font.body, { marginTop: spacing.md }]}>Tienes acceso completo a la academia.</Text>
            ) : (
              <View style={{ marginTop: spacing.md, gap: spacing.sm }}>
                <Row><MapPin color={colors.textMuted} size={16} /><Text style={[font.body, { flex: 1 }]}>Sedes: {data.headquarters.length ? data.headquarters.join(', ') : 'Todas'}</Text></Row>
                <Text style={font.body}>Categorías: {data.categories.length ? data.categories.join(', ') : 'Ninguna asignada'}</Text>
                <Divider />
                {data.permissionGroups.map(g => (
                  <View key={g.name}>
                    <Text style={[font.small, { fontWeight: '700' }]}>{g.name}</Text>
                    <Text style={font.small}>{g.items.join(' · ')}</Text>
                  </View>
                ))}
              </View>
            )}
          </Card>

          <SectionTitle title="Ingresos recientes" />
          <Card>
            {data.recentLogins.length === 0 && <Text style={font.small}>Aún no hay ingresos registrados.</Text>}
            {data.recentLogins.map((l, i) => (
              <View key={l.at}>
                {i > 0 && <Divider />}
                <Row><Smartphone color={colors.textMuted} size={16} /><Text style={[font.body, { flex: 1 }]}>{l.device}</Text><Text style={font.tiny}>{shortDay(l.at)}</Text></Row>
              </View>
            ))}
          </Card>
        </>
      )}
    </Screen>
  );
}
