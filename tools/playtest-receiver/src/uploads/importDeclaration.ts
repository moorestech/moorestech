import { ACKED_MARKER, DECLARED_MARKER, IMPORT_DECLARED_MARKER, importDeclaredMarkerKey, PURGED_MARKER } from "../bundleMarkers";
import type { PlaytestKind } from "../keys";
import { parseFileList, type DeclaredFile, type FileListCheck, type FileListLimits } from "./fileList";

// zipの箱はREADYを持つので許し、取り込み・宣言・片付けの印だけを予約名にする
// A zip's box carries READY, so it is allowed; only the ingest, declaration and purge markers are reserved
const IMPORT_RESERVED_SEGMENTS: ReadonlySet<string> = new Set([ACKED_MARKER, DECLARED_MARKER, PURGED_MARKER, IMPORT_DECLARED_MARKER]);
// zipの全ファイルをR2へ残すため、上限はR2単一PUTの上限（約5GiB）を下回る5GBだけにし、件数と合計は縛らない
// To keep every file of a zip in R2, only 5GB (under R2's ~5GiB single-PUT cap) limits a file; count and total are unbounded
const R2_SINGLE_PUT_MAX_BYTES = 5 * 1000 * 1000 * 1000;
const IMPORT_FILE_LIMITS: FileListLimits = { maxFileBytes: R2_SINGLE_PUT_MAX_BYTES, maxBundleFiles: Number.POSITIVE_INFINITY, maxBundleBytes: Number.POSITIVE_INFINITY };

// importの宣言検査。importと、保存した宣言を読み直すcompleteが同じ規則を使う
// Checks an import declaration; import and complete (re-reading the stored one) share these rules
export function parseImportFiles(files: unknown): FileListCheck {
  return parseFileList(files, IMPORT_RESERVED_SEGMENTS, IMPORT_FILE_LIMITS);
}

// 宣言を箱の中に置き、completeがURL発行時の一覧で照合できるようにする。再importは最新の宣言で置き換える
// Stores the declaration in the box so complete verifies against the list URLs were issued for; a re-import replaces it
export async function writeImportDeclaration(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string, files: DeclaredFile[]): Promise<void> {
  await bucket.put(importDeclaredMarkerKey(kind, steamId, id), JSON.stringify({ files }), { httpMetadata: { contentType: "application/json" } });
}

export type StoredImportDeclaration =
  | { state: "absent" }
  | { state: "unreadable"; reason: string }
  | { state: "ok"; files: DeclaredFile[] };

export async function readImportDeclaration(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<StoredImportDeclaration> {
  const object = await bucket.get(importDeclaredMarkerKey(kind, steamId, id));
  if (object === null) return { state: "absent" };
  // 自分で書いたJSONだが、R2上のオブジェクトは外部入力（パース例外の境界）。壊れていれば照合できないので理由を返す
  // We wrote it, but an R2 object is external input (a parse-exception boundary); a broken one cannot be verified, so return why
  let body: unknown;
  try {
    body = await object.json();
  } catch {
    return { state: "unreadable", reason: "not readable JSON" };
  }
  const parsed = parseImportFiles((body as { files?: unknown })?.files);
  if (!parsed.ok) return { state: "unreadable", reason: `fails the import check (${parsed.error}: ${parsed.detail})` };
  return { state: "ok", files: parsed.files };
}
