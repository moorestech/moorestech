#!/usr/bin/env node
// 意思決定台帳(.decisions/)の目次と運用ルールをセッション開始時に注入する
// Inject the decision ledger index and usage rules at session start

import { readdirSync } from "node:fs";
import { join } from "node:path";

// 失敗時は決して止めない（台帳が無いだけなら何も注入しない）
// Fail open: if the ledger is missing, inject nothing.
function bail() {
  process.exit(0);
}

const root = process.env.CLAUDE_PROJECT_DIR || process.cwd();

let entries;
try {
  entries = readdirSync(join(root, ".decisions"))
    .filter((f) => f.endsWith(".md"))
    .sort();
} catch {
  bail();
}

const lines = [
  "<decisions-ledger>",
  "過去のユーザー裁定の台帳。設計・実装の判断前にこの一覧を確認し、関連する裁定があれば本文を読むこと。裁定と矛盾する変更は黙って行わず、ユーザーに裁定の更新を諮る。",
  ...entries.map((f) => "- .decisions/" + f),
  "",
  "書き込みルール: ユーザーの裁定（AskUserQuestionへの回答・提案の却下や修正・方針転換）が発生したら、その場で .decisions/YYYY-MM-DD-<内容>.md を作成する。書式は「決定/棄却案/理由/リンク」の数行、推敲しない。生きた代替案の無い一本道の判断は書かない。覆った決定は旧ファイルを書き換えず、新レコードから[[旧ファイル名]]で指す。秘密情報は書かない（publicリポジトリ）。",
  "</decisions-ledger>",
];

console.log(lines.join("\n"));
