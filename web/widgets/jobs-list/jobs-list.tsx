import Link from 'next/link';
import { listJobs } from '@entities/job/api';
import { getCustomersByIds } from '@entities/customer/api';
import { getEmployeesByIds } from '@entities/employee/api';
import { serverEnv } from '@shared/config/env';
import { StatusBadge } from '@shared/ui/badge';

type Props = {
  status?: string;
  cursor?: string;
  q?: string;
  scheduledFrom?: string;
  scheduledTo?: string;
};

export async function JobsList({ status, cursor, q, scheduledFrom, scheduledTo }: Props) {
  if (!serverEnv.DEFAULT_ORG_ID) {
    return (
      <div className="rounded-md border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900">
        <p className="font-semibold">DEFAULT_ORG_ID is not configured.</p>
        <p>Add it to <code className="rounded bg-amber-100 px-1">.env.local</code> to load jobs.</p>
      </div>
    );
  }

  const page = await listJobs(serverEnv.DEFAULT_ORG_ID, {
    status,
    cursor,
    q,
    scheduledFrom,
    scheduledTo,
    pageSize: 25,
  });

  if (page.items.length === 0) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">
        No jobs yet. Create your first one using the form above.
      </div>
    );
  }

  const customerIds = Array.from(new Set(page.items.map((j) => j.customerId)));
  const assigneeIds = Array.from(
    new Set(page.items.map((j) => j.assigneeId).filter((v): v is string => !!v)),
  );
  const [customers, employees] = await Promise.all([
    getCustomersByIds(serverEnv.DEFAULT_ORG_ID, customerIds),
    getEmployeesByIds(serverEnv.DEFAULT_ORG_ID, assigneeIds),
  ]);
  const customerNameById = new Map(customers.map((c) => [c.id, c.name]));
  const employeeNameById = new Map(employees.map((e) => [e.id, e.name]));

  return (
    <div className="overflow-hidden rounded-md border border-slate-200 bg-white shadow-sm">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <thead className="bg-slate-50">
          <tr>
            <th className="px-4 py-2 text-left font-medium text-slate-700">Title</th>
            <th className="px-4 py-2 text-left font-medium text-slate-700">Customer</th>
            <th className="px-4 py-2 text-left font-medium text-slate-700">Assignee</th>
            <th className="px-4 py-2 text-left font-medium text-slate-700">Status</th>
            <th className="px-4 py-2 text-left font-medium text-slate-700">Scheduled</th>
            <th className="px-4 py-2 text-left font-medium text-slate-700">Created</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {page.items.map((job) => (
            <tr key={job.id} className="hover:bg-brand-50/40">
              <td className="px-4 py-2">
                <Link href={`/jobs/${job.id}`} className="font-medium text-brand-700 hover:text-brand-800 hover:underline">
                  {job.title}
                </Link>
              </td>
              <td className="px-4 py-2 text-slate-800">
                {customerNameById.get(job.customerId) ?? <span className="text-slate-400">Unknown</span>}
              </td>
              <td className="px-4 py-2 text-slate-800">
                {job.assigneeId
                  ? (employeeNameById.get(job.assigneeId) ?? <span className="text-slate-400">Unknown</span>)
                  : <span className="text-slate-400">—</span>}
              </td>
              <td className="px-4 py-2">
                <StatusBadge status={job.status} />
              </td>
              <td className="px-4 py-2 text-slate-600">
                {job.scheduledDate ? new Date(job.scheduledDate).toLocaleString() : '—'}
              </td>
              <td className="px-4 py-2 text-slate-600">
                {new Date(job.createdAt).toLocaleString()}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {page.nextCursor && (
        <div className="border-t bg-slate-50 px-4 py-2 text-right text-xs">
          <Link
            href={{
              pathname: '/jobs',
              query: {
                ...(status ? { status } : {}),
                ...(q ? { q } : {}),
                ...(scheduledFrom ? { scheduledFrom } : {}),
                ...(scheduledTo ? { scheduledTo } : {}),
                cursor: page.nextCursor,
              },
            }}
            className="text-brand-700 hover:text-brand-800 hover:underline"
          >
            Load more &rarr;
          </Link>
        </div>
      )}
    </div>
  );
}
