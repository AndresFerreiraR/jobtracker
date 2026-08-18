import { apiBaseUrl, serverEnv } from '@shared/config/env';
import { ApiError, problemDetailsSchema } from './problem-details';

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';

type RequestOptions = {
  method?: Method;
  body?: unknown;
  organizationId?: string;
  signal?: AbortSignal;
  cache?: RequestCache;
  next?: NextFetchRequestConfig;
};

export async function request<T>(path: string, opts: RequestOptions = {}): Promise<T> {
  const {
    method = 'GET',
    body,
    organizationId = serverEnv.DEFAULT_ORG_ID,
    signal,
    cache,
    next,
  } = opts;

  if (!organizationId) {
    throw new Error(
      'Missing organizationId. Pass it explicitly or set DEFAULT_ORG_ID in env.',
    );
  }

  const url = `${apiBaseUrl().replace(/\/$/, '')}${path}`;

  const response = await fetch(url, {
    method,
    headers: {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      'X-Organization-Id': organizationId,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal,
    cache,
    next,
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
