import { bundlePrefix, type PlaytestKind } from "./keys";

export const READY_MARKER = "READY";
export const ACKED_MARKER = "ACKED";

export function ackedMarkerKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${bundlePrefix(kind, steamId, id)}/${ACKED_MARKER}`;
}

// 取り込み済みかの正本はACKEDマーカーだけ。索引やREADYの有無からは推測しない
// The ACKED marker is the only source of truth for "already ingested"; never infer it from the index or READY
export async function isAcked(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<boolean> {
  return (await bucket.head(ackedMarkerKey(kind, steamId, id))) !== null;
}
