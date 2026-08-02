#!/usr/bin/env node
// 破壊的なbd/doltコマンドを物理的に拒否する安全ガード
// Safety guard that physically denies destructive bd/dolt commands.
//
// 登録: Claude PreToolUse(Bash)。BD_CONFIRMED=1を前置すると明示的に上書きできる
// Wired to Claude PreToolUse (Bash); prefix the command with BD_CONFIRMED=1 to override.

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";

function bail() {
  process.exit(0);
}

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {}

// Claudeはtool_input.command、Codexのexecツールはtool_input.cmdにコマンドが載る
// Claude carries the command in tool_input.command; Codex's exec tool uses tool_input.cmd.
const command = input?.tool_input?.command ?? input?.tool_input?.cmd;
if (typeof command !== "string" || !/\b(bd|dolt)\b/.test(command)) bail();

// 明示的な上書き。ユーザー承認済みの復旧作業だけがここを通る想定
// Explicit override; only user-approved recovery work is expected to pass here.
if (/\bBD_CONFIRMED=1\b/.test(command)) bail();

// 拒否パターン: データ削除・履歴破壊・強制同期・対話エディタ・二重init
// Deny patterns: data deletion, history destruction, forced sync, interactive editor, re-init.
const denyRules = [
  ["bd\\s+(delete|purge|prune|flatten|compact|gc|rename)\\b", "issueデータを不可逆に破壊しうる"],
  ["bd\\s+admin\\s+(reset|cleanup|compact)\\b", "DB全体を破壊しうる管理コマンド"],
  ["bd\\s+init\\b", "初期化済み。再initはDB・設定を巻き戻しうる"],
  ["(?:bd\\s+sql|dolt\\s+sql)\\b", "SQL直叩きは整合性を壊しうる"],
  ["(?:bd\\s+)?dolt\\s+push\\b[^\\n]*--force", "強制pushは他セッションの記録を消す"],
  ["dolt\\s+reset\\b[^\\n]*--hard", "Dolt履歴の巻き戻しは考古学記録を消す"],
  ["bd\\s+hooks\\s+install\\b", "git hooksは導入しない裁定（本体repoのgitフローを汚さない）"],
  ["bd\\s+edit\\b", "対話エディタが開きエージェントが停止する。bd update <id> --title/--description等を使う"],
];

// コマンド位置（行頭・;・&&・|・環境変数前置の直後）だけに一致させ、文字列内の言及を誤検知しない
// Match only at command positions (line start, ;, &&, |, after env-var prefixes), not mere mentions.
const commandPrefix = "(?:^|[;&|(]\\s*|\\n\\s*)(?:[A-Z_]+=\\S+\\s+)*";

for (const [core, reason] of denyRules) {
  const pattern = new RegExp(commandPrefix + core, "m");
  if (pattern.test(command)) {
    console.error(
      `beads-guard: このコマンドは拒否した（${reason}）。` +
        "本当に必要ならユーザーに確認のうえ BD_CONFIRMED=1 を前置して再実行すること。"
    );
    process.exit(2);
  }
}

// public repoへの誤送信ガード: dolt remoteがprivateのmoorestech_logsを向いている時だけpushを許す
// Public-leak guard: allow dolt push only when the dolt remote targets the private moorestech_logs.
if (new RegExp(commandPrefix + "bd\\s+dolt\\s+push\\b", "m").test(command)) {
  const remotes = doltRemoteList();
  if (remotes !== null && !remotes.includes("moorestech_logs")) {
    console.error(
      "beads-guard: dolt remoteがprivateのmoorestech_logsを向いていない（issueデータがpublic repoへ漏れる）。" +
        "bd dolt remote remove origin → bd dolt remote add origin git+ssh://git@github.com/moorestech/moorestech_logs.git で修正してからpushすること。"
    );
    process.exit(2);
  }
}

bail();

// dolt remote一覧を取得する。失敗はnull（判定不能時はpushを止めない）
// Fetch the dolt remote list; null on failure (don't block pushes when undeterminable).
function doltRemoteList() {
  // 外部境界: 外部プロセス起動の失敗はfail open
  // External boundary: fail open on external process failures.
  try {
    return execFileSync("bd", ["dolt", "remote", "list"], {
      cwd: process.env.CLAUDE_PROJECT_DIR || process.cwd(),
      encoding: "utf8",
      timeout: 10000,
      stdio: ["ignore", "pipe", "ignore"],
    });
  } catch {
    return null;
  }
}
