import { bundlePrefix, type PlaytestKind } from "./keys";

export const READY_MARKER = "READY";
export const ACKED_MARKER = "ACKED";
export const DECLARED_MARKER = "DECLARED";
export const PURGED_MARKER = "PURGED";
export const IMPORT_DECLARED_MARKER = "IMPORT_DECLARED";

export function ackedMarkerKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${bundlePrefix(kind, steamId, id)}/${ACKED_MARKER}`;
}

// prepareの宣言置き場。completeが照合に使う
// Where prepare stores its declaration; complete uses it for verification
export function declaredMarkerKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${bundlePrefix(kind, steamId, id)}/${DECLARED_MARKER}`;
}

// zip取り込みの宣言置き場。import/completeが照合に使う（prepareのDECLAREDとは上限も世代も違うので分ける）
// Where a zip import stores its declaration for import/complete; kept apart from prepare's DECLARED (different limits, no generation)
export function importDeclaredMarkerKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${bundlePrefix(kind, steamId, id)}/${IMPORT_DECLARED_MARKER}`;
}

// 取り込み済みかの正本はACKEDマーカーだけ。索引やREADYの有無からは推測しない
// The ACKED marker is the only source of truth for "already ingested"; never infer it from the index or READY
export async function isAcked(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<boolean> {
  const parent = id.split("/__attachments/")[0] as string;
  return (await bucket.head(ackedMarkerKey(kind, steamId, parent))) !== null;
}

// 片付けで生データを消した箱の印。読む側が「未着」と「削除済み」を区別するために残す
// Marks a box whose raw data was purged; kept so readers can tell "never arrived" from "deleted on cleanup"
export function purgedMarkerKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${bundlePrefix(kind, steamId, id)}/${PURGED_MARKER}`;
}

export type PurgedState = { purged: false } | { purged: true; purgedAt: string | null };

// PURGEDの有無と削除日時。印は自分で書いたJSONだがR2上は外部入力（パース例外の境界）で、壊れていても削除済みとして扱う
// Whether PURGED exists and when; we wrote the JSON but an R2 object is external input (parse boundary), so a broken one still counts as purged
export async function readPurged(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<PurgedState> {
  const key = purgedMarkerKey(kind, steamId, id);
  const object = await bucket.get(key);
  if (object === null) return { purged: false };
  try {
    const body = (await object.json()) as { purgedAt?: unknown };
    if (typeof body?.purgedAt === "string") return { purged: true, purgedAt: body.purgedAt };
  } catch {
    // 下のwarnへ合流する（読めない印も削除済みとして返す）
    // Falls through to the warn below (an unreadable marker still reports purged)
  }
  console.warn(`[inbox] PURGED marker has no readable purgedAt: ${key}`);
  return { purged: true, purgedAt: null };
}
