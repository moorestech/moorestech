#!/usr/bin/env node
// メインワークツリー上でのブランチ作成・HEAD移動を物理拒否し、タスク毎worktreeへ誘導するガード
// Guard that denies branch creation and HEAD moves in the main worktree, steering work to per-task worktrees.
//
// なぜ要るか: セッションランチャがどのセッションもメインクローンのcwdで起動するため、worktreeを切り忘れた
// セッションが同一チェックアウトのHEADを奪い合い、規約の明文化だけでは実際にすり抜けが起きた（2026-08-18）。
// Why: the session launcher starts every session in the main clone, so sessions that skip the worktree step
// fight over one checkout's HEAD; documentation alone failed to prevent this in practice (2026-08-18).
//
// ---------------------------------------------------------------------------
// 使い方 / Usage
// ---------------------------------------------------------------------------
// 既定はOFF。tracked な .claude/settings.json には登録せず、環境ごとに任意で有効化する運用。
// 「タスク毎に使い捨てworktreeを切る」ローカル規約を敷いている環境でのみ入れる。
// Off by default: never wire it into the tracked .claude/settings.json; enable it per environment,
// only where the local convention is to branch inside a disposable worktree.
//
// 有効化 — .claude/settings.local.json（gitignore済み＝そのマシン限定）へ次を足す:
// Enable by adding this to .claude/settings.local.json (gitignored, so it stays machine-local):
//
//   {
//     "hooks": {
//       "PreToolUse": [
//         {
//           "matcher": "Bash",
//           "hooks": [
//             { "type": "command", "command": "node \"${CLAUDE_PROJECT_DIR}/.dev-hooks/main-worktree-guard.mjs\"" }
//           ]
//         }
//       ]
//     }
//   }
//
// 反映は即時（稼働中セッションも再起動不要）。無効化は上記ブロックを消すだけ。
// Takes effect immediately, including in running sessions; disable by deleting that block.
//
// 復旧 — gitignore対象（settings.local.json / CLAUDE.local.md 等）を消してしまった場合は、
// 本ファイルは tracked なので上のJSONを貼り直せば元に戻る。設定以外の状態は持たない。
// Recovery: this file is tracked, so re-pasting the JSON above fully restores the guard after
// untracked files (settings.local.json, CLAUDE.local.md, ...) are lost. It keeps no other state.
//
// 動作確認 — メインワークツリーで次を実行し、拒否されればOK（ブランチは作られない）:
// Smoke test: run this in the main worktree; it is working if the command is denied and no branch appears:
//   git branch tmp/guard-probe
//
// 明示上書き: コマンドに MOORES_MAIN_WT_OK=1 を前置する
// Explicit override: prefix the command with MOORES_MAIN_WT_OK=1.
// ---------------------------------------------------------------------------

