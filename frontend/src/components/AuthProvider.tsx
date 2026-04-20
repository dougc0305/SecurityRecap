import { useState, useCallback, type ReactNode } from 'react';
import { AuthContext, type AuthContextType } from '../hooks/useAuth';
import { authApi } from '../services/api';
import type { UserInfo } from '../types/api';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(() => {
    const stored = localStorage.getItem('user');
    return stored ? JSON.parse(stored) : null;
  });

  const isAuthenticated = !!user && !!localStorage.getItem('accessToken');

  const login = useCallback(async (email: string, password: string) => {
    const res = await authApi.login({ email, password });
    if (res.data.success && res.data.data) {
      const { accessToken, refreshToken, user: userInfo } = res.data.data;
      localStorage.setItem('accessToken', accessToken);
      localStorage.setItem('refreshToken', refreshToken);
      localStorage.setItem('user', JSON.stringify(userInfo));
      setUser(userInfo);
    } else {
      throw new Error(res.data.error || 'Login failed');
    }
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
    setUser(null);
  }, []);

  const markPasswordChanged = useCallback(() => {
    setUser((prev) => {
      if (!prev) return prev;
      const next = { ...prev, mustChangePassword: false };
      localStorage.setItem('user', JSON.stringify(next));
      return next;
    });
  }, []);

  const value: AuthContextType = { user, login, logout, isAuthenticated, markPasswordChanged };

  return <AuthContext value={value}>{children}</AuthContext>;
}
