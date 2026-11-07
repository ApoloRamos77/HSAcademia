import React from 'react';
import { Text, View } from 'react-native';
import { Chips } from '../../components/ui';
import { font } from '../../theme';
import { useFamily } from './FamilyContext';

/** Selector de hijo; solo aparece si el apoderado tiene más de un alumno. */
export default function StudentPicker() {
  const { students, student, setStudentId } = useFamily();
  if (students.length <= 1) return null;
  return (
    <View>
      <Text style={[font.tiny, { marginBottom: 4 }]}>VIENDO A</Text>
      <Chips options={students.map(s => ({ value: s.id, label: s.firstName }))} value={student?.id ?? ''} onChange={setStudentId} />
    </View>
  );
}
