#!/usr/bin/env node
// private の考古学 repo（兄弟の moorestech_logs）にあるフックを呼ぶだけの入口。無い環境では何もしない
// Entry point that only forwards to hooks living in the private archaeology repo (sibling moorestech_logs); a no-op elsewhere.
//
// 使い方: node .dev-hooks/logs-hook.mjs <name> [args...]  → ../moorestech_logs/scripts/hooks/<name>.mjs を同じ標準入力・引数で実行
// Usage: node .dev-hooks/logs-hook.mjs <name> [args...] runs ../moorestech_logs/scripts/hooks/<name>.mjs with the same stdin and args.
//
// 本体は logs repo 側に置く（記録の集め方は logs repo の持ち主の都合で、全 worktree が常に最新版を使えるように）
// The implementation lives in the logs repo: archiving is its owner's concern, and every worktree then runs the latest version.

import { execFileSync, spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";

const [name, ...args] = process.argv.slice(2);
// silent-ok: 標準入力が無い・読めない時は空で渡し、呼ばれた側が入力なしとして扱う / 呼ばれた側の失敗は logs repo の skipped.log に出る
let stdin = "";
try {
  stdin = readFileSync(0, "utf8");
} catch {}

const script = name && /^[a-z-]+$/.test(name) ? findHook(name) : null;
if (script) spawnSync(process.execPath, [script, ...args], { input: stdin, stdio: ["pipe", "inherit", "inherit"] });
process.exit(0);

// git common dir 経由で本体 repo を特定し、兄弟の logs repo にあるフックのパスを返す（worktree 対応）
// Locate the main repo via the git common dir and return the hook path in its sibling logs repo (worktree-safe).
function findHook(hookName) {
  let cwd = process.env.CLAUDE_PROJECT_DIR || process.cwd();
  // silent-ok: 入力の JSON が読めなければ環境変数か現在地を使う / 見つからなければ何もしない入口なので欠落は起きない
  try {
    cwd = JSON.parse(stdin).cwd || cwd;
  } catch {}
  let common;
  // silent-ok: git 管理外から呼ばれたらフックの対象外 / 記録する対象が無いので欠落は起きない
  try {
    common = execFileSync("git", ["-C", cwd, "rev-parse", "--git-common-dir"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  } catch {
    return null;
  }
  const mainRoot = dirname(common === ".git" ? join(cwd, ".git") : common);
  const path = join(dirname(mainRoot), "moorestech_logs", "scripts", "hooks", `${hookName}.mjs`);
  return existsSync(path) ? path : null;
}
