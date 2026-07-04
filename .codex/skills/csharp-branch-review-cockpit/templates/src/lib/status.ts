import type { Status } from './types';

const COLOR: Record<Status, string> = {
  A: '#22C55E',
  M: '#F59E0B',
  D: '#EF4444',
};

export const statusColor = (s: Status): string => COLOR[s];
