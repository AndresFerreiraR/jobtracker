'use client';

import { createContext, useCallback, useContext, useMemo, useTransition, type ReactNode } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { JOB_STATUSES } from '@entities/job';

type FilterContextValue = {
  values: {
    status: string;
    q: string;
    scheduledFrom: string;
    scheduledTo: string;
  };
  isPending: boolean;
  set: (key: keyof FilterContextValue['values'], value: string) => void;
  reset: () => void;
};

const FilterContext = createContext<FilterContextValue | null>(null);

function useFilter(): FilterContextValue {
  const ctx = useContext(FilterContext);
  if (!ctx) throw new Error('JobsFilter.* must be used inside <JobsFilter>.');
  return ctx;
}

type RootProps = { children: ReactNode; className?: string };

export function JobsFilter({ children, className }: RootProps) {
  const router = useRouter();
  const params = useSearchParams();
  const [isPending, startTransition] = useTransition();

  const values = useMemo(() => ({
    status: params.get('status') ?? '',
    q: params.get('q') ?? '',
    scheduledFrom: params.get('scheduledFrom') ?? '',
    scheduledTo: params.get('scheduledTo') ?? '',
  }), [params]);

  const push = useCallback((next: URLSearchParams) => {
    next.delete('cursor');
    startTransition(() => router.replace(`/jobs?${next.toString()}`));
  }, [router]);

  const set: FilterContextValue['set'] = useCallback((key, value) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    push(next);
  }, [params, push]);

  const reset = useCallback(() => push(new URLSearchParams()), [push]);

  const ctx: FilterContextValue = { values, isPending, set, reset };

  return (
    <FilterContext.Provider value={ctx}>
      <div className={className ?? 'flex flex-wrap items-end gap-3'}>{children}</div>
    </FilterContext.Provider>
  );
}

export function JobsFilterStatus() {
  const { values, isPending, set } = useFilter();
  return (
    <label className="flex flex-col text-xs font-medium text-gray-700">
      Status
      <select
        value={values.status}
        disabled={isPending}
        onChange={(e) => set('status', e.target.value)}
        className="mt-1 rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-900 shadow-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
      >
        <option value="">All</option>
        {JOB_STATUSES.map((s) => (
          <option key={s} value={s}>{s}</option>
        ))}
      </select>
    </label>
  );
}

export function JobsFilterSearch() {
  const { values, isPending, set } = useFilter();
  return (
    <label className="flex flex-col text-xs font-medium text-gray-700">
      Search
      <input
        type="search"
        defaultValue={values.q}
        placeholder="Title or customer…"
        disabled={isPending}
        onKeyDown={(e) => {
          if (e.key === 'Enter') set('q', (e.target as HTMLInputElement).value.trim());
        }}
        onBlur={(e) => set('q', e.target.value.trim())}
        className="mt-1 rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm text-slate-900 placeholder:text-slate-400 shadow-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
      />
    </label>
  );
}

export function JobsFilterDateRange() {
  const { values, isPending, set } = useFilter();
  return (
    <div className="flex flex-wrap items-end gap-2">
      <label className="flex flex-col text-xs font-medium text-gray-700">
        Scheduled from
        <input
          type="date"
          value={values.scheduledFrom}
          disabled={isPending}
          onChange={(e) => set('scheduledFrom', e.target.value)}
          className="mt-1 rounded-md border border-gray-300 px-3 py-1.5 text-sm shadow-sm"
        />
      </label>
      <label className="flex flex-col text-xs font-medium text-gray-700">
        Scheduled to
        <input
          type="date"
          value={values.scheduledTo}
          disabled={isPending}
          onChange={(e) => set('scheduledTo', e.target.value)}
          className="mt-1 rounded-md border border-gray-300 px-3 py-1.5 text-sm shadow-sm"
        />
      </label>
    </div>
  );
}

export function JobsFilterReset() {
  const { isPending, reset, values } = useFilter();
  const hasFilters = Object.values(values).some(Boolean);
  return (
    <button
      type="button"
      disabled={isPending || !hasFilters}
      onClick={reset}
      className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm hover:bg-gray-50 disabled:opacity-50"
    >
      Reset
    </button>
  );
}


