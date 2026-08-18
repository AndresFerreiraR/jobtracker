import 'server-only';
import { request } from '@shared/api';
import type { JobDetails, PagedJobs } from './types';

export type ListJobsQuery = {
  status?: string;
  assigneeId?: string;
  customerId?: string;
  scheduledFrom?: string;
  scheduledTo?: string;
  q?: string;
  cursor?: string;
  pageSize?: number;
};

function toQueryString(q: ListJobsQuery): string {
  const params = new URLSearchParams();
  for (const [k, v] of Object.entries(q)) {
    if (v !== undefined && v !== null && v !== '') params.set(k, String(v));
  }
  const s = params.toString();
  return s ? `?${s}` : '';
}

export async function listJobs(
  organizationId: string,
  q: ListJobsQuery = {},
): Promise<PagedJobs> {
  return request<PagedJobs>(`/api/v1/jobs${toQueryString(q)}`, {
    organizationId,
    cache: 'no-store',
  });
}

export async function getJob(
  organizationId: string,
  id: string,
): Promise<JobDetails> {
  return request<JobDetails>(`/api/v1/jobs/${id}`, {
    organizationId,
    cache: 'no-store',
  });
}

export type CreateJobPayload = {
  title: string;
  description: string;
  customerId: string;
  address: {
    street: string;
    city: string;
    state: string;
    zipCode: string;
    latitude: number | null;
    longitude: number | null;
  };
};

export async function createJob(
  organizationId: string,
  payload: CreateJobPayload,
): Promise<{ id: string }> {
  return request<{ id: string }>('/api/v1/jobs', {
    method: 'POST',
    organizationId,
    body: payload,
  });
}
