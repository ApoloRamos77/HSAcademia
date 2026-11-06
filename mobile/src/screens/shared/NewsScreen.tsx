import React, { useState } from 'react';
import { Alert, Switch, Text, View } from 'react-native';
import { Megaphone, Pin } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { Announcement } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Button, Card, Empty, Input, Loading, Row, Screen } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { instantDate } from '../../utils/format';

/** Comunicados de la academia. El personal y el administrador también pueden publicar. */
export default function NewsScreen() {
  const { role } = useAuth();
  const canPublish = role === 'AcademyAdmin' || role === 'Staff';
  const { data, loading, refreshing, refresh, reload } = useApi(() => api.get<Announcement[]>('/announcement').then(r => r.data), []);
  const [writing, setWriting] = useState(false);
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [pinned, setPinned] = useState(false);
  const [saving, setSaving] = useState(false);

  const publish = async () => {
    if (!title.trim() || !content.trim()) { Alert.alert('Faltan datos', 'Completa el título y el mensaje.'); return; }
    setSaving(true);
    try {
      await api.post('/announcement', { title: title.trim(), content: content.trim(), isPinned: pinned });
      setTitle(''); setContent(''); setPinned(false); setWriting(false);
      reload();
    } catch (e) {
      Alert.alert('No se pudo publicar', errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  if (loading && !data) return <Loading />;
  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      {canPublish && !writing && <Button title="Nuevo comunicado" variant="secondary" icon={<Megaphone color={colors.primaryDark} size={18} />} onPress={() => setWriting(true)} />}
      {writing && (
        <Card>
          <Input label="Título" value={title} onChangeText={setTitle} maxLength={150} />
          <Input label="Mensaje" value={content} onChangeText={setContent} multiline maxLength={4000} />
          <Row style={{ justifyContent: 'space-between', marginBottom: spacing.md }}>
            <Text style={font.body}>Fijar al inicio</Text>
            <Switch value={pinned} onValueChange={setPinned} trackColor={{ true: colors.primary }} />
          </Row>
          <Row>
            <Button title="Cancelar" variant="ghost" onPress={() => setWriting(false)} style={{ flex: 1 }} />
            <Button title="Publicar" onPress={publish} loading={saving} style={{ flex: 1 }} />
          </Row>
        </Card>
      )}
      {(data ?? []).length === 0 ? (
        <Card><Empty icon={<Megaphone color={colors.textMuted} />} text="Aún no hay comunicados." /></Card>
      ) : data!.map(n => (
        <Card key={n.id} style={n.isPinned ? { borderColor: colors.primary } : undefined}>
          <Row>
            {n.isPinned && <Pin color={colors.primaryDark} size={16} />}
            <Text style={[font.h3, { flex: 1 }]}>{n.title}</Text>
          </Row>
          <Text style={[font.body, { marginTop: 6 }]}>{n.content}</Text>
          <View style={{ marginTop: spacing.sm }}><Text style={font.tiny}>{instantDate(n.datePosted)} · {n.authorName}</Text></View>
        </Card>
      ))}
    </Screen>
  );
}
