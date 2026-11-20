import React, { useEffect, useState } from 'react';
import { Image, StyleSheet, Text, View } from 'react-native';
import { launchCamera, launchImageLibrary, type Asset } from 'react-native-image-picker';
import { api } from '../api/client';
import { FILES_URL } from '../config';
import { colors, radius } from '../theme';
import { initials } from '../utils/format';

/** URL absoluta de la foto (la API la devuelve relativa: "/uploads/students/x.jpg"). */
export const photoUri = (p?: string | null) => (!p ? null : /^(https?:|file:|content:)/i.test(p) ? p : `${FILES_URL}${p}`);

/** Foto del alumno en círculo; sin foto (o si no carga, p. ej. sin señal) muestra sus iniciales. */
export function StudentAvatar({ photoUrl, name, size = 42 }: { photoUrl?: string | null; name: string; size?: number }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [photoUrl]);
  const uri = photoUri(photoUrl);
  const box = { width: size, height: size, borderRadius: radius.pill };
  if (uri && !failed) {
    return <Image source={{ uri }} style={[styles.base, box]} onError={() => setFailed(true)} accessibilityLabel={`Foto de ${name}`} />;
  }
  return (
    <View style={[styles.base, styles.initials, box]}>
      <Text style={[styles.initialsText, { fontSize: Math.max(12, Math.round(size * 0.36)) }]} maxFontSizeMultiplier={1.2}>{initials(name)}</Text>
    </View>
  );
}

/**
 * Toma o elige la foto ya reducida (600 px, JPEG): sube rápido aunque haya poca señal en la cancha.
 * Devuelve null si se canceló; lanza un error con mensaje legible si falló.
 */
export async function pickStudentPhoto(source: 'camera' | 'library'): Promise<Asset | null> {
  const options = { mediaType: 'photo' as const, quality: 0.8 as const, maxWidth: 600, maxHeight: 600, includeBase64: false };
  const res = source === 'camera'
    ? await launchCamera({ ...options, saveToPhotos: false, cameraType: 'back' })
    : await launchImageLibrary({ ...options, selectionLimit: 1 });
  if (res.didCancel) return null;
  if (res.errorCode) {
    throw new Error(res.errorCode === 'camera_unavailable' ? 'Este celular no tiene cámara disponible.'
      : res.errorCode === 'permission' ? 'Permite el acceso a la cámara o a las fotos en los ajustes del celular.'
      : 'No se pudo obtener la foto.');
  }
  return res.assets?.[0] ?? null;
}

/** Sube la foto del alumno y devuelve la ruta nueva. */
export async function uploadStudentPhoto(studentId: string, photo: Asset): Promise<string> {
  const form = new FormData();
  form.append('file', { uri: photo.uri, type: photo.type ?? 'image/jpeg', name: photo.fileName ?? 'foto.jpg' } as any);
  const res = await api.post<{ photoUrl: string }>(`/students/${studentId}/photo`, form,
    { headers: { 'Content-Type': 'multipart/form-data' }, timeout: 60000 });
  return res.data.photoUrl;
}

export const removeStudentPhoto = (studentId: string) => api.delete(`/students/${studentId}/photo`);

const styles = StyleSheet.create({
  base: { backgroundColor: colors.primaryLight },
  initials: { alignItems: 'center', justifyContent: 'center' },
  initialsText: { color: colors.primaryDark, fontWeight: '700' },
});
