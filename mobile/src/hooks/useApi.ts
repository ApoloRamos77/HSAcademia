import { useCallback, useEffect, useRef, useState } from 'react';
import { useFocusEffect } from '@react-navigation/native';
import { errorMessage } from '../api/client';

/**
 * Carga datos con estado de carga/error. Recarga al volver a la pantalla (focus)
 * y expone refresh() para "jalar para actualizar".
 */
export function useApi<T>(loader: () => Promise<T>, deps: unknown[] = [], { reloadOnFocus = true } = {}) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const first = useRef(true);
  const loaderRef = useRef(loader);
  loaderRef.current = loader;

  const run = useCallback(async (mode: 'initial' | 'refresh' | 'silent') => {
    if (mode === 'initial') setLoading(true);
    if (mode === 'refresh') setRefreshing(true);
    try {
      setData(await loaderRef.current());
      setError(null);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { run('initial'); }, deps);

  useFocusEffect(useCallback(() => {
    if (first.current) { first.current = false; return; }
    if (reloadOnFocus) run('silent');
  }, [run, reloadOnFocus]));

  return { data, setData, loading, refreshing, error, refresh: () => run('refresh'), reload: () => run('silent') };
}
