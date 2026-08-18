import { z } from 'zod';

export const createJobFormSchema = z.object({
  title: z.string().min(1, 'Title is required').max(200),
  description: z.string().max(4000).default(''),
  customerId: z.string().uuid('Customer id must be a UUID'),
  street: z.string().min(1, 'Street is required').max(200),
  city: z.string().min(1, 'City is required').max(120),
  state: z.string().min(1, 'State is required').max(60),
  zipCode: z
    .string()
    .trim()
    .regex(
      /^[A-Za-z0-9][A-Za-z0-9\- ]{2,9}$/,
      'Postal code must be 3–10 alphanumeric characters (spaces and hyphens allowed).',
    ),
});

export type CreateJobFormValues = z.input<typeof createJobFormSchema>;
export type CreateJobFormParsed = z.output<typeof createJobFormSchema>;
