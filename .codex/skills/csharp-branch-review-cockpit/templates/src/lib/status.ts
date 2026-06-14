import type { Status } from './types';

// 変更種別の色。A=追加(緑) / M=変更(橙) / D=削除(赤)。
export const STATUS_COLOR: Record<Status, string> = {
  A: '#3fb950',
  M: '#d29922',
  D: '#f85149',
};
export const statusColor = (s: Status) => STATUS_COLOR[s] ?? '#8b949e';
export const STATUS_LABEL: Record<Status, string> = { A: 'added', M: 'modified', D: 'deleted' };
