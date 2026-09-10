#!/usr/bin/env node
// 二段階「未回答質問だけ出す」ランナー。
//  generate: 設計文書を見せずに、依頼原文+既知事実だけから裁定事項の質問リストを Codex に作らせる
//  filter  : 同じ thread を resume し、裁定台帳(回答済み一覧)と設計文書を見せて未回答の質問だけ残させる
// 目的: Claude/Codex を並行で走らせると同じ質問が二重に来る。質問生成を文書提示より前に固定し、
// 後から台帳で消し込むことで、重複質問を防ぎつつ Codex 独自の質問だけを回収する。
import { spawn } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

const DEFAULT_MODEL = "gpt-6-astra";
const DEFAULT_EFFORT = "medium";

function usage() {
  console.error(`Usage:
  node codex-open-questions.mjs generate --brief <file> [--cwd <dir>] [--model M] [--effort E] [--out <json>]
  node codex-open-questions.mjs filter --session <thread> --decisions <md> --docs <file,file,...> [--cwd <dir>] [--model M] [--effort E] [--out <json>]

generate: --brief は「依頼原文 + 既知の事実」を書いたテキスト。設計文書は渡さない(隔離 worktree を --cwd に)。
filter  : --decisions は「# | 質問 | ユーザーの回答」の表(裁定台帳)。--docs は ADR/計画の絶対パス。
どちらも stdout に JSON、stderr に thread id を出す。`);
  process.exit(2);
}

function parseArgs(argv) {
  const opts = { mode: argv[0], model: DEFAULT_MODEL, effort: DEFAULT_EFFORT, cwd: process.cwd() };
  for (let i = 1; i < argv.length; i++) {
    const key = argv[i];
    const val = argv[i + 1];
    if (!key.startsWith("--") || val === undefined) usage();
    opts[key.slice(2)] = val;
    i++;
  }
  if (!["generate", "filter"].includes(opts.mode)) usage();
  return opts;
}

function generatePrompt(brief) {
  return `あなたは設計インタビュアーです。これから実装する機能について、実装に着手する前にユーザー(プロダクトオーナー)へ聞くべき「裁定事項」の一覧を作ってください。まだ設計文書はありません。このリポジトリ(読み取り専用)と規約ファイル(AGENTS.md / CLAUDE.md)を読んで、事実で解決できることは自分で調べ、ユーザーの意図次第で結果が大きく変わる分岐だけを質問にしてください。

${brief}

【出力形式】JSON のみ。コードフェンス無し。
{"questions":[{"id":"Q1","topic":"短い見出し","question":"ユーザーへの質問文","why":"何が変わるか 1 行","options":["選択肢A(推奨なら末尾に(推奨))","選択肢B"],"depends_on":["Q番号"]}]}
- 重要度の高い順、最大 15 件。事実で解決できる項目は含めない
- 各質問は他の質問と独立に答えられる粒度にする`;
}

function filterPrompt(decisions, docs) {
  return `続きです。あなたが先ほど作った質問のうち、別のエージェントが並行して行った設計インタビューで既に回答済みのものを除外し、残った質問だけをユーザーに提示したい。

【読むもの】
- 裁定台帳(ユーザーの回答済み一覧): ${decisions}
- その裁定を反映した設計文書: ${docs.join(", ")}

【判定規則】
- answered: 裁定台帳の回答で質問の分岐が確定している(選択肢のどれかが選ばれたと言える)。どの D 番号かを書く
- partial: 裁定はあるが質問の一部の分岐が未確定。未確定の部分だけを新しい質問文に書き直す
- open: 裁定台帳にも設計文書にも該当する裁定が無い。設計文書が agent 前提として勝手に決めている場合はその箇所(file:line)を示す
- 「文書に書いてある」だけでは answered にしない。ユーザーの裁定(台帳)に基づくものだけを answered にする
- 質問の言い換え・統合はしてよいが、新しい質問を追加しない(先ほどの質問の範囲内)

【出力形式】JSON のみ。コードフェンス無し。
{"answered":[{"id":"Q1","by":"D8","note":"1行"}],"remaining":[{"id":"Q6","status":"open|partial","topic":"…","question":"ユーザーへの質問文(partial は未確定部分だけ)","why":"1行","options":["…(推奨)","…"],"agent_assumption":"file:line と内容、無ければ null"}]}`;
}

// codex exec は stdin が TTY でないと「Reading additional input from stdin...」で待つため、必ず ignore にする
function runCodex(args, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn("codex", args, { cwd, stdio: ["ignore", "pipe", "pipe"] });
    let out = "";
    let err = "";
    child.stdout.on("data", (d) => { out += d.toString(); });
    child.stderr.on("data", (d) => { err += d.toString(); });
    child.on("error", reject);
    child.on("close", (code) => {
      if (code !== 0) reject(new Error(`codex exit ${code}\n${err.slice(-2000)}`));
      else resolve({ out, err });
    });
  });
}

function extractJson(text) {
  const start = text.indexOf("{");
  const end = text.lastIndexOf("}");
  if (start < 0 || end < 0) throw new Error(`JSON が見つかりません:\n${text.slice(0, 500)}`);
  return JSON.parse(text.slice(start, end + 1));
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const lastMsg = path.join(os.tmpdir(), `codex-oq-${process.pid}.txt`);
  const common = ["-m", opts.model, "-c", `model_reasoning_effort="${opts.effort}"`, "--skip-git-repo-check", "--json", "-o", lastMsg];
  let args;
  if (opts.mode === "generate") {
    if (!opts.brief) usage();
    const brief = fs.readFileSync(opts.brief, "utf8");
    args = ["exec", generatePrompt(brief), "-s", "read-only", ...common];
  } else {
    if (!opts.session || !opts.decisions || !opts.docs) usage();
    const docs = opts.docs.split(",").map((p) => path.resolve(p));
    // resume は -s 非対応なので -c で同じ sandbox を渡す(codex-audit.mjs と同じ流儀)
    args = ["exec", "resume", opts.session, filterPrompt(path.resolve(opts.decisions), docs),
      "-c", 'sandbox_mode="read-only"', ...common];
  }
  const { out } = await runCodex(args, opts.cwd);
  const thread = out.match(/"thread_id":"([^"]+)"/)?.[1];
  const result = extractJson(fs.readFileSync(lastMsg, "utf8"));
  fs.rmSync(lastMsg, { force: true });
  const json = JSON.stringify(result, null, 2);
  if (opts.out) fs.writeFileSync(opts.out, json);
  process.stdout.write(`${json}\n`);
  if (thread) console.error(`thread: ${thread}  (filter 時に --session ${thread} を指定)`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
});
