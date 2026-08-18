import { z } from 'zod';

const publicSchema = z.object({
  NEXT_PUBLIC_API_BASE_URL: z.string().url(),
});

const serverSchema = z.object({
  API_BASE_URL: z.string().url().optional(),
  DEFAULT_ORG_ID: z.string().uuid().optional(),
});

export const publicEnv = publicSchema.parse({
  NEXT_PUBLIC_API_BASE_URL:
    process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5000',
});

export const serverEnv = serverSchema.parse({
  API_BASE_URL: process.env.API_BASE_URL,
  DEFAULT_ORG_ID: process.env.DEFAULT_ORG_ID,
});

export const apiBaseUrl = (): string =>
  serverEnv.API_BASE_URL ?? publicEnv.NEXT_PUBLIC_API_BASE_URL;
