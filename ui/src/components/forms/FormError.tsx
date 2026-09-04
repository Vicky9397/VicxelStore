import type { ReactElement } from 'react';
import { ApiRequestError } from '@/lib/apiClient';

/**
 * Renders an API failure as a single alert. Field-level messages are merged into
 * the form by the calling page; this shows the summary (spec 07 section 7.9).
 */
export function FormError({ error }: { error: unknown }): ReactElement | null {
  if (error === null || error === undefined) {
    return null;
  }

  const message = error instanceof ApiRequestError ? error.title : 'Something went wrong. Try again.';

  return (
    <div className="alert alert-danger" role="alert">
      {message}
    </div>
  );
}
