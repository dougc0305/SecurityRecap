import { useCallback, useEffect, useState } from 'react';
import { apiKeysApi } from '../services/api';
import type { ApiKey } from '../types/api';

export function useApiKeys() {
  const [keys, setKeys] = useState<ApiKey[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await apiKeysApi.getAll();
      if (res.data.success && res.data.data) {
        setKeys(res.data.data);
      } else {
        setError(res.data.error ?? 'Failed to load API keys');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load API keys');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  return { keys, loading, error, reload };
}
