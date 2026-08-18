import { clientRequest } from '@shared/api';
import type { Employee } from './types';

export function searchEmployeesClient(
  organizationId: string,
  q: string | undefined,
  take = 20,
  signal?: AbortSignal,
) {
  const params = new URLSearchParams();
  if (q) params.set('q', q);
  params.set('take', String(take));
  return clientRequest<Employee[]>(`/api/v1/employees?${params.toString()}`, {
    organizationId,
    signal,
  });
}

export function createEmployeeClient(
  organizationId: string,
  payload: { name: string; email?: string; phone?: string },
  idempotencyKey?: string,
) {
  return clientRequest<{ id: string }>(`/api/v1/employees`, {
    method: 'POST',
    organizationId,
    body: payload,
    idempotencyKey,
  });
}
