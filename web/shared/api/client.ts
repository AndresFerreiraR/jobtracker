import { publicEnv } from '@shared/config/env';
import { ApiError, problemDetailsSchema } from './problem-details';

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';

type ClientRequestOptions = {
  method?: Method;
  body?: unknown;
  organizationId: string;
  idempotencyKey?: string;
  signal?: AbortSignal;
};

export async function clientRequest<T>(
  path: string,
  opts: ClientRequestOptions,
): Promise<T> {
  const { method = 'GET', body, organizationId, idempotencyKey, signal } = opts;
  const base = publicEnv.NEXT_PUBLIC_API_BASE_URL.replace(/\/$/, '');
  const url = `${base}${path}`;

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    Accept: 'application/json',
    'X-Organization-Id': organizationId,
  };
  if (idempotencyKey) headers['Idempotency-Key'] = idempotencyKey;

  const response = await fetch(url, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
  });

  if (response.status === 204) return undefined as T;

  const contentType = response.headers.get('content-type') ?? '';
  const payload = contentType.includes('json') ? await response.json() : null;

  if (!response.ok) {
    const parsed = problemDetailsSchema.safeParse(payload);
    if (parsed.success) throw new ApiError(response.status, parsed.data);
    throw new ApiError(response.status, {
      status: response.status,
      title: response.statusText || 'Request failed',
    });
  }

  return payload as T;
}
