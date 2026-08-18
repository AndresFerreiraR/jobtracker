import { clientRequest } from '@shared/api';
import type { JobDetails } from './types';

export type ScheduleJobPayload = {
  scheduledDate: string;
  assigneeId?: string | null;
};

export type CompleteJobPayload = {
  signatureUrl: string;
  completedAt?: string;
};

export type AddPhotoPayload = {
  url: string;
  caption?: string | null;
  capturedAt?: string;
};

export type CancelJobPayload = {
  reason: string;
};

export function scheduleJob(
  organizationId: string,
  id: string,
  payload: ScheduleJobPayload,
  idempotencyKey?: string,
) {
  return clientRequest<void>(`/api/v1/jobs/${id}/schedule`, {
    method: 'POST',
    organizationId,
    body: payload,
    idempotencyKey,
  });
}

export function startJob(
  organizationId: string,
  id: string,
  idempotencyKey?: string,
) {
  return clientRequest<void>(`/api/v1/jobs/${id}/start`, {
    method: 'POST',
    organizationId,
    idempotencyKey,
  });
}

export function completeJob(
  organizationId: string,
  id: string,
  payload: CompleteJobPayload,
  idempotencyKey?: string,
) {
  return clientRequest<void>(`/api/v1/jobs/${id}/complete`, {
    method: 'POST',
    organizationId,
    body: payload,
    idempotencyKey,
  });
}

export function cancelJob(
  organizationId: string,
  id: string,
  payload: CancelJobPayload,
  idempotencyKey?: string,
) {
  return clientRequest<void>(`/api/v1/jobs/${id}/cancel`, {
    method: 'POST',
    organizationId,
    body: payload,
    idempotencyKey,
  });
}

export function addJobPhoto(
  organizationId: string,
  id: string,
  payload: AddPhotoPayload,
  idempotencyKey?: string,
) {
  return clientRequest<{ photoId: string }>(`/api/v1/jobs/${id}/photos`, {
    method: 'POST',
    organizationId,
    body: payload,
    idempotencyKey,
  });
}

export function getJobClient(organizationId: string, id: string) {
  return clientRequest<JobDetails>(`/api/v1/jobs/${id}`, {
    organizationId,
  });
}
