import { ageFrom, digitsOnly, maskDate, parseDmy, toDmy } from '../src/utils/format';

describe('fechas escritas a mano (DD/MM/AAAA)', () => {
  test('máscara mientras se escribe', () => {
    expect(maskDate('1')).toBe('1');
    expect(maskDate('150')).toBe('15/0');
    expect(maskDate('15032015')).toBe('15/03/2015');
    expect(maskDate('15/03/2015999')).toBe('15/03/2015');
  });

  test('valida fechas reales', () => {
    expect(parseDmy('')).toBeNull();
    expect(parseDmy('15/03/2015')).toBe('2015-03-15');
    expect(parseDmy('31/02/2015')).toBe('invalid');
    expect(parseDmy('29/02/2016')).toBe('2016-02-29');
    expect(parseDmy('1/3/2015')).toBe('invalid');
  });

  test('ida y vuelta con ISO', () => {
    expect(toDmy('2015-03-15T00:00:00Z')).toBe('15/03/2015');
    expect(toDmy(null)).toBe('');
  });

  test('edad cumplida', () => {
    const t = new Date();
    const iso = `${t.getFullYear() - 10}-01-01`;
    expect(ageFrom(iso)).toBe(10);
  });

  test('celular solo dígitos', () => {
    expect(digitsOnly('+51 987-654-321')).toBe('51987654321');
  });
});
