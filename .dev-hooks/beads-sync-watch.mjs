#!/usr/bin/env node
// bdコマンドの実行結果を監視し、同期障害の復旧誘導とセッション出自の刻印を行う
// Watch bd command results: guide sync-failure recovery and stamp session provenance.
//
// 登録: Claude/Codex PostToolUse(シェル系)。bd無関係のコマンドは即終了(fail-open)
// Wired to Claude/Codex PostToolUse (shell tools); exits fast for non-bd commands.

import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { hostname } from "node:os";

function bail() {
  process.exit(0);
}

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {}

const command = input?.tool_input?.command;
if (typeof command !== "string" || !/\b(bd|dolt)\b/.test(command)) bail();

const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
const sessionId = input.session_id || input.thread_id || "unknown";
const output = flattenResponse(input.tool_response);

stampClaim();
stampCreated();
const guidance = detectSyncFailure();
if (guidance) {
  console.log(
    JSON.stringify({
      hookSpecificOutput: { hookEventName: "PostToolUse", additionalContext: "beads-sync-watch: " + guidance },
    })
  );
}
bail();

// claim成功時にclaim_session/claim_hostを刻印する（LEARN自動保存の宛先解決に使う）
// Stamp claim_session/claim_host on successful claims (used to resolve LEARN targets).
function stampClaim() {
  const claim = command.match(/\bbd\s+update\s+(\S+)[^\n]*--claim\b/);
  if (!claim || /error/i.test(output)) return;
  bdQuiet(["update", claim[1], "--set-metadata", `claim_session=${sessionId}`, "--set-metadata", `claim_host=${hostname()}`]);
}

// create成功時に出力からissue IDを拾い、作成セッションを刻印する
// On successful create, pick the issue ID from output and stamp the creating session.
function stampCreated() {
  if (!/\bbd\s+create\b/.test(command)) return;
  const created = output.match(/\b(moorestech-[a-z0-9]+(?:\.\d+)*)\b/);
  if (!created) return;
  bdQuiet(["update", created[1], "--set-metadata", `created_session=${sessionId}`, "--set-metadata", `created_host=${hostname()}`]);
}

// Dolt同期の代表的な障害を検知し、moorestech向けの復旧手順を返す
// Detect representative Dolt sync failures and return moorestech-specific recovery steps.
function detectSyncFailure() {
  if (/Error 1105|concurrent\s+(?:update|modification)/i.test(output)) {
    return "Dolt並行更新競合を検知。まず bd dolt pull → 再実行。解決しない場合はユーザーに確認のうえ bd export でバックアップ後 BD_CONFIRMED=1 bd bootstrap --yes（remote優先の再構築）";
  }
  if (/\[rejected\]|non-fast-forward|failed to push/i.test(output)) {
    return "bd dolt pushが非fast-forwardで拒否された。bd dolt pull → bd dolt push の順で再試行（並行セッションがある時の正常な競合）";
  }
  if (/workspace identity mismatch/i.test(output)) {
    return "workspace identity mismatchを検知。bd statsで実状態を確認し、ユーザーへ報告してから復旧すること";
  }
  return null;
}

// tool_responseの形状差（文字列/オブジェクト）を吸収してテキスト化する
// Flatten tool_response shape differences (string/object) into text.
function flattenResponse(response) {
  if (typeof response === "string") return response;
  // 外部境界: 予期しない形状はJSON文字列化で吸収し、失敗は空文字にfail open
  // External boundary: absorb unexpected shapes via JSON stringify; fail open to "".
  try {
    return JSON.stringify(response ?? "");
  } catch {
    return "";
  }
}

// bdを静かに叩く。失敗しても本体の流れは止めない
// Run bd quietly; failures never interrupt the main flow.
function bdQuiet(args) {
  // 外部境界: 外部プロセス起動の失敗はfail open
  // External boundary: fail open on external process failures.
  try {
    execFileSync("bd", args, { cwd, timeout: 10000, stdio: "ignore" });
  } catch {}
}
