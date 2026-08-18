import { publicEnv } from '@shared/config/env';
import { ApiError } from '@shared/api';
import { problemDetailsSchema } from '@shared/api/problem-details';

export async function uploadSignatureDataUrl(
  organizationId: string,
  dataUrl: string,
  signal?: AbortSignal,
): Promise<string> {
  if (!dataUrl.startsWith('data:')) return dataUrl;

  const blob = dataUrlToBlob(dataUrl);
  const form = new FormData();
  form.append('File', blob, `signature-${Date.now()}.png`);

  const base = publicEnv.NEXT_PUBLIC_API_BASE_URL.replace(/\/$/, '');
  const response = await fetch(`${base}/api/v1/uploads/signature`, {
    method: 'POST',
    headers: {
      Accept: 'application/json',
      'X-Organization-Id': organizationId,
    },
    body: form,
    signal,
  });

  const contentType = response.headers.get('content-type') ?? '';
  const payload = contentType.includes('json') ? await response.json() : null;

  if (!response.ok) {
    const parsed = problemDetailsSchema.safeParse(payload);
    if (parsed.success) throw new ApiError(response.status, parsed.data);
    throw new ApiError(response.status, {
      status: response.status,
      title: response.statusText || 'Upload failed',
    });
  }

  const url = (payload as { url?: string })?.url;
  if (!url) throw new Error('Upload succeeded but returned no URL.');
  return url;
}

function dataUrlToBlob(dataUrl: string): Blob {
  const commaIx = dataUrl.indexOf(',');
  if (commaIx < 0) throw new Error('Invalid data URL.');
  const meta = dataUrl.slice(0, commaIx);
  const base64 = dataUrl.slice(commaIx + 1);
  const mimeMatch = /data:(.*?);base64/.exec(meta);
  const mime = mimeMatch?.[1] ?? 'application/octet-stream';
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return new Blob([bytes], { type: mime });
}
