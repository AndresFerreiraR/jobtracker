import 'server-only';
import { request } from '@shared/api';
import type { Customer } from './types';

export async function searchCustomers(
  organizationId: string,
  q: string | undefined,
  take = 20,
): Promise<Customer[]> {
  const params = new URLSearchParams();
  if (q) params.set('q', q);
  params.set('take', String(take));
  return request<Customer[]>(`/api/v1/customers?${params.toString()}`, {
    organizationId,
    cache: 'no-store',
  });
}

export async function getCustomersByIds(
  organizationId: string,
  ids: string[],
): Promise<Customer[]> {
  if (ids.length === 0) return [];
  return request<Customer[]>(`/api/v1/customers/batch`, {
    method: 'POST',
    organizationId,
    body: { ids },
  });
}
