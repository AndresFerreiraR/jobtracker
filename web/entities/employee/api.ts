import 'server-only';
import { request } from '@shared/api';
import type { Employee } from './types';

export async function searchEmployees(
  organizationId: string,
  q: string | undefined,
  take = 20,
): Promise<Employee[]> {
  const params = new URLSearchParams();
  if (q) params.set('q', q);
  params.set('take', String(take));
  return request<Employee[]>(`/api/v1/employees?${params.toString()}`, {
    organizationId,
    cache: 'no-store',
  });
}

export async function getEmployeesByIds(
  organizationId: string,
  ids: string[],
): Promise<Employee[]> {
  if (ids.length === 0) return [];
  return request<Employee[]>(`/api/v1/employees/batch`, {
    method: 'POST',
    organizationId,
    body: { ids },
  });
}
