#!/usr/bin/env node
// コミット↔AIセッションの結合層: HEADの変化を検知してmap/commit-sessions.tsvへ追記する
// Join layer between commits and AI sessions: detect HEAD changes and append to the map.
//
// 登録: Claude/Codex PostToolUse。引数でagent名を渡す（node commit-map.mjs claude|codex）
// Wired to Claude/Codex PostToolUse; the agent name arrives as argv (claude|codex).

import { execFileSync } from "node:child_process";
import { appendFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { hostname } from "node:os";
import { dirname, join } from "node:path";

function bail() {
  process.exit(0);
}

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {}

const agent = process.argv[2] || "unknown";
const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
const sessionId = input.session_id || input.thread_id || "unknown";

// メインrepoと兄弟のlogs repoを特定（worktree対応）。無ければ沈黙
// Locate the main repo and its sibling logs repo (worktree-safe); silent if absent.
const common = git(cwd, ["rev-parse", "--git-common-dir"]);
if (!common) bail();
const mainRoot = dirname(common === ".git" ? join(cwd, ".git") : common);
const logsRepo = join(dirname(mainRoot), "moorestech_logs");
if (!existsSync(join(logsRepo, ".git"))) bail();

const head = git(cwd, ["rev-parse", "HEAD"]);
if (!head) bail();

// セッション×作業ディレクトリごとに前回HEADを記憶し、変化した時だけ追記する
// Remember the last HEAD per session x working dir; append only on change.
const stateKey = createHash("sha1").update(agent + sessionId + cwd).digest("hex").slice(0, 16);
const stateFile = join(logsRepo, ".state", "head-" + stateKey);
mkdirSync(dirname(stateFile), { recursive: true });
const prevHead = existsSync(stateFile) ? readFileSync(stateFile, "utf8").trim() : "";
if (prevHead === head) bail();
writeFileSync(stateFile, head);
// 初回はHEADを記憶するだけ（セッション開始前からあるコミットは対象外）
// First run only records HEAD; commits predating the session are out of scope.
if (prevHead === "") bail();

// 前回HEAD以降の新規コミットを古い順に列挙（rebase/amendでも到達分は拾える）
// List new commits since the last HEAD, oldest first (rebase/amend still yields reachable ones).
const revList = git(cwd, ["rev-list", "--reverse", "--max-count=50", prevHead + ".." + head]);
const shas = revList ? revList.split("\n").filter(Boolean) : [head];
const branch = git(cwd, ["rev-parse", "--abbrev-ref", "HEAD"]) || "detached";
const time = new Date().toISOString();
const rows = shas.map((sha) => [time, hostname(), branch, sha, agent, sessionId].join("\t") + "\n");

// 外部境界: 追記失敗（logs repo側の一時的な不調）はfail open
// External boundary: fail open when appending fails (transient logs-repo issues).
try {
  appendFileSync(join(logsRepo, "map", "commit-sessions.tsv"), rows.join(""));
} catch {}
bail();

// gitコマンドの薄いラッパー。失敗はnullで返す
// Thin git wrapper; failures come back as null.
function git(dir, args) {
  // 外部境界: 外部プロセス起動の失敗はfail open
  // External boundary: fail open on external process failures.
  try {
    return execFileSync("git", ["-C", dir, ...args], {
      encoding: "utf8",
      timeout: 10000,
      stdio: ["ignore", "pipe", "ignore"],
    }).trim();
  } catch {
    return null;
  }
}
