#!/usr/bin/env node
// .decisions/レコードの書式（ファイル名・必須キー）を検査し、逸脱を差し戻す
// Validate .decisions/ record format (filename & required keys); bounce violations

import { readFileSync } from "node:fs";
import { basename } from "node:path";

// 対象外・読み取り失敗は決して止めない（このフックは書式の矯正専用）
// Fail open for non-targets and read failures; this hook only corrects format.
function bail() {
  process.exit(0);
}

// 外部境界: フック標準入力のJSONパース失敗はfail open
// External boundary: fail open when parsing the hook stdin JSON fails.
let filePath = "";
try {
  const input = JSON.parse(readFileSync(0, "utf8"));
  filePath = input?.tool_input?.file_path ?? "";
} catch {
  bail();
}

if (!/[\\/]\.decisions[\\/][^\\/]+\.md$/.test(filePath)) bail();

const errors = [];

if (!/^\d{4}-\d{2}-\d{2}-.+\.md$/.test(basename(filePath))) {
  errors.push("ファイル名を YYYY-MM-DD-<内容>.md 形式にする");
}

let body = "";
try {
  body = readFileSync(filePath, "utf8");
} catch {
  bail();
}

for (const key of ["決定", "棄却案", "理由"]) {
  if (!new RegExp("^" + key, "m").test(body)) errors.push(`「${key}」の行が無い`);
}

if (errors.length > 0) {
  console.error(
    ".decisions/レコードの書式違反: " +
      errors.join(" / ") +
      "。書式は「決定/棄却案/理由/リンク」の数行（リンクは任意）。修正すること。"
  );
  process.exit(2);
}

bail();
