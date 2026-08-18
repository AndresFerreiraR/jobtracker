'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { useJobStore } from '@entities/job';
import { scheduleJob, startJob, cancelJob } from '@entities/job/api.client';
import { CompleteJobDialog } from '@features/complete-job';
import { EmployeeAutocomplete } from '@features/employee-picker';
import { ApiError } from '@shared/api';
import { toast } from '@shared/ui/toast';
import type { Employee } from '@entities/employee';
import type { JobDetails } from '@entities/job';

type Props = {
  organizationId: string;
  job: JobDetails;
};

export function JobActions({ organizationId, job }: Props) {
  const router = useRouter();
  const stored = useJobStore((s) => s.job);
  const mutation = useJobStore((s) => s.mutation);
  const hydrate = useJobStore((s) => s.hydrate);
  const optimistic = useJobStore((s) => s.optimistic);

  const [showComplete, setShowComplete] = useState(false);
  const [showSchedule, setShowSchedule] = useState(false);
  const [scheduledDate, setScheduledDate] = useState('');
  const [assignee, setAssignee] = useState<Employee | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [showCancel, setShowCancel] = useState(false);

  useEffect(() => { hydrate(job); }, [hydrate, job]);

  const current = stored ?? job;
  const busy = mutation.kind === 'pending';

  const withToast = async (op: string, run: () => Promise<unknown>) => {
    try {
      await run();
      toast.success('Job updated', `${current.title} · ${op}`);
      router.refresh();
    } catch (e) {
      const msg = e instanceof ApiError ? (e.problem.detail ?? e.problem.title) : (e as Error).message;
      toast.error(`Could not ${op}`, msg);
    }
  };

  const doSchedule = () => {
    if (!scheduledDate) {
      toast.error('Date required', 'Pick a scheduled date first.');
      return;
    }
    if (!assignee) {
      toast.error('Assignee required', 'Pick or create an employee to assign.');
      return;
    }
    const iso = new Date(scheduledDate).toISOString();
    const key = crypto.randomUUID();
    void withToast('schedule', () =>
      optimistic(
        'schedule',
        (draft) => ({ ...draft, status: 'Scheduled', scheduledDate: iso, assigneeId: assignee.id }),
        () => scheduleJob(organizationId, current.id, { scheduledDate: iso, assigneeId: assignee.id }, key),
      ),
    );
    setShowSchedule(false);
  };

  const doStart = () => {
    const key = crypto.randomUUID();
    void withToast('start', () =>
      optimistic(
        'start',
        (draft) => ({ ...draft, status: 'InProgress', startedAt: new Date().toISOString() }),
        () => startJob(organizationId, current.id, key),
      ),
    );
  };

  const doCancel = () => {
    if (!cancelReason.trim()) {
      toast.error('Reason required', 'Provide a cancellation reason.');
      return;
    }
    const reason = cancelReason.trim();
    const key = crypto.randomUUID();
    void withToast('cancel', () =>
      optimistic(
        'cancel',
        (draft) => ({
          ...draft,
          status: 'Cancelled',
          cancelledAt: new Date().toISOString(),
          cancellationReason: reason,
        }),
        () => cancelJob(organizationId, current.id, { reason }, key),
      ),
    );
    setShowCancel(false);
    setCancelReason('');
  };

  return (
    <section aria-label="Job actions" className="flex flex-col gap-3 rounded-md border bg-white p-4 shadow-sm">
      <div className="flex flex-wrap items-center gap-2">
        {current.status === 'Draft' && (
          <button
            type="button"
            onClick={() => setShowSchedule(true)}
            disabled={busy}
            className="rounded-md bg-brand-500 px-3 py-1.5 text-sm text-white hover:bg-brand-600 disabled:bg-brand-300"
          >
            Schedule
          </button>
        )}
        {current.status === 'Scheduled' && (
          <button
            type="button"
            onClick={doStart}
            disabled={busy}
            className="rounded-md bg-accent-500 px-3 py-1.5 text-sm text-white hover:bg-accent-600 disabled:bg-accent-300"
          >
            Start
          </button>
        )}
        {current.status === 'InProgress' && (
          <button
            type="button"
            onClick={() => setShowComplete(true)}
            disabled={busy}
            className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm text-white hover:bg-emerald-700 disabled:bg-emerald-300"
          >
            Complete
          </button>
        )}
        {current.status !== 'Completed' && current.status !== 'Cancelled' && (
          <button
            type="button"
            onClick={() => setShowCancel(true)}
            disabled={busy}
            className="rounded-md border border-red-200 bg-white px-3 py-1.5 text-sm text-red-700 hover:bg-red-50 disabled:opacity-50"
          >
            Cancel
          </button>
        )}
        {mutation.kind === 'error' && (
          <p role="alert" className="text-xs text-red-700">
            Last {mutation.op} failed — {mutation.message}
          </p>
        )}
      </div>

      {showSchedule && (
        <div className="flex flex-wrap items-end gap-3 rounded-md border bg-gray-50 p-3">
          <label className="flex flex-col text-xs font-medium text-gray-700">
            Scheduled date
            <input
              type="datetime-local"
              value={scheduledDate}
              onChange={(e) => setScheduledDate(e.target.value)}
              className="mt-1 rounded-md border border-gray-300 px-3 py-1.5 text-sm shadow-sm"
            />
          </label>
          <div className="w-72">
            <EmployeeAutocomplete
              organizationId={organizationId}
              name="assigneeId"
              label="Assignee"
              required
              value={assignee}
              onSelect={setAssignee}
            />
          </div>
          <button
            type="button"
            onClick={doSchedule}
            className="rounded-md bg-brand-500 px-3 py-1.5 text-sm text-white hover:bg-brand-600"
          >
            Confirm
          </button>
          <button
            type="button"
            onClick={() => setShowSchedule(false)}
            className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm"
          >
            Dismiss
          </button>
        </div>
      )}

      {showCancel && (
        <div className="flex flex-wrap items-end gap-2 rounded-md border bg-red-50 p-3">
          <label className="flex flex-col text-xs font-medium text-red-900">
            Reason
            <input
              type="text"
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder="Customer requested cancellation"
              className="mt-1 w-72 rounded-md border border-red-300 px-3 py-1.5 text-sm shadow-sm"
            />
          </label>
          <button
            type="button"
            onClick={doCancel}
            className="rounded-md bg-red-600 px-3 py-1.5 text-sm text-white hover:bg-red-700"
          >
            Cancel job
          </button>
          <button
            type="button"
            onClick={() => setShowCancel(false)}
            className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm"
          >
            Dismiss
          </button>
        </div>
      )}

      {showComplete && (
        <CompleteJobDialog
          organizationId={organizationId}
          onClose={() => setShowComplete(false)}
        />
      )}
    </section>
  );
}
