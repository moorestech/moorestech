import type { DeclaredFile } from "../uploads/bundleDeclaration";
import { bundlePrefix, type PlaytestKind } from "../keys";

export interface MissingObject {
  path: string;
  expectedBytes: number;
  actualBytes: number | null;
}

export interface VerifiedObjects {
  present: string[];
  missing: MissingObject[];
}

// 箱配下の実オブジェクトの長さ（箱からの相対パス→bytes）。prefix単位のlistで引き、ファイルごとのheadはしない（無料プランのCPU上限）
// Sizes of a box's real objects (box-relative path -> bytes); one prefix list, never a head per file (free-plan CPU limit)
export async function listBundleObjectSizes(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<Map<string, number>> {
  const prefix = `${bundlePrefix(kind, steamId, id)}/`;
  const sizes = new Map<string, number>();
  let cursor: string | undefined;
  do {
    const page = await bucket.list({ prefix, cursor, limit: 1000 });
    for (const object of page.objects) sizes.set(object.key.slice(prefix.length), object.size);
    cursor = page.truncated ? page.cursor : undefined;
  } while (cursor !== undefined);
  return sizes;
}

// 宣言と実オブジェクトの照合。存在と長さだけを見る（内容の検査は取り込み側の仕事）。宣言に無いキーは無視する
// Verifies the declaration against real objects by presence and size only (content checks belong to ingest); undeclared keys are ignored
export async function verifyDeclaredObjects(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string, declared: DeclaredFile[]): Promise<VerifiedObjects> {
  const sizes = await listBundleObjectSizes(bucket, kind, steamId, id);

  // 宣言ごとに有無と長さで仕分ける。presentはREADYのfilesを安定させるため整列する
  // Classify each declared file by presence and size; present is sorted so READY's files stays stable
  const present: string[] = [];
  const missing: MissingObject[] = [];
  for (const file of declared) {
    const actual = sizes.get(file.path);
    if (actual === file.bytes) present.push(file.path);
    else missing.push({ path: file.path, expectedBytes: file.bytes, actualBytes: actual ?? null });
  }
  present.sort();
  return { present, missing };
}
