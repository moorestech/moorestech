#!/usr/bin/env node
// .decisions/レコードの書式（ファイル名・必須キー）を検査し、逸脱を差し戻す
// Validate .decisions/ record format (filename & required keys); bounce violations
//
// 対応: Claude Code (Write/Edit の file_path) と Codex (apply_patch のパッチ本文)
// Handles both Claude Code (file_path) and Codex (apply_patch patch body).

import { readFileSync } from "node:fs";
import { basename } from "node:path";

// 対象外・読み取り失敗は決して止めない（このフックは書式の矯正専用）
// Fail open for non-targets and read failures; this hook only corrects format.
function bail() {
  process.exit(0);
}

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let input;
try {
  input = JSON.parse(readFileSync(0, "utf8"));
} catch {
  bail();
}

const targets = collectDecisionPaths(input);
if (targets.length === 0) bail();

// 各レコードを検査し、違反をまとめて差し戻す
// Validate each record and bounce all violations at once.
const violations = targets.flatMap((path) => {
  const errors = validate(path);
  return errors.length > 0 ? [`${basename(path)}: ${errors.join(" / ")}`] : [];
});

if (violations.length > 0) {
  console.error(
    ".decisions/レコードの書式違反: " +
      violations.join(" ; ") +
      "。書式は「決定/棄却案/理由/リンク」の数行（リンクは任意）。修正すること。"
  );
  process.exit(2);
}

bail();

// 編集ツールの入力形状の差を吸収し、.decisions/配下の対象パスだけを取り出す
// Absorb per-tool input shapes and extract only .decisions/ target paths.
function collectDecisionPaths(hookInput) {
  const toolInput = hookInput?.tool_input ?? {};
  const candidates = [];

  // Claude Code の Write/Edit 系は file_path をそのまま持つ
  // Claude Code's Write/Edit family carries file_path directly.
  if (typeof toolInput.file_path === "string") candidates.push(toolInput.file_path);

  // Codex の apply_patch はパッチ本文の Add/Update File 行にパスが載る
  // Codex's apply_patch puts paths on the Add/Update File lines of the patch body.
  const patch = [toolInput.command, toolInput.input, toolInput.patch]
    .filter((v) => typeof v === "string")
    .join("\n");
  for (const m of patch.matchAll(/^\*\*\* (?:Add|Update) File: (.+)$/gm)) {
    candidates.push(m[1].trim());
  }

  return [...new Set(candidates)].filter((p) =>
    /[\\/]\.decisions[\\/][^\\/]+\.md$/.test(p)
  );
}

// ファイル名形式と必須キーの有無を検査する
// Check the filename format and the presence of required keys.
function validate(path) {
  const errors = [];

  if (!/^\d{4}-\d{2}-\d{2}-.+\.md$/.test(basename(path))) {
    errors.push("ファイル名を YYYY-MM-DD-<内容>.md 形式にする");
  }

  // 編集後のファイルが読めない場合は書式判定を諦める（fail open）
  // Give up format checks when the edited file cannot be read (fail open).
  let body = "";
  try {
    body = readFileSync(path, "utf8");
  } catch {
    return errors;
  }

  for (const key of ["決定", "棄却案", "理由"]) {
    if (!new RegExp("^" + key, "m").test(body)) errors.push(`「${key}」の行が無い`);
  }

  return errors;
}
