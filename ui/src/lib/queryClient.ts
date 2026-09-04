import { QueryClient } from '@tanstack/react-query';
import { ApiRequestError } from '@/lib/apiClient';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (failureCount, error) => {
        // Client errors are not worth retrying; transient failures get two more attempts.
        if (error instanceof ApiRequestError && error.status < 500) {
          return false;
        }
        return failureCount < 2;
      },
    },
  },
});
