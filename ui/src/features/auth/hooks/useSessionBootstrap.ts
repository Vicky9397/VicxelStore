import { useEffect } from 'react';
import { refresh } from '@/features/auth/api/authApi';
import { useAuthStore } from '@/features/auth/store';

/**
 * On a cold load the access token is gone (it lives in memory only), so the app
 * asks the refresh cookie for a new session once before rendering guarded routes.
 */
export function useSessionBootstrap(): void {
  const status = useAuthStore((state) => state.status);
  const setSession = useAuthStore((state) => state.setSession);
  const markAnonymous = useAuthStore((state) => state.markAnonymous);

  useEffect(() => {
    if (status !== 'unknown') {
      return;
    }

    let cancelled = false;
    refresh()
      .then((response) => {
        if (!cancelled) {
          setSession(response.user, response.accessToken);
        }
      })
      .catch(() => {
        if (!cancelled) {
          markAnonymous();
        }
      });

    return () => {
      cancelled = true;
    };
  }, [status, setSession, markAnonymous]);
}
