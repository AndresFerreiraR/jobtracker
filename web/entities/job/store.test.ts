import { describe, it, expect, beforeEach } from 'vitest';
import { useJobStore } from './store';
import type { JobDetails } from './types';

const baseJob: JobDetails = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Roof replacement',
  description: 'Full re-roof',
  address: {
    street: '1 Main',
    city: 'Austin',
    state: 'TX',
    zipCode: '78701',
    latitude: null,
    longitude: null,
  },
  status: 'Draft',
  scheduledDate: null,
  startedAt: null,
  completedAt: null,
  cancelledAt: null,
  cancellationReason: null,
  signatureUrl: null,
  assigneeId: null,
  customerId: '22222222-2222-2222-2222-222222222222',
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  photos: [],
};

describe('useJobStore', () => {
  beforeEach(() => {
    useJobStore.getState().clear();
  });

  it('hydrate loads a job and sets idle mutation', () => {
    useJobStore.getState().hydrate(baseJob);
    const s = useJobStore.getState();
    expect(s.job?.id).toBe(baseJob.id);
    expect(s.mutation).toEqual({ kind: 'idle' });
  });

  it('optimistic applies changes eagerly and commits on success', async () => {
    useJobStore.getState().hydrate(baseJob);
    const result = await useJobStore.getState().optimistic(
      'schedule',
      (draft) => ({ ...draft, status: 'Scheduled', scheduledDate: '2026-09-01T10:00:00Z' }),
      async () => 'ok',
    );
    expect(result).toBe('ok');
    const s = useJobStore.getState();
    expect(s.job?.status).toBe('Scheduled');
    expect(s.mutation.kind).toBe('idle');
  });

  it('optimistic rolls back on failure and surfaces error', async () => {
    useJobStore.getState().hydrate(baseJob);
    const original = baseJob.status;

    await expect(
      useJobStore.getState().optimistic(
        'start',
        (draft) => ({ ...draft, status: 'InProgress' }),
        async () => { throw new Error('server rejected'); },
      ),
    ).rejects.toThrow('server rejected');

    const s = useJobStore.getState();
    expect(s.job?.status).toBe(original);
    expect(s.mutation).toMatchObject({ kind: 'error', op: 'start', message: 'server rejected' });
  });
});
