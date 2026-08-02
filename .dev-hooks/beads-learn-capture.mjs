#!/usr/bin/env node
// 応答末尾の「LEARN: <一行>」を検出し、claim中のissueのnoteへ自動保存する
// Detect trailing "LEARN: <one line>" entries and auto-save them as notes on the claimed issue.
//
// 登録: Claude Stop。claimが特定できない時はセッションログ用choreへ保存する
// Wired to Claude Stop; falls back to a per-session chore when no claim is identifiable.

import { execFileSync } from "node:child_process";
import { closeSync, existsSync, openSync, readFileSync, readSync, statSync, writeFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { tmpdir } from "node:os";
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
const sessionId = input.session_id || "unknown";
if (!existsSync(join(cwd, ".beads"))) bail();

// 最終assistantメッセージからLEARN行を抽出（無ければ即終了）
// Extract LEARN lines from the last assistant message; exit fast when absent.
const lastText = readLastAssistantText(input.transcript_path);
const learns = [...lastText.matchAll(/^(?:[-*\s>]*)(?:📌\s*)?(?:LEARN|学び)[:：]\s*(.+)$/gm)].map((m) => m[1].trim());
if (learns.length === 0) bail();

// セッション内の重複保存をハッシュで防ぐ
// Prevent duplicate saves within the session via content hashes.
const stateFile = join(tmpdir(), "beads-learn-" + hash(sessionId) + ".json");
const state = readState();
const fresh = learns.filter((text) => !state.hashes.includes(hash(text)));
if (fresh.length === 0) bail();

const targetId = resolveTargetIssue();
if (!targetId) bail();

for (const text of fresh) {
  // 外部境界: note保存の失敗は握りつぶす（次のStopで再試行される）
  // External boundary: swallow note failures; the next Stop retries them.
  try {
    execFileSync("bd", ["note", targetId, `[auto learn session=${sessionId.slice(0, 8)}] ${text}`], {
      cwd,
      timeout: 10000,
      stdio: "ignore",
    });
    state.hashes.push(hash(text));
  } catch {}
}
writeFileSync(stateFile, JSON.stringify(state));
bail();

// claim_sessionが一致するin_progress issueを探し、無ければセッションchoreを作る
// Find the in_progress issue claimed by this session; otherwise create the session chore.
function resolveTargetIssue() {
  const inProgress = bdJson(["list", "--status", "in_progress", "--json"]) ?? [];
  const claimed = inProgress.filter((issue) => issue?.metadata?.claim_session === sessionId);
  if (claimed.length === 1) return claimed[0].id;
  if (state.choreId) return state.choreId;
  // 外部境界: chore作成の失敗はfail open（今回のLEARNは見送る）
  // External boundary: fail open on chore creation failure (skip this LEARN batch).
  try {
    const out = execFileSync(
      "bd",
      ["create", `session-log ${sessionId.slice(0, 8)}`, "--type=chore", "--description=セッション中の学びの自動保存先", "--json"],
      { cwd, encoding: "utf8", timeout: 10000, stdio: ["ignore", "pipe", "ignore"] }
    );
    const id = JSON.parse(out)?.id ?? out.match(/\b(moorestech-[a-z0-9]+(?:\.\d+)*)\b/)?.[1];
    if (id) state.choreId = id;
    return id ?? null;
  } catch {
    return null;
  }
}

// transcript末尾2MBを読み、最後のassistantメッセージのtext結合を返す
// Read the last 2MB of the transcript and join the final assistant message's text blocks.
function readLastAssistantText(path) {
  if (typeof path !== "string") return "";
  // 外部境界: 他プロセス管理下のtranscript読み取り失敗はfail open
  // External boundary: fail open when the transcript (owned elsewhere) cannot be read.
  try {
    const size = statSync(path).size;
    const readLen = Math.min(size, 2 * 1024 * 1024);
    const buf = Buffer.alloc(readLen);
    const fd = openSync(path, "r");
    readSync(fd, buf, 0, readLen, size - readLen);
    closeSync(fd);
    const lines = buf.toString("utf8").split("\n");
    for (let i = lines.length - 1; i >= 0; i--) {
      const entry = safeParse(lines[i]);
      if (entry?.type !== "assistant" || entry?.isSidechain) continue;
      const content = entry?.message?.content;
      if (!Array.isArray(content)) continue;
      return content.filter((c) => c?.type === "text").map((c) => c.text).join("\n");
    }
    return "";
  } catch {
    return "";
  }
}

// 保存済みハッシュとchore IDのセッション状態を読む（無ければ初期値）
// Read per-session state (saved hashes, chore ID); default when missing.
function readState() {
  // 外部境界: 状態ファイルの破損はfail open（初期値から再開）
  // External boundary: fail open on corrupt state files (restart from defaults).
  try {
    return { hashes: [], choreId: null, ...JSON.parse(readFileSync(stateFile, "utf8")) };
  } catch {
    return { hashes: [], choreId: null };
  }
}

// 1行JSONのパース。壊れた行はnull
// Parse a single JSON line; corrupt lines yield null.
function safeParse(line) {
  // 外部境界: transcript行のJSONパース失敗はfail open
  // External boundary: fail open when a transcript line fails to parse.
  try {
    return JSON.parse(line);
  } catch {
    return null;
  }
}

// bdをJSON出力で叩く。失敗はnull
// Run bd with JSON output; failures yield null.
function bdJson(args) {
  // 外部境界: 外部プロセス起動とそのJSONパース失敗はfail open
  // External boundary: fail open on process launch or JSON parse failures.
  try {
    const out = execFileSync("bd", args, { cwd, encoding: "utf8", timeout: 10000, stdio: ["ignore", "pipe", "ignore"] });
    const parsed = JSON.parse(out);
    return Array.isArray(parsed) ? parsed : (parsed?.issues ?? []);
  } catch {
    return null;
  }
}

// 短いsha1ハッシュ
// Short sha1 hash.
function hash(text) {
  return createHash("sha1").update(text).digest("hex").slice(0, 12);
}
