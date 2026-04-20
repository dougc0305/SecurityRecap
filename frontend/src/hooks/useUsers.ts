import { useCallback, useEffect, useState } from 'react';
import { usersApi } from '../services/api';
import type { ManagedUser } from '../types/api';

export function useUsers() {
  const [users, setUsers] = useState<ManagedUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await usersApi.getAll();
      if (res.data.success && res.data.data) {
        setUsers(res.data.data);
      } else {
        setError(res.data.error ?? 'Failed to load users');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load users');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  return { users, loading, error, reload };
}
