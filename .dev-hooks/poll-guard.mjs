#!/usr/bin/env node
// ポーリング連打ガード: 同一ツール呼び出しの反復を拒否し、正しい待ち方を再注入する
// Poll guard: denies repeated identical tool calls and re-injects the correct way to wait.
//
// 目的: サブエージェント/バックグラウンド完了待ちを同一呼び出しの連打で見張る退行を機械的に止める。
// 初版はBashコマンドの完全一致連続だけを見ていたが、それでは実際の退行を1件も止められなかった
// (2026-08-17のセッションで ListAgents 593回・最長380回連続・間隔中央値3秒のポーリングが素通り。
// Bash以外は判定対象外で、さらに非Bashツールが来るとカウンタを消していたため、間にListAgentsを
// 挟んだBashの18回反復も検知できなかった)。よって判定は全ツールへ一般化し、リセットは副作用の
// あるツールだけに限定する。指示文言では防げないためdenyで構造的に遮断する。
// Why: instructions alone never stopped the regression — the Bash-only version let 593 ListAgents
// polls (380 consecutive, ~3s apart) through and its "any non-Bash tool resets" rule also erased
// the streak of 18 repeated Bash polls. Detection is now tool-agnostic and only side-effecting
// tools clear the streak.
//
// 判定: ツール名＋引数の指紋が、直近WINDOW件かつWINDOW_SECONDS秒内の非リセット呼び出しの中で
// THRESHOLD回に達したらdeny。時間窓があるので、分単位で離れた正当な同一呼び出しは積み上がらない。
// 交互ポーリング(ls → ListAgents → ls …)も窓内の出現回数で拾える。deny後は窓を空にする
// (恒久ブロックにしない — 主目的は指示の再注入で、正当な反復や一時的失敗の再試行を殺さないため)。
// Edit/Write/Agent等の副作用ツールが挟まれば窓ごとリセットされるため、実作業の反復は誤検知しない。
// 意図的な反復は1呼び出し内のfor/untilループで書けば通る。
// Detection: deny when a tool-name+arguments fingerprint occurs THRESHOLD times within the last
// WINDOW non-resetting calls; the window is cleared after a deny and by any side-effecting tool.
//
// 所有する状態: ~/.cache/claude-poll-guard/<自スクリプトpathの短縮hash>__<session>__<transcript basename>
// (JSON {window,last})。鍵に自分のpathを混ぜるのは、同じ内容の写し(ユーザー全体hookとproject hook)が
// 両方登録された環境で1回のツール呼びを二重に数え、閾値より早くdenyしてしまうのを防ぐため。
// The key includes this script's own path so a user-level and a project-level copy of the same hook
// do not both count one tool call into the same window and deny before the threshold.
// 鍵にtranscriptを含めるのは、並列subagentがhook入力で親と同一session_idを共有するため
// (cmux-connector側で実測)。session単独鍵だと並列エージェント間でカウンタが相互汚染する。
// State: keyed by session + transcript basename because parallel subagents share the parent's
// session_id in hook input (measured); a session-only key cross-contaminates counters.
//
// 全失敗経路はexit 0・無出力(エージェントを止めない。beads-guard等と同じfail open思想)。
// Every failure path exits 0 silently (fail open, same philosophy as beads-guard).

import { readFileSync, writeFileSync, mkdirSync, readdirSync, statSync, unlinkSync, rmSync, existsSync } from "node:fs";
import { createHash } from "node:crypto";
import { homedir } from "node:os";
import { fileURLToPath } from "node:url";
import { join, basename } from "node:path";

const THRESHOLD = 3;
const WINDOW = 24;
// sleepを挟む遅いポーリング(間隔15分未満)まで捕まえる長さにする。短くすると素通りする穴になる
// Long enough to catch slow sleep-based polling (under 15 min apart); shorter values become a bypass.
const WINDOW_SECONDS = 1800;
// 実作業を進めるツールだけが窓をリセットする(読み取り専用ツールを挟んだ偽装ポーリングを通さないため)
// Only tools that advance real work reset the window, so read-only calls cannot launder a poll loop.
// SendMessageは除外する — 同文の「終わった?」をsubagentへ反復送るのは実在のポーリング形態だから。
// SendMessage is excluded: repeating the same "are you done?" to a subagent is itself a poll loop.
const RESETTING_TOOLS =
  /^(Edit|Write|MultiEdit|NotebookEdit|apply_patch|Agent|Task|TaskCreate|TaskUpdate|AskUserQuestion|ExitPlanMode|Workflow|Artifact|EnterWorktree|ExitWorktree)$/;

