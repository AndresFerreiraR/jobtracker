'use client';

import { useEffect } from 'react';

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error('Unhandled route error', error);
  }, [error]);

  return (
    <main className="mx-auto flex max-w-lg flex-col items-start gap-4 p-8">
      <h1 className="text-2xl font-bold text-red-800">Something went wrong</h1>
      <p className="text-sm text-gray-700">
        {error.message || 'An unexpected error occurred.'}
      </p>
      {error.digest && (
        <p className="text-xs text-gray-500">Ref: {error.digest}</p>
      )}
      <button
        type="button"
        onClick={reset}
        className="rounded-md bg-brand-500 px-4 py-2 text-sm font-medium text-white hover:bg-brand-600"
      >
        Try again
      </button>
    </main>
  );
}
