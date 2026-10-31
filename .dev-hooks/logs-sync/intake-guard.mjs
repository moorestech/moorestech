// 自動コミットに載せる前に、logs repo に置くべきでない物（動画・大きなファイル・一括コピー）を外して知らせる
// Before the auto-commit, unstage what does not belong in the logs repo (videos, large files, bulk copies) and report it.
//
// logs repo は判断を辿るための文章の置き場。生データは作業場所か MOORESTECH_PLAYTEST_RAW に置く（AGENTS.md）
// The logs repo holds text for tracing decisions; raw data lives in the work area or MOORESTECH_PLAYTEST_RAW (see AGENTS.md).

import { existsSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { extname, join } from "node:path";
import { git } from "./sync-io.mjs";

// 会話ログ類は logs-sync 自身が書き、大きさは別途 GitHub 上限で見ている
// Conversation logs are written by logs-sync itself; their size is checked against the GitHub limit elsewhere.
const CONVERSATION_PREFIXES = ["claude/", "codex/", "map/", "beads/", "blobs/"];
const VIDEO_EXTENSIONS = new Set([".mp4", ".webm", ".mov", ".mkv", ".avi", ".m4v"]);
// 報告書・記録の文章はこれより十分小さい。これを超えるのはスナップショットやダンプの類
// Reports and records are far below this; anything larger is a snapshot or a dump.
const MAX_RECORD_FILE_BYTES = 2 * 1024 * 1024;
// 1回の同期で同じディレクトリにこれ以上の新規ファイルが現れたら、連番フレームか worktree のコピーとみなす
// More new files than this under one directory in a single sync means frame sequences or a worktree copy.
const MAX_NEW_FILES_PER_DIR = 200;
const BULK_DIR_DEPTH = 3;

// git add -A の後に呼ぶ。外したパスは作業ツリーに残り、初めて外した時だけ report する
// Call after `git add -A`. Unstaged paths stay in the work tree; each is reported only the first time.
export function unstageRejected(logsRepo, stateDir, report) {
  const staged = stagedChanges(logsRepo);
  const rejected = classify(logsRepo, staged);
  if (rejected.size === 0) return;
  git(logsRepo, ["reset", "-q", "--pathspec-from-file=-", "--pathspec-file-nul"], 30000, [...rejected.keys()].join("\0"));
  const seenPath = join(stateDir, "intake-rejected.json");
  const seen = readSeen(seenPath, report);
  // 一括コピーは1件ずつ出すと数百行になるので、理由（＝ディレクトリ）ごとに1行にまとめる
  // Bulk copies would flood hundreds of lines, so they collapse into one line per reason (i.e. per directory).
  const lines = new Map();
  for (const [path, reason] of rejected) {
    if (seen[path] === reason) continue;
    seen[path] = reason;
    const key = reason.startsWith("bulk:") ? reason : `${reason}\0${path}`;
    lines.set(key, reason.startsWith("bulk:") ? `rejected (${reason})` : `rejected (${reason}): ${path}`);
  }
  for (const line of lines.values()) report(`${line} — logs repo には文章の記録だけを置く。作業場所か MOORESTECH_PLAYTEST_RAW へ移すこと`);
  writeFileSync(seenPath, JSON.stringify(seen));
}

function readSeen(seenPath, report) {
  if (!existsSync(seenPath)) return {};
  try {
    return JSON.parse(readFileSync(seenPath, "utf8"));
  } catch (error) {
    report(`intake-rejected.json unreadable, re-reporting all rejections: ${error.message}`);
    return {};
  }
}

// 追加・変更されたパスの一覧（削除は常に通す）
// Added or modified paths; deletions always pass.
function stagedChanges(logsRepo) {
  const out = git(logsRepo, ["diff", "--cached", "--name-status", "-z", "--no-renames"], 30000) ?? "";
  const tokens = out.split("\0").filter(Boolean);
  const changes = [];
  for (let i = 0; i + 1 < tokens.length; i += 2) {
    if (tokens[i] !== "D") changes.push({ status: tokens[i], path: tokens[i + 1] });
  }
  return changes;
}

function classify(logsRepo, changes) {
  const rejected = new Map();
  const newPerDir = new Map();
  for (const { status, path } of changes) {
    if (CONVERSATION_PREFIXES.some((prefix) => path.startsWith(prefix))) continue;
    if (VIDEO_EXTENSIONS.has(extname(path).toLowerCase())) rejected.set(path, "video");
    else if (sizeOf(join(logsRepo, path)) > MAX_RECORD_FILE_BYTES) rejected.set(path, `over ${MAX_RECORD_FILE_BYTES} bytes`);
    if (status === "A") {
      const dir = path.split("/").slice(0, BULK_DIR_DEPTH).join("/");
      newPerDir.set(dir, [...(newPerDir.get(dir) ?? []), path]);
    }
  }
  for (const [dir, paths] of newPerDir) {
    if (paths.length <= MAX_NEW_FILES_PER_DIR) continue;
    for (const path of paths) rejected.set(path, `bulk: ${paths.length} new files under ${dir}`);
  }
  return rejected;
}

function sizeOf(path) {
  // silent-ok: stat できないパスは大きさの判定から外れるだけで、動画と一括コピーの判定は続く / 実体が無いなら git add も載せない
  try {
    return statSync(path).size;
  } catch {
    return 0;
  }
}
