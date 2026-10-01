#!/usr/bin/env node
// Beads台帳の概況と、bd/.decisionsの運用ルールをセッション開始時に注入する（bd prime相当の自前版）
// Inject the beads ledger digest plus the bd/.decisions usage rules at session start (our own take on bd prime).
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

// ready上位と動いている進行中claimだけを1行ずつに要約する
// Summarize top ready items and only the live in-progress claims, one line each.
const staleThresholdDays = 7;
const readyLines = (ready ?? []).slice(0, 8).map(formatIssue);
const liveInProgress = (inProgress ?? []).filter((issue) => daysSince(issue.updated_at) < staleThresholdDays);
const staleCount = (inProgress ?? []).length - liveInProgress.length;
const inProgressLines = liveInProgress.map((issue) => formatIssue(issue) + ` @${issue.assignee || "?"}`);

// 放置claimは件数だけ出す（全列挙は毎セッションの固定費になるため）
// Stale claims are reported as a count only; listing them is a fixed cost on every session.
const staleLine =
  staleCount > 0
    ? [`stale(${staleThresholdDays}日以上更新なし): ${staleCount}件 — 一覧は bd list --status in_progress`]
    : [];

const lines = [
  "<beads-ledger>",
  "タスク台帳bd(Beads)の概況。タスク・設計検討・学び・派生発見はbdへ記録する。ユーザー裁定の蒸留は.decisions/が正で、bd側からは[[ファイル名]]で参照する。",
  `ready: ${(ready ?? []).length}件` + (readyLines.length > 0 ? "" : "（着手可能なタスク無し）"),
  ...readyLines,
  `in_progress: ${(inProgress ?? []).length}件`,
  ...inProgressLines,
  ...staleLine,
  "",
  "使い方: 着手前にbd createで積む → bd update <id> --claim → 経緯・失敗はbd note <id> → bd close <id> --reason。応答末尾に「LEARN: <一行>」と書くとhookが自動でnote保存する。bd editは対話エディタを開くため禁止。秘密情報は書かない。",
  "裁定台帳: 設計・実装の判断前に ls .decisions | grep <語> で過去のユーザー裁定を引き、該当があれば本文を読む。裁定と矛盾する変更は黙って行わずユーザーに諮る。",
  "裁定の記録: AskUserQuestionへの回答に限らず、自由文での却下・修正・方針転換もその場で .decisions/YYYY-MM-DD-<内容>.md へ「決定/棄却案/理由/リンク」の数行で書く。覆った決定は旧ファイルを書き換えず新レコードから[[旧ファイル名]]で指す。",
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
