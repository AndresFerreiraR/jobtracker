'use client';

import { useEffect } from 'react';

export default function JobsError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error('Jobs route error', error);
  }, [error]);

  return (
    <section className="mx-auto flex max-w-2xl flex-col items-start gap-3 rounded-md border border-red-200 bg-red-50 p-6">
      <h2 className="text-xl font-semibold text-red-900">Could not load jobs</h2>
      <p className="text-sm text-red-800">{error.message}</p>
      <button
        type="button"
        onClick={reset}
        className="rounded-md bg-red-600 px-3 py-1.5 text-sm text-white hover:bg-red-700"
      >
        Retry
      </button>
    </section>
  );
}