import { execFileSync } from "node:child_process";
import { readFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";

function bail() {
  process.exit(0);
}

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {}

const command = input?.tool_input?.command ?? input?.tool_input?.cmd;
if (typeof command !== "string") bail();
if (/\bMOORES_MAIN_WT_OK=1\b/.test(command)) bail();

// コマンド位置（行頭・;・&&・|・環境変数前置の直後）のgitだけに反応する
// React only to git at command positions (line start, ;, &&, |, after env-var prefixes).
const COMMAND_PREFIX = "(?:^|[;&|(]\\s*|\\n\\s*)(?:[A-Z_]+=\\S+\\s+)*";
const gitCalls = [...command.matchAll(new RegExp(COMMAND_PREFIX + "(git\\b[^\\n;|&]*)", "gm"))].map((m) => m[1]);
if (gitCalls.length === 0) bail();

// 対象repoの特定: git -C <path> があればそちら、なければセッションcwd
// Locate the target repo: honor git -C <path> if present, else the session cwd.
const cwd = input?.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();

for (const call of gitCalls) {
  const violation = inspect(call);
  if (!violation) continue;
  process.stderr.write(
    [
      "main-worktree-guard: メインワークツリーでの" + violation.what + "は禁止（ローカル運用規約 CLAUDE.local.md）。",
      "  拒否したコマンド: " + call.trim(),
      "  並列セッションが同一チェックアウトのHEADを奪い合うため、タスク毎の使い捨てworktreeで作業すること:",
      "    moores-wt new <branch>   # 環境ローカルツール。Library/PersonalAssetsのコピー込み",
      "  低リスク作業（調査・閲覧）で本当にメイン上で実行する場合のみ MOORES_MAIN_WT_OK=1 を前置する。",
      "",
    ].join("\n")
  );
  process.exit(2);
}

bail();

// 1つのgit呼び出しを検査し、メインワークツリー上の禁止操作なら理由を返す
// Inspect a single git invocation and return a reason when it is a forbidden op in the main worktree.
function inspect(call) {
  const cMatch = call.match(/\bgit\s+-C\s+(\S+)/);
  const repoDir = cMatch ? resolve(cwd, unquote(cMatch[1])) : cwd;

  const args = tokenize(call).slice(1);
  if (cMatch) args.splice(args.indexOf("-C"), 2);
  const sub = args.find((a) => !a.startsWith("-"));
  if (!["checkout", "switch", "branch"].includes(sub)) return null;

  const rest = args.slice(args.indexOf(sub) + 1);
  const flags = rest.filter((a) => a.startsWith("-"));
  const operands = rest.filter((a) => !a.startsWith("-")).map(unquote);

  // 分岐作成・HEAD移動のみを対象にする。一覧・削除・パス復元は通す
  // Target only branch creation and HEAD moves; pass listing, deletion, and path restores.
  let what = null;
  if (sub === "branch") {
    const readOnly = /^-(d|D|l|a|r|v+|-list|-delete|-show-current|-contains|-merged|-no-merged|-set-upstream-to|-unset-upstream|-format|-sort)$/;
    if (flags.some((f) => readOnly.test(f)) || operands.length === 0) return null;
    what = "ブランチ作成(git branch)";
  } else if (flags.some((f) => /^-(b|B|c|C)$/.test(f))) {
    what = "ブランチ作成(git " + sub + ")";
  } else if (rest.includes("--") || operands.length === 0) {
    return null;
  } else if (isRef(repoDir, operands[0]) && !existsSync(resolve(repoDir, operands[0]))) {
    what = "HEAD移動(git " + sub + ")";
  } else {
    return null;
  }

  // publicのmoorestech本体のメインワークツリーだけを守る。linked worktree・他repo・判定不能は通す
  // Protect only the main worktree of the public moorestech repo; pass linked worktrees, other repos, and undeterminable cases.
  const origin = git(repoDir, ["remote", "get-url", "origin"]);
  if (origin === null || !/github\.com[/:]moorestech\/moorestech(\.git)?$/.test(origin.trim())) return null;
  const gitDir = git(repoDir, ["rev-parse", "--absolute-git-dir"]);
  const commonDir = git(repoDir, ["rev-parse", "--path-format=absolute", "--git-common-dir"]);
  if (gitDir === null || commonDir === null || gitDir.trim() !== commonDir.trim()) return null;

  return { what };
}

// クォートを剥がす。シェル展開は解釈しない
// Strip quotes; shell expansion is not interpreted.
function unquote(s) {
  return s.replace(/^["']|["']$/g, "");
}

function tokenize(call) {
  return (call.match(/"[^"]*"|'[^']*'|\S+/g) ?? []).filter(Boolean);
}

function isRef(repoDir, name) {
  return git(repoDir, ["rev-parse", "--verify", "--quiet", name + "^{commit}"]) !== null;
}

// gitを叩く。失敗はnull（判定不能時は止めない）
// Run git; null on failure (do not block when undeterminable).
function git(repoDir, args) {
  // 外部境界: 外部プロセス起動の失敗はfail open
  // External boundary: fail open on external process failures.
  try {
    return execFileSync("git", ["-C", repoDir, ...args], { encoding: "utf8", timeout: 5000, stdio: ["ignore", "pipe", "ignore"] });
  } catch {
    return null;
  }
}
