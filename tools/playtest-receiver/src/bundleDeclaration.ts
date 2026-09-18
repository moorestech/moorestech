import { declaredMarkerKey } from "./bundleMarkers";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES, RESERVED_UPLOAD_SEGMENTS } from "./contract";
import { joinSafePath, type PlaytestKind } from "./keys";

export interface DeclaredFile {
  path: string;
  bytes: number;
}

export type DeclarationCheck =
  | { ok: true; files: DeclaredFile[] }
  | { ok: false; status: 400 | 413; error: string; detail: string };

// 宣言の検査はここ1箇所。パスの安全性はkeys.tsに委ね、上限は契約値だけを見る
// The single declaration check; path safety is delegated to keys.ts and every limit comes from the contract
export function parseDeclaration(body: unknown): DeclarationCheck {
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
  return { ok: true, files: accepted };
}

function reject(status: 400 | 413, error: string, detail: string): DeclarationCheck {
  return { ok: false, status, error, detail };
}

// 宣言はcompleteの照合元としてR2に置く。クライアントの再申告を信じないための唯一の記録
// The declaration is stored in R2 as the reference complete verifies against; it is the only record, so the client's re-statement is never trusted
export async function writeDeclaration(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string, files: DeclaredFile[]): Promise<void> {
  await bucket.put(declaredMarkerKey(kind, steamId, id), JSON.stringify({ files }), { httpMetadata: { contentType: "application/json" } });
}

export async function readDeclaration(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<DeclaredFile[] | null> {
  const object = await bucket.get(declaredMarkerKey(kind, steamId, id));
  if (object === null) return null;
  // 自分で書いたJSONだが、R2上のオブジェクトは外部入力として扱い、壊れていれば無かったことにして再prepareを促す
  // Although we wrote it, an R2 object is treated as external input; a broken one counts as absent so the client re-prepares
  try {
    const parsed = parseDeclaration(await object.json());
    return parsed.ok ? parsed.files : null;
  } catch {
    console.warn(`[upload] DECLARED of ${steamId}/${id} is not readable JSON; treating it as absent`);
    return null;
  }
}
