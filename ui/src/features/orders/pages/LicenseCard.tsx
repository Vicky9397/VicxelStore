import { useMutation } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { requestDownload } from '@/features/cart/api/cartApi';
import { ApiRequestError } from '@/lib/apiClient';
import type { License } from '@/types/api';

function formatSize(bytes: number): string {
  const megabytes = bytes / (1024 * 1024);
  return megabytes >= 1 ? `${megabytes.toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

/**
 * One purchased license and its files.
 *
 * Every refusal is the server's decision; this only translates the code it
 * returns, so the buyer learns why a download was denied instead of meeting a
 * dead link. The URL it hands back is short-lived and single-purpose.
 */
export function LicenseCard({
  license,
  onDownloaded,
}: {
  license: License;
  onDownloaded: () => void;
}): ReactElement {
  const { t } = useTranslation();
  const [failedFileId, setFailedFileId] = useState<string | null>(null);

  const download = useMutation({
    mutationFn: (fileId: string) => requestDownload(license.id, fileId),
    onSuccess: (result) => {
      setFailedFileId(null);
      window.location.assign(result.url);
      onDownloaded();
    },
    onError: (_error, fileId) => setFailedFileId(fileId),
  });

  const remaining = license.downloadLimit - license.downloadsUsed;

  const failureMessage = ((): string | null => {
    if (failedFileId === null || download.error === null) {
      return null;
    }

    if (!(download.error instanceof ApiRequestError)) {
      return t('errors.generic');
    }

    switch (download.error.code) {
      case 'DOWNLOAD_LIMIT':
        return t('orders.downloadLimitReached');
      case 'FILE_NOT_READY':
        return t('orders.fileNotReady');
      case 'FILE_QUARANTINED':
        return t('orders.fileQuarantined');
      case 'NO_ENTITLEMENT':
        return t('orders.noEntitlement');
      default:
        return t('errors.generic');
    }
  })();

  return (
    <div className="border rounded p-3 mb-3">
      <div className="d-flex justify-content-between align-items-start mb-2">
        <div>
          <div className="fw-semibold">{license.productTitle}</div>
          <div className="text-muted small">{license.variantName}</div>
        </div>
        <span className={remaining > 0 ? 'badge text-bg-secondary' : 'badge text-bg-warning'}>
          {t('orders.downloadsRemaining', { remaining, limit: license.downloadLimit })}
        </span>
      </div>

      {failureMessage !== null ? (
        <div className="alert alert-warning py-2" role="alert">
          {failureMessage}
        </div>
      ) : null}

      {license.files.length === 0 ? (
        <p className="text-muted small mb-0">{t('orders.noFilesYet')}</p>
      ) : (
        <ul className="list-unstyled mb-0">
          {license.files.map((file) => (
            <li className="d-flex justify-content-between align-items-center py-1" key={file.id}>
              <span>
                {file.fileName}{' '}
                <span className="text-muted small">({formatSize(file.sizeBytes)})</span>
              </span>
              <button
                type="button"
                className="btn btn-sm btn-outline-primary"
                disabled={download.isPending || remaining <= 0}
                onClick={() => download.mutate(file.id)}
              >
                {t('orders.download')}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
