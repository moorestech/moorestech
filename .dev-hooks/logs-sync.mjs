#!/usr/bin/env node
// Claude/Codexの生セッションログをprivate考古学repo(moorestech_logs)へ退避する
// Archive raw Claude/Codex session logs into the private archaeology repo.
//
// 登録: Claude Stop/SessionEnd、Codex PostToolUse。logs repo不在時は沈黙(fail-open)
// Wired to Claude Stop/SessionEnd and Codex PostToolUse; silent when the logs repo is absent.
//
// 自動実行の記録（Claude の subagent・codex exec）は、元に戻せる部分だけ削って入れる（logs-sync/trim-*.mjs）。
// 戻すときは logs-sync/restore-transcript.mjs。人とのセッションはそのまま入れる。
// Automated-run transcripts (Claude subagents, codex exec) are stored with only restorable parts trimmed (logs-sync/trim-*.mjs);
// restore them with logs-sync/restore-transcript.mjs. Human sessions are stored verbatim.

import { execFileSync } from "node:child_process";
import { closeSync, existsSync, mkdirSync, openSync, readFileSync, readSync, writeFileSync } from "node:fs";
import { homedir, hostname } from "node:os";
import { dirname, join, sep } from "node:path";
import { unstageRejected } from "./logs-sync/intake-guard.mjs";
import { copyIfUpdated, createReporter, git, isDirectory, isNewerThan, safeReaddir } from "./logs-sync/sync-io.mjs";
import { createTrimContext, writeTrimmedTranscript } from "./logs-sync/trim-file.mjs";

function bail() {
  process.exit(0);
}

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {}

const logsRepo = findLogsRepo();
if (!logsRepo) bail();

// スロットル: 直近実行から90秒未満なら何もしない（Codexの毎ツール発火対策）
// Throttle: skip entirely within 90s of the last run (Codex fires per tool call).
const stateDir = join(logsRepo, ".state");
mkdirSync(stateDir, { recursive: true });
const throttleMarker = join(stateDir, "logs-sync-last-run");
if (isNewerThan(throttleMarker, 90)) bail();
writeFileSync(throttleMarker, "");

const report = createReporter(stateDir);
const trimContext = createTrimContext(logsRepo);
syncClaudeTranscripts();
syncCodexRollouts();
syncBeadsMirror();
commitAndPush();
bail();

// git common dir経由でメインrepoを特定し、兄弟のmoorestech_logsを返す（worktree対応）
// Locate the main repo via git common dir and return the sibling moorestech_logs (worktree-safe).
function findLogsRepo() {
  const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
  const common = git(cwd, ["rev-parse", "--git-common-dir"]);
  if (!common) return null;
  const mainRoot = dirname(common === ".git" ? join(cwd, ".git") : common);
  const candidate = join(dirname(mainRoot), "moorestech_logs");
  return existsSync(join(candidate, ".git")) ? candidate : null;
}

// ~/.claude/projects のmoorestech系スラグ配下を丸ごと増分コピーする（subagents/・tool-results/含む）。
// subagent 記録は、参照先の tool-results が先に退避されているよう最後に削って書く
// Incrementally mirror everything under moorestech-ish slugs in ~/.claude/projects (incl. subagents/ and tool-results/).
// Subagent transcripts are trimmed last so the tool-results they point at are already mirrored.
function syncClaudeTranscripts() {
  const projectsDir = join(homedir(), ".claude", "projects");
  const subagentTranscripts = [];
  for (const slug of safeReaddir(projectsDir)) {
    if (!slug.includes("moorestech")) continue;
    syncTree(join(projectsDir, slug), join(logsRepo, "claude", slug), subagentTranscripts);
  }
  for (const [src, dest] of subagentTranscripts) {
    copyIfUpdated(src, dest, report, (s, d) => writeTrimmedTranscript(s, d, "claude", trimContext));
  }
}

// srcDir以下の全ファイルを再帰的に増分コピーする（subagent 記録は deferred に回す）
// Recursively copy every file under srcDir, incrementally (subagent transcripts go to deferred).
function syncTree(srcDir, destDir, deferred) {
  for (const entry of safeReaddir(srcDir)) {
    const src = join(srcDir, entry);
    if (isDirectory(src)) syncTree(src, join(destDir, entry), deferred);
    else if (src.includes(`${sep}subagents${sep}`) && entry.endsWith(".jsonl")) deferred.push([src, join(destDir, entry)]);
    else copyIfUpdated(src, join(destDir, entry), report);
  }
}

