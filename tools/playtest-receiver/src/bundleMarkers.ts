import { bundlePrefix, type PlaytestKind } from "./keys";

export const READY_MARKER = "READY";
export const ACKED_MARKER = "ACKED";
export const DECLARED_MARKER = "DECLARED";

export function ackedMarkerKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${bundlePrefix(kind, steamId, id)}/${ACKED_MARKER}`;
}

// prepareの宣言置き場。completeが照合に使う
// Where prepare stores its declaration; complete uses it for verification
export function declaredMarkerKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${bundlePrefix(kind, steamId, id)}/${DECLARED_MARKER}`;
}

// 取り込み済みかの正本はACKEDマーカーだけ。索引やREADYの有無からは推測しない
// The ACKED marker is the only source of truth for "already ingested"; never infer it from the index or READY
export async function isAcked(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<boolean> {
  return (await bucket.head(ackedMarkerKey(kind, steamId, id))) !== null;
}