let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {
  process.exit(0);
}

const session = typeof input?.session_id === "string" ? input.session_id : "";
const transcript = typeof input?.transcript_path === "string" ? input.transcript_path : "";
const tool = typeof input?.tool_name === "string" ? input.tool_name : "";
if (!session || !tool) process.exit(0);

const home = homedir();
if (!home) process.exit(0);
const stateDir = join(home, ".cache", "claude-poll-guard");
const scope = createHash("sha1").update(fileURLToPath(import.meta.url)).digest("hex").slice(0, 8);
const key = `${scope}__${session}__${basename(transcript)}`.replace(/[\/\\]/g, "_");
const stateFile = join(stateDir, key);

// 副作用のあるツールが来たら窓ごと破棄する(実作業が進んだので反復の意味が変わる)
// A side-effecting tool discards the window: real work advanced, so repeats mean something else.
if (RESETTING_TOOLS.test(tool)) {
  try {
    rmSync(stateFile, { force: true });
  } catch {}
  process.exit(0);
}

// キー順を固定して指紋化する(同じ引数がオブジェクトの並び順違いで別物にならないように)
// Canonicalize key order so identical arguments cannot hash differently.
function canonical(value) {
  if (value === null || typeof value !== "object") return JSON.stringify(value) ?? "null";
  if (Array.isArray(value)) return "[" + value.map(canonical).join(",") + "]";
  return (
    "{" +
    Object.keys(value)
      .sort()
      .map((k) => JSON.stringify(k) + ":" + canonical(value[k]))
      .join(",") +
    "}"
  );
}

const fingerprint = createHash("sha1").update(tool + "|" + canonical(input?.tool_input ?? {})).digest("hex").slice(0, 16);

try {
  // dir作成と古いstateの掃除はstate新規作成時(エージェントの初回ツール呼び)だけ払う
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

  const now = Date.now();
  let window = [];
  try {
    const st = JSON.parse(readFileSync(stateFile, "utf8"));
    if (Array.isArray(st?.window)) {
      window = st.window.filter(
        (e) => typeof e?.fp === "string" && Number.isFinite(e?.at) && now - e.at <= WINDOW_SECONDS * 1000
      );
    }
  } catch {}

  window.push({ fp: fingerprint, at: now });
  if (window.length > WINDOW) window = window.slice(-WINDOW);
  const hits = window.filter((e) => e.fp === fingerprint).length;
  const denied = hits >= THRESHOLD;
  // 書き込み失敗はcatchでexit 0に落ちる = denyまで進まない(fail open)
  // A write failure lands in the outer catch and exits 0, never reaching the deny.
  writeFileSync(stateFile, JSON.stringify({ window: denied ? [] : window, last: tool }));

  if (denied) {
    console.error(
      `poll-guard: 同一の ${tool} 呼び出し(引数まで一致)が直近${window.length}件中${hits}回出現しています` +
        "(ポーリングと判定して拒否。窓はリセット済みで、次の同一呼び出しは通る)。サブエージェント/" +
        "バックグラウンドタスクの完了待ちなら、ポーリングせずにターンを終了してtask-notificationを" +
        "待つこと(ListAgents・TaskGet・BashOutput・成果物ファイルのlsを含め、待つための反復呼び出しは" +
        "すべて対象)。状態の出現をどうしても監視する必要があるなら、Monitorツールにuntilループを1本" +
        "だけ渡すこと(例: until [ -f <path> ]; do sleep 5; done)。意図的な反復(flake確認・一時的失敗の" +
        "再試行等)やMonitorが無い環境では、1回のBash呼び出し内でfor/untilループとして書くこと。"
    );
    process.exit(2);
  }
} catch {
  process.exit(0);
}
process.exit(0);
