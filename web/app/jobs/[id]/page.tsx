import Link from 'next/link';
import { notFound } from 'next/navigation';
import { getJob } from '@entities/job/api';
import { getCustomersByIds } from '@entities/customer/api';
import { getEmployeesByIds } from '@entities/employee/api';
import { ApiError } from '@shared/api';
import { serverEnv } from '@shared/config/env';
import { StatusBadge } from '@shared/ui/badge';
import { JobActions } from '@widgets/job-actions';

type Params = { id: string };

export default async function JobDetailsPage({
  params,
}: {
  params: Promise<Params>;
}) {
  const { id } = await params;
  if (!serverEnv.DEFAULT_ORG_ID) {
    return (
      <main className="p-6 text-sm text-amber-800">
        DEFAULT_ORG_ID is not configured.
      </main>
    );
  }

  try {
    const job = await getJob(serverEnv.DEFAULT_ORG_ID, id);
    const [customersRes, employeesRes] = await Promise.all([
      getCustomersByIds(serverEnv.DEFAULT_ORG_ID, [job.customerId]),
      job.assigneeId ? getEmployeesByIds(serverEnv.DEFAULT_ORG_ID, [job.assigneeId]) : Promise.resolve([]),
    ]);
    const customer = customersRes[0];
    const assignee = employeesRes[0];
    return (
      <main className="mx-auto flex max-w-3xl flex-col gap-4 p-6">
        <Link href="/jobs" className="text-sm text-brand-700 hover:text-brand-800 hover:underline">
          &larr; Back to list
        </Link>

        <header className="flex items-center gap-3">
          <h1 className="text-2xl font-bold text-slate-900">{job.title}</h1>
          <StatusBadge status={job.status} />
        </header>

        <p className="text-slate-700">{job.description || <em>No description</em>}</p>

        <JobActions organizationId={serverEnv.DEFAULT_ORG_ID} job={job} />

        <dl className="grid grid-cols-2 gap-4 rounded-md border border-slate-200 bg-white p-4 text-sm">
          <div>
            <dt className="font-medium text-slate-500">Address</dt>
            <dd className="text-slate-900">
              {job.address.street}, {job.address.city}, {job.address.state} {job.address.zipCode}
            </dd>
          </div>
          <div>
            <dt className="font-medium text-slate-500">Customer</dt>
            <dd className="text-slate-900">
              {customer?.name ?? <span className="font-mono text-xs text-slate-500">{job.customerId}</span>}
            </dd>
          </div>
          <div>
            <dt className="font-medium text-slate-500">Scheduled</dt>
            <dd className="text-slate-900">
              {job.scheduledDate ? new Date(job.scheduledDate).toLocaleString() : '—'}
            </dd>
          </div>
          <div>
            <dt className="font-medium text-slate-500">Assignee</dt>
            <dd className="text-slate-900">
              {assignee?.name
                ?? (job.assigneeId
                  ? <span className="font-mono text-xs text-slate-500">{job.assigneeId}</span>
                  : '—')}
            </dd>
          </div>
        </dl>

        {job.photos.length > 0 && (
          <section>
            <h2 className="mb-2 text-lg font-semibold text-slate-900">Photos</h2>
            <ul className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              {job.photos.map((p) => (
                <li key={p.id} className="overflow-hidden rounded border border-slate-200 bg-white">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={p.url} alt={p.caption ?? ''} className="h-32 w-full object-cover" />
                  {p.caption && (
                    <p className="p-2 text-xs text-slate-600">{p.caption}</p>
                  )}
                </li>
              ))}
            </ul>
          </section>
        )}
      </main>
    );
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) notFound();
    throw e;
  }
}
