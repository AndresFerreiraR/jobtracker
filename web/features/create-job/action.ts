'use server';

import { revalidatePath } from 'next/cache';
import { createJob } from '@entities/job/api';
import { serverEnv } from '@shared/config/env';
import { ApiError } from '@shared/api';
import { createJobFormSchema } from './schema';

export type CreateJobFormValues = {
  title?: string;
  description?: string;
  customerId?: string;
  street?: string;
  city?: string;
  state?: string;
  zipCode?: string;
};

export type CreateJobActionState =
  | { status: 'idle' }
  | { status: 'success'; jobId: string }
  | {
      status: 'error';
      message: string;
      fieldErrors?: Record<string, string>;
      values?: CreateJobFormValues;
    };

function stringValues(fd: FormData): CreateJobFormValues {
  const pick = (k: string) => {
    const v = fd.get(k);
    return typeof v === 'string' ? v : undefined;
  };
  return {
    title: pick('title'),
    description: pick('description'),
    customerId: pick('customerId'),
    street: pick('street'),
    city: pick('city'),
    state: pick('state'),
    zipCode: pick('zipCode'),
  };
}

export async function createJobAction(
  _prev: CreateJobActionState,
  formData: FormData,
): Promise<CreateJobActionState> {
  const raw = Object.fromEntries(formData.entries());
  const values = stringValues(formData);
  const parsed = createJobFormSchema.safeParse(raw);

  if (!parsed.success) {
    const fieldErrors: Record<string, string> = {};
    for (const issue of parsed.error.issues) {
      const key = issue.path.join('.') || 'form';
      if (!fieldErrors[key]) fieldErrors[key] = issue.message;
    }
    return { status: 'error', message: 'Validation failed', fieldErrors, values };
  }

  const orgId = serverEnv.DEFAULT_ORG_ID;
  if (!orgId) {
    return {
      status: 'error',
      message: 'DEFAULT_ORG_ID env var is not configured on the server.',
      values,
    };
  }

  try {
    const created = await createJob(orgId, {
      title: parsed.data.title,
      description: parsed.data.description,
      customerId: parsed.data.customerId,
      address: {
        street: parsed.data.street,
        city: parsed.data.city,
        state: parsed.data.state,
        zipCode: parsed.data.zipCode,
        latitude: null,
        longitude: null,
      },
    });

    revalidatePath('/jobs');
    return { status: 'success', jobId: created.id };
  } catch (e) {
    if (e instanceof ApiError) {
      return {
        status: 'error',
        message: e.problem.detail ?? e.problem.title,
        values,
      };
    }
    return { status: 'error', message: 'Unexpected error creating job.', values };
  }
}
