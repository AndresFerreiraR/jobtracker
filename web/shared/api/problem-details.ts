import { z } from 'zod';

export const problemDetailsSchema = z.object({
  status: z.number().int(),
  title: z.string(),
  type: z.string().optional(),
  detail: z.string().optional(),
  errorCode: z.string().optional(),
});

export type ProblemDetails = z.infer<typeof problemDetailsSchema>;

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ProblemDetails,
  ) {
    super(problem.detail ?? problem.title);
    this.name = 'ApiError';
  }
}
