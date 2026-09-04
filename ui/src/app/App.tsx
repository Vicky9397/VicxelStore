import { QueryClientProvider } from '@tanstack/react-query';
import { useEffect, type ReactElement } from 'react';
import { AppRouter } from '@/app/router';
import { useSessionBootstrap } from '@/features/auth/hooks/useSessionBootstrap';
import { useAuthStore } from '@/features/auth/store';
import { setAuthLostHandler } from '@/lib/apiClient';
import { queryClient } from '@/lib/queryClient';

function SessionGate(): ReactElement {
  useSessionBootstrap();
  const clearSession = useAuthStore((state) => state.clearSession);

  useEffect(() => {
    setAuthLostHandler(clearSession);
    return () => {
      setAuthLostHandler(null);
    };
  }, [clearSession]);

  return <AppRouter />;
}

export function App(): ReactElement {
  return (
    <QueryClientProvider client={queryClient}>
      <SessionGate />
    </QueryClientProvider>
  );
}
