'use client';

import { create } from 'zustand';
import type { JobDetails, JobStatus } from './types';
import { canTransition } from './types';

export type MutationState =
  | { kind: 'idle' }
  | { kind: 'pending'; op: string }
  | { kind: 'error'; op: string; message: string };

type JobStoreState = {
  job: JobDetails | null;
  mutation: MutationState;
};

type JobStoreActions = {
  hydrate: (job: JobDetails) => void;
  replace: (job: JobDetails) => void;
  clear: () => void;
  optimistic: <T>(
    op: string,
    apply: (draft: JobDetails) => JobDetails,
    commit: () => Promise<T>,
  ) => Promise<T>;
  setError: (op: string, message: string) => void;
  clearError: () => void;
};

export type JobStore = JobStoreState & JobStoreActions;

export const useJobStore = create<JobStore>((set, get) => ({
  job: null,
  mutation: { kind: 'idle' },

  hydrate: (job) => {
    if (get().job?.id !== job.id) set({ job, mutation: { kind: 'idle' } });
  },

  replace: (job) => set({ job, mutation: { kind: 'idle' } }),

  clear: () => set({ job: null, mutation: { kind: 'idle' } }),

  optimistic: async (op, apply, commit) => {
    const prev = get().job;
    if (!prev) throw new Error('No job loaded in store.');

    const next = apply(prev);
    set({ job: next, mutation: { kind: 'pending', op } });

    try {
      const result = await commit();
      set({ mutation: { kind: 'idle' } });
      return result;
    } catch (e) {
      const message = e instanceof Error ? e.message : 'Unexpected error';
      set({ job: prev, mutation: { kind: 'error', op, message } });
      throw e;
    }
  },

  setError: (op, message) => set({ mutation: { kind: 'error', op, message } }),
  clearError: () => set({ mutation: { kind: 'idle' } }),
}));

export function nextStatusFor(current: JobStatus): JobStatus | null {
  const options: JobStatus[] = ['Scheduled', 'InProgress', 'Completed'];
  for (const opt of options) if (canTransition(current, opt)) return opt;
  return null;
}
