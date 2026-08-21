#!/usr/bin/env node
// ポーリング連打ガード: 同一Bashコマンドの連続実行を拒否し、正しい待ち方を再注入する
// Poll guard: denies consecutive identical Bash commands and re-injects the correct way to wait.
//
// 目的: サブエージェント完了待ちを `ls` 等の同一コマンド連打でポーリングする退行を機械的に
// 止める(2026-08-15にcmux-connector側セッションで数十回のlsポーリングが実際に起きた。
// 指示文言だけでは防げなかったのでdenyで構造的に遮断する。cmux-connectorのpoll-guard.shと
// 同一ロジックのNode移植 — このrepoはmac/windows共通でnode前提のため)。
// Why: mechanically stop the regression of polling for subagent completion by repeating the
// same command; instructions alone did not prevent it. Node port of cmux-connector's
// poll-guard.sh because this repo's hooks are node-based for mac/windows parity.
//
// 判定: 他ツール呼び出しを挟まず直前と完全一致するBashコマンドがTHRESHOLD回連続したらdeny。
// deny後はカウンタを0に戻す(恒久ブロックにしない — 主目的は指示の再注入で、一時的失敗の
// 正当な再試行を殺さないため)。Bash以外のツールが挟まればリセットされるため、テスト→修正→
// テストの反復は誤検知しない。意図的な反復は1コマンド内のfor/untilで書けば通る。
// Detection: deny when an identical Bash command repeats THRESHOLD times with no other tool
// in between; the counter resets after a deny and on any non-Bash tool call.
//
// 所有する状態: ~/.cache/claude-poll-guard/<session>__<transcript basename>(JSON {count,prev})。
// 鍵にtranscriptを含めるのは、並列subagentがhook入力で親と同一session_idを共有するため
// (cmux-connector側で実測)。session単独鍵だと並列エージェント間でカウンタが相互汚染する。
// State: keyed by session + transcript basename because parallel subagents share the parent's
// session_id in hook input (measured); a session-only key cross-contaminates counters.
//
// 全失敗経路はexit 0・無出力(エージェントを止めない。beads-guard等と同じfail open思想)。
// Every failure path exits 0 silently (fail open, same philosophy as beads-guard).

import { readFileSync, writeFileSync, mkdirSync, readdirSync, statSync, unlinkSync, rmSync, existsSync } from "node:fs";
import { homedir } from "node:os";
import { join, basename } from "node:path";

const THRESHOLD = 3;
// Claudeは"Bash"、Codexのシェル系ツール名も同列に扱う(Codex側はhooks.json未登録のうちは不発)
// "Bash" for Claude; Codex shell tool names accepted for future wiring.
const BASH_TOOLS = /^(Bash|shell|local_shell|exec|exec_command)$/;

let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {
  process.exit(0);
}

const session = typeof input?.session_id === "string" ? input.session_id : "";
const transcript = typeof input?.transcript_path === "string" ? input.transcript_path : "";
const tool = typeof input?.tool_name === "string" ? input.tool_name : "";
// Claudeはtool_input.command、Codexのexecツールはtool_input.cmd
// Claude carries the command in tool_input.command; Codex's exec tool uses tool_input.cmd.
const rawCmd = input?.tool_input?.command ?? input?.tool_input?.cmd;
if (!session || !tool) process.exit(0);

const home = homedir();
if (!home) process.exit(0);
const stateDir = join(home, ".cache", "claude-poll-guard");
const key = `${session}__${basename(transcript)}`.replace(/[\/\\]/g, "_");
const stateFile = join(stateDir, key);

// Bash以外のツールが挟まったら連続カウントをリセット
// Any non-Bash tool call resets the streak.
if (!BASH_TOOLS.test(tool)) {
  try {
    rmSync(stateFile, { force: true });
  } catch {}
  process.exit(0);
}
if (typeof rawCmd !== "string" || rawCmd === "") process.exit(0);

try {
  // dir作成と古いstateの掃除はstate新規作成時(エージェントの初回Bash)だけ払う
  // Pay for mkdir + stale-state cleanup only when creating this agent's state file.
  if (!existsSync(stateFile)) {
    mkdirSync(stateDir, { recursive: true });
    const cutoff = Date.now() - 24 * 60 * 60 * 1000;
    for (const name of readdirSync(stateDir)) {
      const p = join(stateDir, name);
      try {
        if (statSync(p).mtimeMs < cutoff) unlinkSync(p);
      } catch {}
    }
  }

  let count = 0;
  let prev = "";
  try {
    const st = JSON.parse(readFileSync(stateFile, "utf8"));
    if (Number.isInteger(st?.count) && st.count >= 0) count = st.count;
    if (typeof st?.prev === "string") prev = st.prev;
  } catch {}

  count = rawCmd === prev ? count + 1 : 1;
  // 書き込み失敗はcatchでexit 0に落ちる = denyまで進まない(fail open)
  // A write failure lands in the outer catch and exits 0, never reaching the deny.
  writeFileSync(stateFile, JSON.stringify({ count, prev: rawCmd }));

  if (count >= THRESHOLD) {
    writeFileSync(stateFile, JSON.stringify({ count: 0, prev: rawCmd }));
    console.error(
      `poll-guard: 同一Bashコマンドが${count}回連続しています(ポーリングと判定して拒否。` +
        "カウンタはリセット済みで、次の同一コマンドは通る)。サブエージェント/バックグラウンド" +
        "タスクの完了待ちなら、ポーリングせずにターンを終了してtask-notificationを待つこと。" +
        "状態の出現をどうしても監視する必要があるなら、Monitorツールにuntilループを1本だけ" +
        "渡すこと(例: until [ -f <path> ]; do sleep 5; done)。意図的な反復(flake確認・" +
        "一時的失敗の再試行等)やMonitorが無い環境では、1回のBash呼び出し内でfor/untilループ" +
        "として書くこと。"
    );
    process.exit(2);
  }
} catch {
  process.exit(0);
}
process.exit(0);
