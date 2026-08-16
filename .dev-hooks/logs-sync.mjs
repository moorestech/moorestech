#!/usr/bin/env node
// Claude/Codexの生セッションログをprivate考古学repo(moorestech_logs)へ退避する
// Archive raw Claude/Codex session logs into the private archaeology repo.
//
// 登録: Claude Stop/SessionEnd、Codex PostToolUse。logs repo不在時は沈黙(fail-open)
// Wired to Claude Stop/SessionEnd and Codex PostToolUse; silent when the logs repo is absent.

import { execFileSync } from "node:child_process";
import {
  appendFileSync, copyFileSync, existsSync, mkdirSync, openSync, readSync, closeSync,
  readdirSync, readFileSync, statSync, utimesSync, writeFileSync,
} from "node:fs";
import { homedir } from "node:os";
import { basename, dirname, join } from "node:path";

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

// ~/.claude/projects のmoorestech系スラグ配下を丸ごと増分コピーする（subagents/・tool-results/含む）
// Incrementally mirror everything under moorestech-ish slugs in ~/.claude/projects (incl. subagents/ and tool-results/).
function syncClaudeTranscripts() {
  const projectsDir = join(homedir(), ".claude", "projects");
  for (const slug of safeReaddir(projectsDir)) {
    if (!slug.includes("moorestech")) continue;
    syncTree(join(projectsDir, slug), join(logsRepo, "claude", slug));
  }
}

// srcDir以下の全ファイルを再帰的に増分コピーする
// Recursively copy every file under srcDir, incrementally.
function syncTree(srcDir, destDir) {
  for (const entry of safeReaddir(srcDir)) {
    const src = join(srcDir, entry);
    if (isDirectory(src)) syncTree(src, join(destDir, entry));
    else copyIfUpdated(src, join(destDir, entry));
  }
}

// ~/.codex/sessions の直近日付ディレクトリからcwdがmoorestech系のrolloutをコピーする
// Copy rollouts whose session_meta.cwd is moorestech-ish, from recent date dirs of ~/.codex/sessions.
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
          if (readCwdOfRollout(src).includes("moorestech")) {
            copyIfUpdated(src, join(logsRepo, "codex", file));
          }
        }
      }
    }
  }
}

// Beadsのissues.jsonlミラーをスナップショットコピーする（正本はDolt側）
// Snapshot-copy the beads issues.jsonl mirror (Dolt remains the source of truth).
function syncBeadsMirror() {
  const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
  const common = git(cwd, ["rev-parse", "--git-common-dir"]);
  if (!common) return;
  const mainRoot = dirname(common === ".git" ? join(cwd, ".git") : common);
  copyIfUpdated(join(mainRoot, ".beads", "issues.jsonl"), join(logsRepo, "beads", "issues.jsonl"));
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
  git(logsRepo, ["add", "-A"]);
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

// 宛先が無い(かつ7日以内のファイル)か、宛先より新しい時だけコピーする冪等コピー
// Idempotent copy: only when dest is missing (and src is <7 days old) or src is newer.
function copyIfUpdated(src, dest) {
  // 外部境界: 他プロセス管理下のファイル群のためstat/copy失敗は握りつぶす
  // External boundary: swallow stat/copy failures on files owned by other processes.
  try {
    const srcStat = statSync(src);
    // GitHubの100MB上限を超えるファイルはpushできないため同期しない
    // Skip files over GitHub's 100MB limit; they can never be pushed.
    if (srcStat.size > 95 * 1024 * 1024) return;
    if (existsSync(dest)) {
      if (srcStat.mtimeMs <= statSync(dest).mtimeMs) return;
    } else if (Date.now() - srcStat.mtimeMs > 7 * 24 * 3600 * 1000) {
      return;
    }
    mkdirSync(dirname(dest), { recursive: true });
    copyFileSync(src, dest);
    utimesSync(dest, srcStat.atime, srcStat.mtime);
  } catch {}
}

// rollout先頭のsession_metaからcwdを読む（行が巨大なのでJSONパースせずに正規表現で抜く）
// Read cwd from the rollout's session_meta; the line is huge, so regex instead of JSON parse.
function readCwdOfRollout(path) {
  // 外部境界: 読み取り失敗は「対象外」として扱う
  // External boundary: treat read failures as "not a target".
  try {
    const fd = openSync(path, "r");
    const buf = Buffer.alloc(8192);
    const len = readSync(fd, buf, 0, 8192, 0);
    closeSync(fd);
    const firstLine = buf.toString("utf8", 0, len).split("\n")[0];
    return firstLine.match(/"cwd":"([^"]+)"/)?.[1] ?? "";
  } catch {
    return "";
  }
}

// ディレクトリならtrue（stat失敗はfalse）
// True for directories; stat failures yield false.
function isDirectory(path) {
  // 外部境界: 他プロセス管理下のためstat失敗は握りつぶす
  // External boundary: swallow stat failures on files owned by other processes.
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
}

// ファイル/ディレクトリのmtimeが今からsec秒以内ならtrue
// True when the file/dir mtime is within sec seconds from now.
function isNewerThan(path, sec) {
  // 外部境界: stat失敗は「古い」扱い
  // External boundary: treat stat failure as "old".
  try {
    return Date.now() - statSync(path).mtimeMs < sec * 1000;
  } catch {
    return false;
  }
}

// 読めないディレクトリは空配列を返すreaddir
// readdir that returns [] for unreadable directories.
function safeReaddir(dir) {
  // 外部境界: 存在しない/権限のないディレクトリはfail open
  // External boundary: fail open on missing/unreadable directories.
  try {
    return readdirSync(dir);
  } catch {
    return [];
  }
}

// gitコマンドの薄いラッパー。失敗はnull/空文字で返す
// Thin git wrapper; failures come back as null/empty string.
function git(dir, args, timeout) {
  // 外部境界: 外部プロセス起動の失敗はfail open
  // External boundary: fail open on external process failures.
  try {
    return execFileSync("git", ["-C", dir, ...args], {
      encoding: "utf8",
      timeout: timeout ?? 10000,
      stdio: ["ignore", "pipe", "ignore"],
    }).trim();
  } catch {
    return null;
  }
}
