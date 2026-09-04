import { apiRequest } from '@/lib/apiClient';
import type { ProductFile, UploadSession } from '@/types/api';
import { completeUpload, initUpload } from '@/features/seller/api/sellerApi';

async function sha256Hex(data: ArrayBuffer): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', data);
  return Array.from(new Uint8Array(digest))
    .map((byte) => byte.toString(16).padStart(2, '0'))
    .join('');
}

function uploadPart(
  uploadId: string,
  partNumber: number,
  chunk: Blob,
): Promise<UploadSession> {
  return apiRequest(`/files/${uploadId}/parts/${partNumber}`, {
    method: 'PUT',
    rawBody: chunk,
  });
}

export interface UploadProgress {
  uploadedParts: number;
  totalParts: number;
}

/**
 * Drives a resumable chunked upload: declare the file and its SHA-256, send the
 * parts the server still wants, then complete. Resuming re-reads `missingParts`
 * from the session, so an interrupted upload does not restart from zero.
 */
export async function uploadProductFile(
  variantId: string,
  file: File,
  onProgress?: (progress: UploadProgress) => void,
): Promise<ProductFile> {
  const checksum = await sha256Hex(await file.arrayBuffer());
  const session = await initUpload(variantId, file.name, file.size, checksum);

  let remaining = session.missingParts;
  for (const partNumber of session.missingParts) {
    const start = (partNumber - 1) * session.partSizeBytes;
    const chunk = file.slice(start, Math.min(start + session.partSizeBytes, file.size));
    const updated = await uploadPart(session.uploadId, partNumber, chunk);
    remaining = updated.missingParts;
    onProgress?.({
      uploadedParts: session.totalParts - remaining.length,
      totalParts: session.totalParts,
    });
  }

  return completeUpload(session.uploadId);
}
