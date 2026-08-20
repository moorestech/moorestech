#!/usr/bin/env node
// public repoへのgit commit直前に、持ち込まれる内容から個人情報・秘密情報を検知して物理拒否するガード
// Guard that inspects content entering the public repo right before git commit and physically denies leaks.
//
// 登録: Claude PreToolUse(Bash)。sakastudio機でのみ有効、GUARD_CONFIRMED=1の前置で明示的に上書きできる
// Wired to Claude PreToolUse (Bash); active only on the sakastudio machine, overridable with GUARD_CONFIRMED=1.

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { homedir } from "node:os";
import { resolve } from "node:path";

function bail() {
  process.exit(0);
}

// 守る対象はsakastudio機の1台のみ。他マシンでは一切検査しない
// Only the sakastudio machine is guarded; every other machine skips inspection entirely.
if (homedir() !== "/Users/sakastudio") bail();

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {}

const command = input?.tool_input?.command ?? input?.tool_input?.cmd;
if (typeof command !== "string") bail();

// コマンド位置（行頭・;・&&・|・環境変数前置の直後）のgit commitだけに反応する
// React only to git commit at command positions (line start, ;, &&, |, after env-var prefixes).
const commandPrefix = "(?:^|[;&|(]\\s*|\\n\\s*)(?:[A-Z_]+=\\S+\\s+)*";
if (!new RegExp(commandPrefix + "git\\b[^\\n;|&]*\\bcommit\\b", "m").test(command)) bail();
if (/\bGUARD_CONFIRMED=1\b/.test(command)) bail();

