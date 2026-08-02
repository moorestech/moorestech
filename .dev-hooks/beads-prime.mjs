#!/usr/bin/env node
// Beads台帳の概況と役割分担ルールをセッション開始時に注入する（bd prime相当の自前版）
// Inject the beads ledger digest and role-split rules at session start (our own take on bd prime).
//
// 登録: Claude/Codex SessionStart・Codex PostCompact。bd不在/.beads不在なら沈黙(fail-open)
// Wired to Claude/Codex SessionStart and Codex PostCompact; silent without bd or .beads.

import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";

function bail() {
  process.exit(0);
}

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let input = {};
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {}

const cwd = input.cwd || process.env.CLAUDE_PROJECT_DIR || process.cwd();
if (!existsSync(join(cwd, ".beads"))) bail();

const ready = bdJson(["ready", "--json"]);
const inProgress = bdJson(["list", "--status", "in_progress", "--json"]);
if (ready === null && inProgress === null) bail();

// ready上位と進行中claimを1行ずつに要約する（注入は簡潔に保つ）
// Summarize top ready items and active claims one line each; keep the injection lean.
const readyLines = (ready ?? []).slice(0, 8).map(formatIssue);
const inProgressLines = (inProgress ?? []).map((issue) => {
  const staleDays = daysSince(issue.updated_at);
  const stale = staleDays >= 7 ? ` ⚠stale(${staleDays}日放置)` : "";
  return formatIssue(issue) + ` @${issue.assignee || "?"}` + stale;
});

const lines = [
  "<beads-ledger>",
  "タスク台帳bd(Beads)の概況。タスク・設計検討・学び・派生発見はbdへ記録する。ユーザー裁定の蒸留は.decisions/が正で、bd側からは[[ファイル名]]で参照する。",
  `ready: ${(ready ?? []).length}件` + (readyLines.length > 0 ? "" : "（着手可能なタスク無し）"),
  ...readyLines,
  `in_progress: ${(inProgress ?? []).length}件`,
  ...inProgressLines,
  "",
  "使い方: 着手前にbd createで積む → bd update <id> --claim → 経緯・失敗はbd note <id> → bd close <id> --reason。応答末尾に「LEARN: <一行>」と書くとhookが自動でnote保存する。bd editは対話エディタを開くため禁止。秘密情報は書かない。",
  "</beads-ledger>",
];
console.log(lines.join("\n"));
bail();

// issueを「- id [P2] title」の1行へ整形する
// Format an issue as a single "- id [P2] title" line.
function formatIssue(issue) {
  return `- ${issue.id} [P${issue.priority}] ${issue.title}`;
}

// ISO時刻から経過日数を求める（不正値は0日扱い）
// Days elapsed since an ISO timestamp (invalid values count as 0 days).
function daysSince(iso) {
  const ms = Date.now() - new Date(iso ?? Date.now()).getTime();
  return Number.isFinite(ms) ? Math.floor(ms / (24 * 3600 * 1000)) : 0;
}

// bdをJSON出力で叩く。失敗はnull（bd未インストール等はfail open）
// Run bd with JSON output; failures yield null (missing bd etc. fail open).
function bdJson(args) {
  // 外部境界: 外部プロセス起動とそのJSONパース失敗はfail open
  // External boundary: fail open on process launch or JSON parse failures.
  try {
    const out = execFileSync("bd", args, {
      cwd,
      encoding: "utf8",
      timeout: 10000,
      stdio: ["ignore", "pipe", "ignore"],
    });
    const parsed = JSON.parse(out);
    return Array.isArray(parsed) ? parsed : (parsed?.issues ?? []);
  } catch {
    return null;
  }
}