// ~/.codex/sessions の直近日付ディレクトリからcwdがmoorestech系のrolloutをコピーする（codex exec は削って書く）
// Copy rollouts whose session_meta.cwd is moorestech-ish from recent date dirs of ~/.codex/sessions (codex exec ones are trimmed).
function syncCodexRollouts() {
  const sessionsDir = join(homedir(), ".codex", "sessions");
  for (const year of safeReaddir(sessionsDir)) {
    for (const month of safeReaddir(join(sessionsDir, year))) {
      for (const day of safeReaddir(join(sessionsDir, year, month))) {
        const dayDir = join(sessionsDir, year, month, day);
        // 過去分はbackfill担当なので、直近3日に触れられた日付ディレクトリだけ見る
        // Backfill owns the past; only visit day dirs touched within 3 days.
        if (!isNewerThan(dayDir, 3 * 24 * 3600)) continue;
        for (const file of safeReaddir(dayDir)) {
          if (!file.startsWith("rollout-") || !file.endsWith(".jsonl")) continue;
          const src = join(dayDir, file);
          const meta = readSessionMeta(src);
          if (!meta.cwd.includes("moorestech")) continue;
          const write = meta.originator === "codex_exec" ? (s, d) => writeTrimmedTranscript(s, d, "codex", trimContext) : undefined;
          copyIfUpdated(src, join(logsRepo, "codex", file), report, write);
        }
      }
    }
  }
}

// Beadsのissues.jsonlミラーをマシン別ファイルへスナップショットコピーする（正本はDolt側）
// 共有1ファイルだと同期の止まった2機のexportが交互に上書きし合い、件数が往復して片側のissueが消えて見えた（2026-09-20）
// Snapshot-copy the beads issues.jsonl mirror into a per-host file (Dolt remains the source of truth).
// A single shared file let two out-of-sync hosts overwrite each other's export, so one side's issues looked lost (2026-09-20).
function syncBeadsMirror() {
  const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
  const common = git(cwd, ["rev-parse", "--git-common-dir"]);
  if (!common) return;
  const mainRoot = dirname(common === ".git" ? join(cwd, ".git") : common);
  copyIfUpdated(join(mainRoot, ".beads", "issues.jsonl"), join(logsRepo, "beads", `issues.${hostname()}.jsonl`), report);
}

// 5分スロットルでcommitし、pushはbest-effort（並行セッションはロックで譲る）
// Commit on a 5-minute throttle; push best-effort (concurrent sessions yield via a lock).
function commitAndPush() {
  const commitMarker = join(stateDir, "logs-sync-last-commit");
  if (isNewerThan(commitMarker, 300)) return;
  const lockDir = join(stateDir, "lock");
  // 外部境界: mkdirの原子性で多重実行を排他。古い残骸ロック(10分超)は無視して奪う
  // External boundary: exclude concurrent runs via atomic mkdir; steal stale (>10min) locks.
  try {
    mkdirSync(lockDir);
  } catch {
    if (isNewerThan(lockDir, 600)) return;
  }
  // logs repo は古いログを sparse-checkout で展開していない。--sparse が無いと、その範囲に書かれた新しいファイルが黙ってコミットから漏れる
  // The logs repo leaves old logs unexpanded via sparse-checkout; without --sparse, new files under those paths silently miss the commit.
  git(logsRepo, ["add", "-A", "--sparse"], 60000);
  unstageRejected(logsRepo, stateDir, report);
  if (git(logsRepo, ["diff", "--cached", "--name-only"]) !== "") {
    git(logsRepo, ["commit", "-q", "-m", "auto: logs-sync"]);
    writeFileSync(commitMarker, "");
    // 前回のrebaseが残っていたら中断状態を解消してから進む
    // Clear any leftover mid-rebase state before proceeding.
    if (existsSync(join(logsRepo, ".git", "rebase-merge")) || existsSync(join(logsRepo, ".git", "rebase-apply"))) {
      git(logsRepo, ["rebase", "--abort"]);
    }
    const pulled = git(logsRepo, ["pull", "--rebase", "--autostash", "-q"], 30000);
    // pull失敗時はmid-rebaseを残さず中断し、push自体をスキップ（次回サイクルで再試行）
    // On pull failure, abort to avoid leaving a mid-rebase state and skip the push (retried next cycle).
    if (pulled === null) {
      git(logsRepo, ["rebase", "--abort"]);
    } else {
      git(logsRepo, ["push", "-q"], 30000);
    }
  }
  // 外部境界: ロック解放失敗は次回のstale判定に任せる
  // External boundary: leave failed unlock to the next stale check.
  try {
    execFileSync("rmdir", [lockDir]);
  } catch {}
}

// rollout先頭のsession_metaからcwdとoriginatorを読む（行が巨大なのでJSONパースせずに正規表現で抜く）
// Read cwd and originator from the rollout's session_meta; the line is huge, so regex instead of JSON parse.
function readSessionMeta(path) {
  // 外部境界: 読み取り失敗は「対象外」として扱う
  // External boundary: treat read failures as "not a target".
  try {
    const fd = openSync(path, "r");
    const buf = Buffer.alloc(8192);
    const len = readSync(fd, buf, 0, 8192, 0);
    closeSync(fd);
    const firstLine = buf.toString("utf8", 0, len).split("\n")[0];
    return {
      cwd: firstLine.match(/"cwd":"([^"]+)"/)?.[1] ?? "",
      originator: firstLine.match(/"originator":"([^"]+)"/)?.[1] ?? "",
    };
  } catch {
    return { cwd: "", originator: "" };
  }
}
