// R2のプレフィックス表。共有契約§4の「{kind}s」を字義適用すると progresss になるため表で固定する
// Prefix table for R2; the contract's literal "{kind}s" would yield "progresss", so the mapping is pinned here
export const KIND_PREFIX = { report: "reports", progress: "progress" } as const;

export type PlaytestKind = keyof typeof KIND_PREFIX;

const PENDING_ROOT = "index/pending";
// キーはR2のオブジェクト名でUTF-8が通る。逸脱と制御文字だけを拒み、報告バンドル内の実ファイル名をそのまま残す
// Keys are R2 object names and accept UTF-8, so only traversal and control characters are rejected, keeping real file names intact
const UNSAFE_SEGMENT_PATTERN = /[/\\\u0000-\u001f\u007f]/;

export function isKind(value: string): value is PlaytestKind {
  return value === "report" || value === "progress";
}

export function bundlePrefix(kind: PlaytestKind, steamId: string, id: string): string {
  return `${KIND_PREFIX[kind]}/${steamId}/${id}`;
}

export function pendingIndexKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${PENDING_ROOT}/${kind}/${steamId}/${id}`;
}

export function parsePendingIndexKey(key: string): { kind: PlaytestKind; steamId: string; id: string } | null {
  const segments = key.split("/");
  if (segments.length !== 5) return null;
  if (segments[0] !== "index" || segments[1] !== "pending") return null;
  const [, , kind, steamId, id] = segments as [string, string, string, string, string];
  if (!isKind(kind)) return null;
  return { kind, steamId, id };
}

// 逸脱の入口はここ1箇所。「.」「..」「空」「区切り文字混入」を弾けば連結後のキーは必ずprefix配下に入る
// This is the only entry point for traversal; rejecting ".", "..", empty and separators keeps every key under the prefix
export function isSafeSegment(segment: string): boolean {
  if (segment.length === 0 || segment === "." || segment === "..") return false;
  return !UNSAFE_SEGMENT_PATTERN.test(segment);
}

export function joinSafePath(segments: string[]): string | null {
  if (segments.length === 0) return null;
  for (const segment of segments) {
    if (!isSafeSegment(segment)) return null;
  }
  return segments.join("/");
}
