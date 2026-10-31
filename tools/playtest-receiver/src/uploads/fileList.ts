import { joinSafePath } from "../keys";

export interface DeclaredFile {
  path: string;
  bytes: number;
}

// 宣言に当てる上限。prepareはクライアント契約値、importはR2単一PUTの上限を渡す
// Limits applied to a declaration; prepare passes the client contract values, import passes R2's single-PUT cap
export interface FileListLimits {
  maxFileBytes: number;
  maxBundleFiles: number;
  maxBundleBytes: number;
}

export type FileListCheck =
  | { ok: true; files: DeclaredFile[] }
  | { ok: false; status: 400 | 413; error: string; detail: string };

// ファイル一覧の検査はここ1箇所（prepareとimportが共有）。予約名と上限は呼び出し側が渡し、パスの安全性はkeys.tsが見る
// The single file-list check shared by prepare and import; callers pass the reserved names and limits, and path safety is keys.ts
export function parseFileList(files: unknown, reservedSegments: ReadonlySet<string>, limits: FileListLimits): FileListCheck {
  if (!Array.isArray(files)) return reject(400, "bad-request", "files is not an array");
  if (files.length === 0) return reject(400, "empty-declaration", "no files declared");
  if (files.length > limits.maxBundleFiles) return reject(413, "too-many-files", `${files.length} files exceeds maxBundleFiles ${limits.maxBundleFiles}`);

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
    if (bytes > limits.maxFileBytes) return reject(413, "too-large", `${path}: ${bytes} bytes exceeds maxFileBytes ${limits.maxFileBytes}`);
    seen.add(path);
    total += bytes;
    if (total > limits.maxBundleBytes) return reject(413, "bundle-too-large", `${path}: running total ${total} bytes exceeds maxBundleBytes ${limits.maxBundleBytes}`);
    accepted.push({ path, bytes });
  }
  return { ok: true, files: accepted };
}

function reject(status: 400 | 413, error: string, detail: string): FileListCheck {
  return { ok: false, status, error, detail };
}
