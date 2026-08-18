'use client';

import { useState } from 'react';
import { completeJob } from '@entities/job/api.client';
import { useJobStore } from '@entities/job';
import { ApiError } from '@shared/api';
import { toast } from '@shared/ui/toast';
import { SignaturePad } from './signature-pad';
import { uploadSignatureDataUrl } from './upload-signature';

type Props = {
  organizationId: string;
  onClose: () => void;
};

export function CompleteJobDialog({ organizationId, onClose }: Props) {
  const job = useJobStore((s) => s.job);
  const optimistic = useJobStore((s) => s.optimistic);
  const [signature, setSignature] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!job) return null;

  const submit = async () => {
    if (!signature) {
      toast.error('Signature required', 'Please capture a signature before completing the job.');
      return;
    }
    setSubmitting(true);
    const idempotencyKey = crypto.randomUUID();
    try {
      const signatureUrl = await uploadSignatureDataUrl(organizationId, signature);
      const completedAt = new Date().toISOString();
      await optimistic(
        'complete',
        (draft) => ({
          ...draft,
          status: 'Completed',
          completedAt,
          signatureUrl,
        }),
        () => completeJob(
          organizationId,
          job.id,
          { signatureUrl, completedAt },
          idempotencyKey,
        ),
      );
      toast.success('Job completed', `${job.title} is now marked as completed.`);
      onClose();
    } catch (e) {
      const msg = e instanceof ApiError ? (e.problem.detail ?? e.problem.title) : (e as Error).message;
      toast.error('Could not complete job', msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="complete-title"
      className="fixed inset-0 z-40 flex items-center justify-center bg-black/50 p-4"
      onClick={onClose}
    >
      <div
        className="w-full max-w-lg rounded-md bg-white p-6 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <h2 id="complete-title" className="mb-2 text-lg font-semibold text-gray-900">
          Complete job
        </h2>
        <p className="mb-3 text-sm text-gray-600">
          Capture the customer signature to close out <strong>{job.title}</strong>.
        </p>
        <SignaturePad onChange={setSignature} />
        <div className="mt-4 flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm hover:bg-gray-50"
            disabled={submitting}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={submit}
            disabled={!signature || submitting}
            className="rounded-md bg-emerald-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-emerald-700 disabled:bg-emerald-300"
          >
            {submitting ? 'Completing…' : 'Complete job'}
          </button>
        </div>
      </div>
    </div>
  );
}
