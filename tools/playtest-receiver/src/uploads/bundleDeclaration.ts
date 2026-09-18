import { declaredMarkerKey } from "../bundleMarkers";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES, RESERVED_UPLOAD_SEGMENTS } from "../contract";
import { joinSafePath, type PlaytestKind } from "../keys";

export interface DeclaredFile {
  path: string;
  bytes: number;
}

// 宣言は世代付き。同世代は同じ集合だけを表し、集合を縮めるときだけ世代を上げる
// A declaration carries a generation; one generation means one exact set, and only a shrink moves to a higher generation
export interface Declaration {
  generation: number;
  files: DeclaredFile[];
}

export type DeclarationCheck =
  | ({ ok: true } & Declaration)
  | { ok: false; status: 400 | 413; error: string; detail: string };

// 宣言の検査はここ1箇所。パスの安全性はkeys.tsに委ね、上限は契約値だけを見る
// The single declaration check; path safety is delegated to keys.ts and every limit comes from the contract
export function parseDeclaration(body: unknown): DeclarationCheck {
  const generation = (body as { generation?: unknown })?.generation;
  if (typeof generation !== "number" || !Number.isInteger(generation) || generation < 1) return reject(400, "bad-request", `generation is not an integer >= 1: ${JSON.stringify(generation)}`);
  const files = (body as { files?: unknown })?.files;
  if (!Array.isArray(files)) return reject(400, "bad-request", "files is not an array");
  if (files.length === 0) return reject(400, "empty-declaration", "no files declared");
  if (files.length > MAX_BUNDLE_FILES) return reject(413, "too-many-files", `${files.length} files exceeds ${MAX_BUNDLE_FILES}`);

  const seen = new Set<string>();
  const accepted: DeclaredFile[] = [];
  let total = 0;
  for (const entry of files) {
    const path = (entry as { path?: unknown })?.path;
    const bytes = (entry as { bytes?: unknown })?.bytes;
    if (typeof path !== "string" || typeof bytes !== "number" || !Number.isInteger(bytes) || bytes < 0) {
      return reject(400, "bad-request", `malformed entry: ${JSON.stringify(entry)}`);
    }
    const segments = path.split("/");
    if (joinSafePath(segments) === null) return reject(400, "bad-path", path);
    if (RESERVED_UPLOAD_SEGMENTS.has(segments[0] as string)) return reject(400, "reserved-name", path);
    if (seen.has(path)) return reject(400, "duplicate-path", path);
    if (bytes > MAX_FILE_BYTES) return reject(413, "too-large", `${path}: ${bytes} bytes exceeds ${MAX_FILE_BYTES}`);
    seen.add(path);
    total += bytes;
    if (total > MAX_BUNDLE_BYTES) return reject(413, "bundle-too-large", `${total} bytes exceeds ${MAX_BUNDLE_BYTES}`);
    accepted.push({ path, bytes });
  }
  return { ok: true, generation, files: accepted };
}

function reject(status: 400 | 413, error: string, detail: string): DeclarationCheck {
  return { ok: false, status, error, detail };
}

export type RedeclarationKind = "same" | "replace" | "conflict";

// 再prepareと保存済みDECLAREDの関係。同世代は集合の完全一致だけ、上の世代は部分集合（縮小）だけ許す。件数も総量も増やせず、同世代の食い違いは同時prepareの衝突として拒む
// How a re-prepare relates to the stored DECLARED: the same generation must match exactly, a higher one may only shrink; neither can grow past the limits, and a same-generation mismatch is a racing prepare
export function classifyRedeclaration(stored: Declaration, requested: Declaration): RedeclarationKind {
  if (requested.generation < stored.generation) return "conflict";
  const storedBytes = new Map(stored.files.map((file) => [file.path, file.bytes]));
  const isSubset = requested.files.every((file) => storedBytes.get(file.path) === file.bytes);
  if (requested.generation === stored.generation) return isSubset && requested.files.length === stored.files.length ? "same" : "conflict";
  return isSubset ? "replace" : "conflict";
}

export type StoredDeclaration =
  | { state: "absent" }
  | { state: "unreadable"; reason: string }
  | { state: "ok"; declaration: Declaration; etag: string };

// 宣言はcompleteの照合元としてR2に置く。クライアントの再申告を信じないための唯一の記録
// The declaration is stored in R2 as the reference complete verifies against; it is the only record, so the client's re-statement is never trusted
export async function readDeclaration(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<StoredDeclaration> {
  const object = await bucket.get(declaredMarkerKey(kind, steamId, id));
  if (object === null) return { state: "absent" };
  // 自分で書いたJSONだが、R2上のオブジェクトは外部入力（パース例外の境界）。壊れていても無い扱いにはしない（ガードが外れるため）
  // Although we wrote it, an R2 object is external input (a parse-exception boundary); a broken one never counts as absent, which would drop the guard
  let body: unknown;
  try {
    body = await object.json();
  } catch {
    return { state: "unreadable", reason: "not readable JSON" };
  }
  const parsed = parseDeclaration(body);
  if (!parsed.ok) return { state: "unreadable", reason: `fails the declaration check (${parsed.error}: ${parsed.detail})` };
  return { state: "ok", declaration: { generation: parsed.generation, files: parsed.files }, etag: object.etag };
}

// 初回確定は「まだ無いときだけ」書く。同時prepareの後着はfalse（先行者の宣言を上書きしない）
// The first declaration is written only while none exists; a racing latecomer gets false and never overwrites the winner
export async function writeDeclarationIfAbsent(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string, declaration: Declaration): Promise<boolean> {
  return (await bucket.put(declaredMarkerKey(kind, steamId, id), serialize(declaration), { onlyIf: { etagDoesNotMatch: "*" }, httpMetadata: { contentType: "application/json" } })) !== null;
}

// 縮小は読んだ版がまだ最新のときだけ置き換える（CAS）。間に別の書き込みがあればfalse
// A shrink replaces only the version that was read (CAS); false when another write landed in between
export async function replaceDeclarationIfUnchanged(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string, declaration: Declaration, etag: string): Promise<boolean> {
  return (await bucket.put(declaredMarkerKey(kind, steamId, id), serialize(declaration), { onlyIf: { etagMatches: etag }, httpMetadata: { contentType: "application/json" } })) !== null;
}

function serialize(declaration: Declaration): string {
  return JSON.stringify({ generation: declaration.generation, files: declaration.files });
}
