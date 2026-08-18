import { Suspense } from 'react';
import { CreateJobForm } from '@features/create-job';
import {
  JobsFilter,
  JobsFilterStatus,
  JobsFilterSearch,
  JobsFilterDateRange,
  JobsFilterReset,
} from '@features/filter-jobs';
import { JobsList } from '@widgets/jobs-list';
import { serverEnv } from '@shared/config/env';

type SearchParams = {
  status?: string;
  cursor?: string;
  q?: string;
  scheduledFrom?: string;
  scheduledTo?: string;
};

export default async function JobsPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const orgId = serverEnv.DEFAULT_ORG_ID;

  return (
    <main className="mx-auto flex max-w-5xl flex-col gap-6 p-6">
      <header className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-900">Jobs</h1>
      </header>

      <JobsFilter>
        <JobsFilterStatus />
        <JobsFilterSearch />
        <JobsFilterDateRange />
        <JobsFilterReset />
      </JobsFilter>

      {orgId && (
        <section>
          <CreateJobForm organizationId={orgId} />
        </section>
      )}

      <section>
        <Suspense
          key={JSON.stringify(params)}
          fallback={<div className="text-sm text-gray-500">Loading jobs…</div>}
        >
          <JobsList
            status={params.status}
            cursor={params.cursor}
            q={params.q}
            scheduledFrom={params.scheduledFrom}
            scheduledTo={params.scheduledTo}
          />
        </Suspense>
      </section>
    </main>
  );
}
