// 自動コミットに載せる前に、logs repo に置くべきでない物（記録でない種類・大きなファイル・連番画像）を外して知らせる
// Before the auto-commit, unstage what does not belong in the logs repo (non-record types, large files, image sequences) and report it.
//
// logs repo は判断を辿るための文章の置き場。生データは作業場所か MOORESTECH_PLAYTEST_RAW に置く（AGENTS.md）。
// 判定はファイル単位の許可リストで、件数では判定しない（ゆっくり書かれる連番や浅いコピーをすり抜けさせず、隣の報告書を巻き添えにしないため）
// The logs repo holds text for tracing decisions; raw data lives in the work area or MOORESTECH_PLAYTEST_RAW (see AGENTS.md).
// Each file is judged against an allow-list, never by counts, so slow frame sequences and shallow copies cannot slip through
// and neighbouring reports are never rejected along with them.

import { closeSync, existsSync, openSync, readFileSync, readSync, statSync, writeFileSync } from "node:fs";
import { dirname, extname, join } from "node:path";
import { git } from "./sync-io.mjs";

// 会話ログ類は logs-sync 自身が書き、大きさは別途 GitHub 上限で見ている
// Conversation logs are written by logs-sync itself; their size is checked against the GitHub limit elsewhere.
const CONVERSATION_PREFIXES = ["claude/", "codex/", "map/", "beads/", "blobs/"];
// 記録として置いてよい種類（文章・結果・差分・実験に書いたスクリプト）。コードやアセットのコピー・バイナリ・動画はここに無い
// Record types allowed in: text, results, diffs and scripts written for experiments. Copies of code/assets, binaries and videos are absent.
const RECORD_EXTENSIONS = new Set([
  "", ".md", ".txt", ".log", ".json", ".jsonl", ".tsv", ".csv", ".yaml", ".yml", ".toml", ".html", ".htm", ".xml", ".svg",
  ".diff", ".patch", ".err", ".stderr", ".stdout", ".out", ".sha", ".sha256", ".marker", ".exit", ".session", ".ini", ".cfg",
  ".py", ".sh", ".js", ".mjs", ".ts", ".ps1", ".cmd", ".bat",
]);
// 記録の種類でも、中身が文字でない物（先頭にNULがある）はバイナリとして外す（拡張子の無いダンプや偽装を通さない）
// Even with a record extension, content with a NUL byte near the start is a binary and is rejected.
const TEXT_SNIFF_BYTES = 8192;
const IMAGE_EXTENSIONS = new Set([".png", ".jpg", ".jpeg", ".webp", ".gif"]);
// 報告に添える画像はディレクトリあたり数枚。これを超えるのは連番フレームの類（既にコミット済みの枚数も数える）
// A report carries a few images per directory; more than this (committed ones included) is a frame sequence.
const MAX_IMAGES_PER_DIR = 20;
// 報告書・記録の文章はこれより十分小さい。これを超えるのはスナップショットやダンプの類
// Reports and records are far below this; anything larger is a snapshot or a dump.
const MAX_RECORD_FILE_BYTES = 2 * 1024 * 1024;

// git add の後に呼ぶ。外したパスは作業ツリーに残り、初めて外した時だけ report する
// Call after `git add`. Unstaged paths stay in the work tree; each is reported only the first time.
export function unstageRejected(logsRepo, stateDir, report) {
  const rejected = classify(logsRepo, stagedChanges(logsRepo));
  if (rejected.size === 0) return;
  git(logsRepo, ["reset", "-q", "--pathspec-from-file=-", "--pathspec-file-nul"], 30000, [...rejected.keys()].join("\0"));
  const seenPath = join(stateDir, "intake-rejected.json");
  const seen = readSeen(seenPath, report);
  // 同じ理由・同じディレクトリの物は1行にまとめ、件数と例を出す（コピー数百件で通知が埋まらないように）
  // Group by reason and directory into one line with a count and an example, so hundreds of copies do not flood the report.
  const groups = new Map();
  for (const [path, reason] of rejected) {
    if (seen[path] === reason) continue;
    seen[path] = reason;
    const key = `${reason}\0${dirname(path)}`;
    groups.set(key, [...(groups.get(key) ?? []), path]);
  }
  for (const [key, paths] of groups) {
    const [reason] = key.split("\0");
    const what = paths.length === 1 ? paths[0] : `${paths.length} files under ${dirname(paths[0])}/ (e.g. ${paths[0]})`;
    report(`rejected (${reason}): ${what} — logs repo には文章の記録だけを置く。作業場所か MOORESTECH_PLAYTEST_RAW へ移すこと`);
  }
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
  const imagesPerDir = new Map();
  for (const { status, path } of changes) {
    if (CONVERSATION_PREFIXES.some((prefix) => path.startsWith(prefix))) continue;
    const ext = extname(path).toLowerCase();
    if (IMAGE_EXTENSIONS.has(ext)) {
      if (status === "A") imagesPerDir.set(dirname(path), [...(imagesPerDir.get(dirname(path)) ?? []), path]);
    } else if (!RECORD_EXTENSIONS.has(ext)) {
      rejected.set(path, `not a record type: ${ext}`);
      continue;
    } else if (isBinary(join(logsRepo, path))) {
      rejected.set(path, `binary content: ${ext || "(no extension)"}`);
      continue;
    }
    if (sizeOf(join(logsRepo, path)) > MAX_RECORD_FILE_BYTES) rejected.set(path, `over ${MAX_RECORD_FILE_BYTES} bytes`);
  }
  for (const [dir, added] of imagesPerDir) {
    // -z で読む（無いと日本語のパスが引用符つきで返り、拡張子を読み違える）
    // Read with -z; otherwise non-ASCII paths come back quoted and their extensions are misread.
    const committed = (git(logsRepo, ["ls-tree", "-z", "--name-only", "HEAD", `${dir}/`]) ?? "")
      .split("\0").filter((p) => IMAGE_EXTENSIONS.has(extname(p).toLowerCase())).length;
    if (committed + added.length <= MAX_IMAGES_PER_DIR) continue;
    for (const path of added) rejected.set(path, `image sequence: over ${MAX_IMAGES_PER_DIR} images in one directory`);
  }
  return rejected;
}

function isBinary(path) {
  // silent-ok: 読めないパスは文字扱いにして大きさの判定へ進み、実体が無いなら git add も載せない / 欠落は起きない
  try {
    const fd = openSync(path, "r");
    const buf = Buffer.alloc(TEXT_SNIFF_BYTES);
    const len = readSync(fd, buf, 0, TEXT_SNIFF_BYTES, 0);
    closeSync(fd);
    return buf.subarray(0, len).includes(0);
  } catch {
    return false;
  }
}

function sizeOf(path) {
  // silent-ok: stat できないパスは大きさの判定から外れるだけで、種類と連番の判定は続く / 実体が無いなら git add も載せない
  try {
    return statSync(path).size;
  } catch {
    return 0;
  }
}
