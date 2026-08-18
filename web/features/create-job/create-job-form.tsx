'use client';

import { useActionState, useEffect, useState } from 'react';
import { Button } from '@shared/ui/button';
import { Input } from '@shared/ui/input';
import { CustomerAutocomplete } from '@features/customer-picker';
import {
  createJobAction,
  type CreateJobActionState,
} from './action';

const initialState: CreateJobActionState = { status: 'idle' };

type FieldValues = {
  title: string;
  description: string;
  street: string;
  city: string;
  state: string;
  zipCode: string;
};

const EMPTY_FIELDS: FieldValues = {
  title: '',
  description: '',
  street: '',
  city: '',
  state: '',
  zipCode: '',
};

export function CreateJobForm({ organizationId }: { organizationId: string }) {
  const [state, formAction, isPending] = useActionState(createJobAction, initialState);
  const [fields, setFields] = useState<FieldValues>(EMPTY_FIELDS);

  useEffect(() => {
    if (state.status === 'error' && state.values) {
      setFields((prev) => ({
        title: state.values?.title ?? prev.title,
        description: state.values?.description ?? prev.description,
        street: state.values?.street ?? prev.street,
        city: state.values?.city ?? prev.city,
        state: state.values?.state ?? prev.state,
        zipCode: state.values?.zipCode ?? prev.zipCode,
      }));
    } else if (state.status === 'success') {
      setFields(EMPTY_FIELDS);
    }
  }, [state]);

  const fieldError = (name: string): string | undefined =>
    state.status === 'error' ? state.fieldErrors?.[name] : undefined;

  const bind = (key: keyof FieldValues) => ({
    value: fields[key],
    onChange: (e: React.ChangeEvent<HTMLInputElement>) =>
      setFields((prev) => ({ ...prev, [key]: e.target.value })),
  });

  return (
    <form action={formAction} className="space-y-4 rounded-lg border border-[color:var(--color-border)] bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-slate-900">Create a new job</h2>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Input name="title" label="Title" error={fieldError('title')} required {...bind('title')} />
        <CustomerAutocomplete
          organizationId={organizationId}
          name="customerId"
          label="Customer"
          required
          error={fieldError('customerId')}
        />
      </div>

      <Input name="description" label="Description" error={fieldError('description')} {...bind('description')} />

      <fieldset className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <legend className="col-span-full text-sm font-semibold text-slate-700">Address</legend>
        <Input name="street" label="Street" error={fieldError('street')} required {...bind('street')} />
        <Input name="city" label="City" error={fieldError('city')} required {...bind('city')} />
        <Input name="state" label="State" error={fieldError('state')} required {...bind('state')} />
        <Input name="zipCode" label="Postal code" error={fieldError('zipCode')} required {...bind('zipCode')} />
      </fieldset>

      {state.status === 'error' && !state.fieldErrors && (
        <p role="alert" className="text-sm text-red-600">
          {state.message}
        </p>
      )}
      {state.status === 'success' && (
        <p role="status" className="text-sm text-emerald-700">
          Job created: {state.jobId}
        </p>
      )}

      <div className="flex justify-end">
        <Button type="submit" isLoading={isPending}>
          Create job
        </Button>
      </div>
    </form>
  );
}
