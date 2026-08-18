import type { JobStatus } from '@entities/job/types';

const statusStyles: Record<JobStatus, string> = {
  Draft: 'bg-slate-100 text-slate-700 ring-1 ring-inset ring-slate-200',
  Scheduled: 'bg-brand-50 text-brand-800 ring-1 ring-inset ring-brand-200',
  InProgress: 'bg-accent-50 text-accent-700 ring-1 ring-inset ring-accent-200',
  Completed: 'bg-emerald-50 text-emerald-800 ring-1 ring-inset ring-emerald-200',
  Cancelled: 'bg-red-50 text-red-800 ring-1 ring-inset ring-red-200',
};

export function StatusBadge({ status }: { status: JobStatus }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${statusStyles[status]}`}
    >
      {status}
    </span>
  );
}
