import type { PlaytestKind } from "../keys";

// SteamIDと衝突しない接頭辞で開発機を識別する
// Identify developer machines with a prefix that never collides with numeric Steam IDs
export const DEVELOPER_SUBJECT_PREFIX = "dev-";
export const MACHINE_PATTERN = /^[a-z0-9][a-z0-9-]{0,31}$/;

export function developerSubjectOf(machine: string): string {
  return `${DEVELOPER_SUBJECT_PREFIX}${machine}`;
}

export function isDeveloperSubject(sub: string): boolean {
  return sub.startsWith(DEVELOPER_SUBJECT_PREFIX);
}

// 開発機の進行記録をテスター集計へ混ぜない
// Keep developer progress records out of tester statistics
export function refusesDeveloperProgress(kind: PlaytestKind, sub: string): boolean {
  if (kind !== "progress" || !isDeveloperSubject(sub)) return false;
  console.warn(`[uploads] refused: progress records are not accepted from developer machine ${sub}`);
  return true;
}
