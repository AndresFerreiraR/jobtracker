import { describe, it, expect } from 'vitest';
import { canTransition, type NextStatus } from './types';

describe('Job state machine (client)', () => {
  it('Draft can transition to Scheduled and Cancelled', () => {
    expect(canTransition('Draft', 'Scheduled')).toBe(true);
    expect(canTransition('Draft', 'Cancelled')).toBe(true);
    expect(canTransition('Draft', 'InProgress')).toBe(false);
    expect(canTransition('Draft', 'Completed')).toBe(false);
  });

  it('Terminal statuses cannot transition anywhere', () => {
    expect(canTransition('Completed', 'Cancelled')).toBe(false);
    expect(canTransition('Cancelled', 'Draft')).toBe(false);
  });

  it('InProgress can only reach Completed or Cancelled', () => {
    expect(canTransition('InProgress', 'Completed')).toBe(true);
    expect(canTransition('InProgress', 'Cancelled')).toBe(true);
    expect(canTransition('InProgress', 'Draft')).toBe(false);
  });

  it('NextStatus<S> narrows valid transitions at the type level', () => {
    const draftNext: NextStatus<'Draft'> = 'Scheduled';
    const scheduledNext: NextStatus<'Scheduled'> = 'InProgress';
    expect(draftNext).toBe('Scheduled');
    expect(scheduledNext).toBe('InProgress');
  });
});
