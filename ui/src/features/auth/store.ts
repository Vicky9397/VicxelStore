import { create } from 'zustand';
import { setAccessToken } from '@/lib/apiClient';
import type { Role, User } from '@/types/api';

interface AuthState {
  user: User | null;
  status: 'unknown' | 'authenticated' | 'anonymous';
  setSession: (user: User, accessToken: string) => void;
  clearSession: () => void;
  markAnonymous: () => void;
  hasRole: (role: Role) => boolean;
}

/**
 * Session state only. Server data lives in the TanStack Query cache; nothing is
 * duplicated between the two (spec 07 section 7.3).
 */
export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  status: 'unknown',
  setSession: (user, accessToken) => {
    setAccessToken(accessToken);
    set({ user, status: 'authenticated' });
  },
  clearSession: () => {
    setAccessToken(null);
    set({ user: null, status: 'anonymous' });
  },
  markAnonymous: () => set({ user: null, status: 'anonymous' }),
  hasRole: (role) => get().user?.roles.includes(role) ?? false,
}));
