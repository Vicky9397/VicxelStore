import { useMutation, useQuery } from '@tanstack/react-query';
import { useEffect, useState, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { FormError } from '@/components/forms/FormError';
import { uploadProductFile, type UploadProgress } from '@/features/seller/api/chunkedUpload';
import { getScanStatus } from '@/features/seller/api/sellerApi';

/**
 * Chunked upload for one variant, followed by scan polling. A file is not
 * deliverable until the scan reports Clean, so the panel keeps reporting status
 * until it settles rather than claiming success at upload time.
 */
export function FileUploader({
  variantId,
  variantName,
  hasCleanFile,
  onScanSettled,
}: {
  variantId: string;
  variantName: string;
  hasCleanFile: boolean;
  onScanSettled: () => void;
}): ReactElement {
  const { t } = useTranslation();
  const [progress, setProgress] = useState<UploadProgress | null>(null);
  const [fileId, setFileId] = useState<string | null>(null);

  const upload = useMutation({
    mutationFn: (file: File) => uploadProductFile(variantId, file, setProgress),
    onSuccess: (file) => setFileId(file.id),
  });

  // Scanning is asynchronous, so poll until the outcome is no longer Pending.
  const scan = useQuery({
    queryKey: ['scan-status', fileId],
    queryFn: () => getScanStatus(fileId!),
    enabled: fileId !== null,
    refetchInterval: (query) => (query.state.data?.scanStatus === 'Pending' ? 2000 : false),
  });

  const scanStatus = scan.data?.scanStatus ?? null;

  useEffect(() => {
    if (scanStatus === 'Clean' || scanStatus === 'Infected') {
      onScanSettled();
    }
  }, [scanStatus, onScanSettled]);

  return (
    <div className="border rounded p-3 mb-3">
      <div className="d-flex justify-content-between align-items-center mb-2">
        <h3 className="h6 mb-0">{variantName}</h3>
        {hasCleanFile ? (
          <span className="badge text-bg-success">{t('seller.hasFile')}</span>
        ) : (
          <span className="badge text-bg-warning">{t('seller.needsFile')}</span>
        )}
      </div>

      <FormError error={upload.error} />

      <input
        className="form-control mb-2"
        type="file"
        aria-label={t('seller.chooseFile', { variant: variantName })}
        disabled={upload.isPending}
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file !== undefined) {
            setFileId(null);
            setProgress(null);
            upload.mutate(file);
          }
        }}
      />

      {upload.isPending && progress !== null ? (
        <div className="progress mb-2" role="progressbar" aria-label={t('seller.uploading')}>
          <div
            className="progress-bar"
            style={{ width: `${(progress.uploadedParts / progress.totalParts) * 100}%` }}
          >
            {progress.uploadedParts}/{progress.totalParts}
          </div>
        </div>
      ) : null}

      {scanStatus === 'Pending' ? (
        <div className="alert alert-info py-2 mb-0" role="status">
          {t('seller.scanPending')}
        </div>
      ) : null}
      {scanStatus === 'Clean' ? (
        <div className="alert alert-success py-2 mb-0" role="status">
          {t('seller.scanClean')}
        </div>
      ) : null}
      {scanStatus === 'Infected' ? (
        <div className="alert alert-danger py-2 mb-0" role="alert">
          {t('seller.scanInfected')}
        </div>
      ) : null}
    </div>
  );
}
