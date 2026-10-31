import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES } from "../contract";
import { joinSafePath } from "../keys";

export interface DeclaredFile {
  path: string;
  bytes: number;
}

export type FileListCheck =
  | { ok: true; files: DeclaredFile[] }
  | { ok: false; status: 400 | 413; error: string; detail: string };

// ファイル一覧の検査はここ1箇所（prepareとimportが共有）。予約名だけは呼び出し側が渡し、パスの安全性はkeys.ts、上限は契約値だけを見る
// The single file-list check shared by prepare and import; callers pass the reserved names, path safety is keys.ts, and limits come from the contract
export function parseFileList(files: unknown, reservedSegments: ReadonlySet<string>): FileListCheck {
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
    if (reservedSegments.has(segments[0] as string)) return reject(400, "reserved-name", path);
    if (seen.has(path)) return reject(400, "duplicate-path", path);
    if (bytes > MAX_FILE_BYTES) return reject(413, "too-large", `${path}: ${bytes} bytes exceeds ${MAX_FILE_BYTES}`);
    seen.add(path);
    total += bytes;
    if (total > MAX_BUNDLE_BYTES) return reject(413, "bundle-too-large", `${total} bytes exceeds ${MAX_BUNDLE_BYTES}`);
    accepted.push({ path, bytes });
  }
  return { ok: true, files: accepted };
}

function reject(status: 400 | 413, error: string, detail: string): FileListCheck {
  return { ok: false, status, error, detail };
}
