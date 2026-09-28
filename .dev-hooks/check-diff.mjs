#!/usr/bin/env node
// 差分テキストをルールに照合し、一致した観点リマインドをエージェントへ流す共通フック
// Universal hook: match the edited diff against rules and feed reminders back to the agent
//
// 対応: Claude Code / Codex、mac / windows（node さえ通れば動く）
// Works on Claude Code & Codex, macOS & Windows (only needs `node` on PATH).
//
// 拡張方法: 同ディレクトリの rules.json にルールを足すだけ。スクリプトの変更は不要。
// To extend: just add rules to rules.json next to this file. No script change needed.

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const HERE = dirname(fileURLToPath(import.meta.url));

// 失敗時は決して止めない（フックは最終防衛線ではない・安全側に倒す）
// Never block on failure; a reminder hook must fail open.
function bail() {
  process.exit(0);
}

// 編集系ツールのみ対象。Read 等での誤爆を防ぐ
// Only edit-like tools; avoids false positives on Read etc.
const EDIT_TOOLS = new Set([
  "Edit", "Write", "MultiEdit", "NotebookEdit",
  "apply_patch", "str_replace_editor", "str_replace_based_edit_tool",
]);

function loadRules() {
  // 環境変数で差し替え可能、無ければ同梱の rules.json
  // Overridable via env; defaults to the bundled rules.json
  const path = process.env.DEV_HOOKS_RULES || join(HERE, "rules.json");
  const parsed = JSON.parse(readFileSync(path, "utf8"));
  return Array.isArray(parsed) ? parsed : parsed.rules || [];
}

// 編集ツールの入力形状の差を吸収し、編集対象のファイルパスを取り出す
// Absorb per-tool input shapes and extract the edited file paths.
function collectEditedPaths(toolInput) {
  const paths = [];

  // Claude Code の編集系は file_path（ノートブックは notebook_path）を持つ
  // Claude Code's edit tools carry file_path (notebook_path for notebooks).
  for (const key of ["file_path", "notebook_path", "path"]) {
    if (typeof toolInput[key] === "string") paths.push(toolInput[key]);
  }

  // Codex の apply_patch はパッチ本文の Add/Update File 行にパスが載る
  // Codex's apply_patch puts paths on the Add/Update File lines of the patch body.
  const patch = [toolInput.command, toolInput.input, toolInput.patch]
    .filter((v) => typeof v === "string")
    .join("\n");
  for (const m of patch.matchAll(/^\*\*\* (?:Add|Update) File: (.+)$/gm)) {
    paths.push(m[1].trim());
  }

  return [...new Set(paths)];
}

// filePattern 指定のルールは、編集対象のどれかが一致した時だけ照合へ進める
// A rule with filePattern proceeds only when some edited path matches it.
function matchesFilePattern(rule, editedPaths) {
  if (!rule.filePattern) return true;
  let re;
  try {
    re = new RegExp(rule.filePattern, rule.filePatternFlags || "");
  } catch {
    return false;
  }
  return editedPaths.some((p) => re.test(p));
}

function readStdin() {
  try {
    return readFileSync(0, "utf8");
  } catch {
    return "";
  }
}

function main() {
  const raw = readStdin();
  if (!raw.trim()) bail();

  let payload;
  try {
    payload = JSON.parse(raw);
  } catch {
    bail();
  }

  // Claude / Codex どちらのキー名でも拾う
  // Accept either Claude's or Codex's key naming.
  const toolName = payload.tool_name || payload.toolName || "";
  const toolInput = payload.tool_input || payload.toolInput || {};
  const eventName = payload.hook_event_name || payload.hookEventName || "PostToolUse";

  if (toolName && !EDIT_TOOLS.has(toolName)) bail();

  // 差分テキスト＝tool_input 全体（file_path / content / new_string / patch を網羅）
  // The "diff" haystack is the whole tool_input (covers file_path, content, new_string, patch).
  const haystack = JSON.stringify(toolInput);
  if (!haystack) bail();

  let rules;
  try {
    rules = loadRules();
  } catch {
    bail();
  }

  // 各ルールを照合し、ツール限定・ファイル種別限定があれば尊重する
  // Match each rule, respecting optional per-rule tool and file-path filters.
  const editedPaths = collectEditedPaths(toolInput);
  const messages = [];
  for (const rule of rules) {
    if (!rule || !rule.pattern || !rule.message) continue;
    if (Array.isArray(rule.tools) && rule.tools.length && toolName && !rule.tools.includes(toolName)) continue;
    if (!matchesFilePattern(rule, editedPaths)) continue;

    let re;
    try {
      re = new RegExp(rule.pattern, rule.flags || "");
    } catch {
      continue;
    }
    if (re.test(haystack)) messages.push(rule.message);
  }

  if (messages.length === 0) bail();

  // additionalContext で非ブロッキングに観点を注入（Claude / Codex 共通フォーマット）
  // Inject the perspective non-blockingly via additionalContext (shared Claude/Codex format).
  const additionalContext = messages.join("\n\n");
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: eventName,
      additionalContext,
    },
  }));
  process.exit(0);
}

main();