// 対象repoの特定: git -C <path> があればそちら、なければセッションcwd
// Locate the target repo: honor git -C <path> if present, else the session cwd.
const cwd = input?.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
const cMatch = command.match(new RegExp(commandPrefix + "git\\s+-C\\s+(\\S+)", "m"));
const repoDir = cMatch ? resolve(cwd, cMatch[1].replace(/^["']|["']$/g, "")) : cwd;

// publicのmoorestech本体だけを守る。private repo（logs等）や判定不能時は通す
// Protect only the public moorestech repo; pass private repos (logs etc.) and undeterminable cases.
const originUrl = git("remote", "get-url", "origin");
if (originUrl === null || !/github\.com[/:]moorestech\/moorestech(\.git)?$/.test(originUrl.trim())) bail();

// 検知ルール: 個人パス・非noreplyメール・transcript痕跡・秘密情報
// Detection rules: personal paths, non-noreply emails, transcript traces, secrets.
const EMAIL_ALLOW = /noreply@anthropic\.com|users\.noreply\.github\.com|git@github\.com|example\.(com|org)/;
const rules = [
  ["個人の絶対パス / personal absolute path", /\/(Users|home)\/[A-Za-z0-9._-]+\//],
  ["メールアドレス / email address", /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/, (m) => !EMAIL_ALLOW.test(m)],
  ["セッションtranscriptの痕跡 / session transcript trace", /"parentUuid"\s*:|"isSidechain"\s*:/],
  [
    "秘密情報らしき文字列 / secret-looking string",
    /-----BEGIN [A-Z ]*PRIVATE KEY-----|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-ant-[A-Za-z0-9-]{20,}|xox[baprs]-[A-Za-z0-9-]{10,}|AKIA[0-9A-Z]{16}/,
  ],
];

// 検査除外: 本ガード自身（ルール文字列を含む）とサードパーティアセット
// Scan exemptions: this guard itself (contains rule strings) and third-party assets.
const EXEMPT = [/^\.dev-hooks\/commit-guard\.mjs$/, /^moorestech_client\/Assets\/Dependencies\//];

// ローカル専用としてgitignore済みのパスは、-f等での混入自体を拒否する
// Paths already gitignored as local-only are denied outright even if forced in with -f.
const FORBIDDEN =
  /^(\.mso\/|\.claude\/memory\/|\.claude\/settings\.local\.json$|\.agents\/skills\/user-simulator\/datasets\/|\.agents\/skills\/moores-code-review\/records\/|TmpUnityPjt\/MapMaking\/RestoreManifest\/)/;

const findings = [];
for (const path of (git("diff", "--cached", "--name-only") ?? "").split("\n")) {
  if (FORBIDDEN.test(path)) findings.push({ path, rule: "ローカル専用パスの混入 / local-only path", text: "" });
}

// staged差分の追加行を検査。同一コマンド内にgit addがあるなら未stage差分と未追跡ファイルも見る
// Scan added lines of the staged diff; if the same command runs git add, also scan unstaged diff and untracked files.
scanDiff(git("diff", "--cached", "-U0"));
const stagesMore =
  new RegExp(commandPrefix + "git\\b[^\\n;|&]*\\badd\\b", "m").test(command) ||
  new RegExp(commandPrefix + "git\\b[^\\n;|&]*\\bcommit\\b[^\\n;|&]*\\s(-a\\b|-am\\b|--all\\b)", "m").test(command);
if (stagesMore) {
  scanDiff(git("diff", "-U0"));
  for (const path of (git("ls-files", "--others", "--exclude-standard") ?? "").split("\n")) {
    if (path) scanFile(path);
  }
}

if (findings.length > 0) {
  const lines = findings
    .slice(0, 10)
    .map((f) => `  ${f.path}: ${f.rule}${f.text ? ` → ${f.text.slice(0, 120)}` : ""}`);
  console.error(
    `commit-guard: public repoへ持ち込めない内容を検知したためコミットを拒否した（${findings.length}件）\n` +
      lines.join("\n") +
      "\n該当箇所を除去するか、誤検知ならユーザーに確認のうえ GUARD_CONFIRMED=1 を前置して再実行すること。"
  );
  process.exit(2);
}

bail();

// 統一diff内の追加行へルールを適用する。+++行から対象ファイルを追跡する
// Apply rules to added lines in a unified diff, tracking the current file from +++ lines.
function scanDiff(diff) {
  if (!diff) return;
  let path = "";
  for (const line of diff.split("\n")) {
    if (line.startsWith("+++ b/")) {
      path = line.slice(6);
      continue;
    }
    if (!line.startsWith("+") || line.startsWith("+++")) continue;
    if (EXEMPT.some((re) => re.test(path))) continue;
    matchRules(path, line.slice(1));
  }
}

// 未追跡ファイルを全文検査する。バイナリと巨大ファイルは対象外
// Scan an untracked file's full content, skipping binaries and oversized files.
function scanFile(path) {
  if (EXEMPT.some((re) => re.test(path))) return;
  // 外部境界: 任意ファイルの読み取り失敗（権限・エンコーディング等）はfail open
  // External boundary: fail open on arbitrary file read failures (permissions, encoding, etc.).
  let content = "";
  try {
    content = readFileSync(resolve(repoDir, path), "utf8");
  } catch {
    return;
  }
  if (content.length > 512 * 1024 || content.includes("\0")) return;
  for (const line of content.split("\n")) matchRules(path, line);
}

// 1行に許可対象と違反が同居しても取りこぼさないよう、全マッチを個別判定する
// Evaluate every match individually so an allowed hit on the same line cannot mask a violation.
function matchRules(path, text) {
  for (const [rule, re, accept] of rules) {
    for (const m of text.matchAll(new RegExp(re.source, "g"))) {
      if (!accept || accept(m[0])) {
        findings.push({ path, rule, text: text.trim() });
        break;
      }
    }
  }
}

// 対象repoでgitを実行する。失敗はnull（判定不能時はコミットを止めない）
// Run git in the target repo; null on failure (don't block commits when undeterminable).
function git(...args) {
  // 外部境界: 外部プロセス起動の失敗はfail open
  // External boundary: fail open on external process failures.
  try {
    return execFileSync("git", args, {
      cwd: repoDir,
      encoding: "utf8",
      timeout: 15000,
      maxBuffer: 64 * 1024 * 1024,
      stdio: ["ignore", "pipe", "ignore"],
    });
  } catch {
    return null;
  }
}
