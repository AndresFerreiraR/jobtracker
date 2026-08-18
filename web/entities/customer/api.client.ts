import { clientRequest } from '@shared/api';
import type { Customer } from './types';

export function searchCustomersClient(
  organizationId: string,
  q: string | undefined,
  take = 20,
  signal?: AbortSignal,
) {
  const params = new URLSearchParams();
  if (q) params.set('q', q);
  params.set('take', String(take));
  return clientRequest<Customer[]>(`/api/v1/customers?${params.toString()}`, {
    organizationId,
    signal,
  });
}

export function createCustomerClient(
  organizationId: string,
  payload: { name: string; email?: string; phone?: string },
  idempotencyKey?: string,
) {
  return clientRequest<{ id: string }>(`/api/v1/customers`, {
    method: 'POST',
    organizationId,
    body: payload,
    idempotencyKey,
  });
}
