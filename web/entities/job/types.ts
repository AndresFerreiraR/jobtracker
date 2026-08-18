export const JOB_STATUSES = [
  'Draft',
  'Scheduled',
  'InProgress',
  'Completed',
  'Cancelled',
] as const;

export type JobStatus = (typeof JOB_STATUSES)[number];

export type JobAddress = {
  street: string;
  city: string;
  state: string;
  zipCode: string;
  latitude: number | null;
  longitude: number | null;
};

export type JobPhoto = {
  id: string;
  url: string;
  capturedAt: string;
  caption: string | null;
};

export type JobListItem = {
  id: string;
  title: string;
  status: JobStatus;
  customerId: string;
  assigneeId: string | null;
  scheduledDate: string | null;
  createdAt: string;
};

export type JobDetails = {
  id: string;
  title: string;
  description: string;
  address: JobAddress;
  status: JobStatus;
  scheduledDate: string | null;
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  signatureUrl: string | null;
  assigneeId: string | null;
  customerId: string;
  createdAt: string;
  updatedAt: string;
  photos: JobPhoto[];
};

export type PagedJobs = {
  items: JobListItem[];
  nextCursor: string | null;
};

type Transitions = {
  Draft: 'Scheduled' | 'Cancelled';
  Scheduled: 'InProgress' | 'Cancelled';
  InProgress: 'Completed' | 'Cancelled';
  Completed: never;
  Cancelled: never;
};

export type NextStatus<S extends JobStatus> = Transitions[S];

export function canTransition<S extends JobStatus>(
  from: S,
  to: JobStatus,
): to is NextStatus<S> {
  const map: Record<JobStatus, readonly JobStatus[]> = {
    Draft: ['Scheduled', 'Cancelled'],
    Scheduled: ['InProgress', 'Cancelled'],
    InProgress: ['Completed', 'Cancelled'],
    Completed: [],
    Cancelled: [],
  };
  return map[from].includes(to);
}
