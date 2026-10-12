#!/usr/bin/env node
// =====================================================================
// ⚠ scripts/ 配下を変更したら必ず回帰テストを実行すること:
//     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
// Workflow ツールが無いホスト（Codex）で、build_workflow_args.py が書いた $RUNDIR/review_workflow.js をそのまま実行するランタイム。
// agent() を codex exec 1プロセスに置き換えるだけで、系統の選択・発火数・再起動・統合・適用の流れはスクリプト本文が決める。
// よって Claude の Workflow 実行と同じ系統が同じ数だけ発火する（tests/test_codex_workflow_runner.py が模擬実行と突き合わせる）。
// 使い方: node run.mjs <$RUNDIR/review_workflow.js> [--codex-bin PATH] [--max-parallel N] [--timeout-minutes N] [--sandbox MODE]
// 成果物: <run_dir>/codex-runner/（各 agent の prompt/schema/out/final・runner.log・fires.json・result.json）。標準出力は result.json と同じ JSON。
//
// ⚠ Run the regression suite after ANY change under scripts/.
// Runtime for hosts without the Workflow tool (Codex): executes the assembled $RUNDIR/review_workflow.js unchanged,
// replacing agent() with one codex exec process, so the same systems fire the same number of times as under Claude's Workflow.
// =====================================================================
import { readFileSync, writeFileSync, mkdirSync, appendFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { cpus } from 'node:os'
import { fileURLToPath } from 'node:url'
import { createCodexAgent } from './codex_agent.mjs'
import { launchCodexJobs } from './codex_jobs.mjs'

const HERE = dirname(fileURLToPath(import.meta.url))
const AsyncFunction = Object.getPrototypeOf(async () => {}).constructor

const options = parseOptions(process.argv.slice(2))
const scriptPath = resolve(options.script)
const source = readFileSync(scriptPath, 'utf8')
const args = embeddedArgs(scriptPath)
const logDir = join(args.runDir, 'codex-runner')
mkdirSync(logDir, { recursive: true })

// 実行文脈を組み立てる（同時数は Workflow ランタイムと同じ min(16, CPU-2)）
// Build the run context (concurrency mirrors the Workflow runtime's min(16, CPU-2))
const ctx = {
  codexBin: options.codexBin,
  sandbox: options.sandbox,
  timeoutMinutes: options.timeoutMinutes,
  repoRoot: args.repoRoot,
  modelMap: JSON.parse(readFileSync(join(HERE, 'codex_model_map.json'), 'utf8')),
  logDir,
  fires: [],
  semaphore: createSemaphore(options.maxParallel),
  log: (message) => {
    const line = `[${new Date().toISOString()}] ${message}`
    appendFileSync(join(logDir, 'runner.log'), `${line}\n`)
    process.stderr.write(`${line}\n`)
  },
}
ctx.log(`runner 開始: ${scriptPath} / systems ${args.systems.length} / 同時数 ${options.maxParallel} / sandbox ${options.sandbox}`)

// Codex 外部監査を先に切り離して起動し、Workflow 本文を関数本体として評価する（ランタイムと同じく export を剥がす）
// Launch the Codex audits detached first, then evaluate the workflow body as a function body (export stripped like the runtime)
launchCodexJobs(args.codexJobs, ctx)
const body = new AsyncFunction('agent', 'parallel', 'pipeline', 'phase', 'log', 'args', source.replace('export const meta', 'const meta'))
const runtime = {
  agent: createCodexAgent(ctx),
  parallel: async (thunks) => Promise.all(thunks.map((thunk) => thunk().catch((e) => { ctx.log(`parallel 枠の例外: ${e && e.message}`); return null }))),
  pipeline: async (items, ...stages) => Promise.all(items.map(async (item) => { let v = item; for (const stage of stages) v = await stage(v); return v })),
  phase: (title) => ctx.log(`phase: ${title}`),
  log: (message) => ctx.log(message),
}

let exitCode = 0
let payload
// Workflow 本文の throw は「親が再派遣すべき欠員」の申告なので、握り潰さず result.json と終了コード 3 で返す
// A throw from the workflow body is a gap report for the parent; surface it in result.json and exit code 3
const outcome = await body(runtime.agent, runtime.parallel, runtime.pipeline, runtime.phase, runtime.log, undefined)
  .then((result) => ({ result }), (error) => ({ error: String(error && error.stack || error) }))
if (outcome.error) {
  exitCode = 3
  ctx.log(`Workflow 本文が中断: ${outcome.error}`)
}
payload = { script: scriptPath, plannedSystems: args.systems.length, fireCount: ctx.fires.length, failedFires: ctx.fires.filter((f) => !f.ok).length, ...outcome }
writeFileSync(join(logDir, 'fires.json'), JSON.stringify(ctx.fires, null, 2))
writeFileSync(join(logDir, 'result.json'), JSON.stringify(payload, null, 2))
process.stdout.write(`${JSON.stringify(payload)}\n`)
process.exit(exitCode)

function parseOptions(argv) {
  const opts = { script: null, codexBin: process.env.CODEX_BIN || 'codex', maxParallel: Math.min(16, Math.max(1, cpus().length - 2)), timeoutMinutes: 60, sandbox: 'danger-full-access' }
  for (let i = 0; i < argv.length; i++) {
    const key = argv[i]
    if (key === '--codex-bin') opts.codexBin = argv[++i]
    else if (key === '--max-parallel') opts.maxParallel = Number(argv[++i])
    else if (key === '--timeout-minutes') opts.timeoutMinutes = Number(argv[++i])
    else if (key === '--sandbox') opts.sandbox = argv[++i]
    else if (!opts.script) opts.script = key
    else throw new Error(`未知の引数: ${key}`)
  }
  if (!opts.script) throw new Error('usage: node run.mjs <$RUNDIR/review_workflow.js> [--codex-bin PATH] [--max-parallel N] [--timeout-minutes N] [--sandbox MODE]')
  return opts
}

// 結合済みスクリプトと同じ dir の workflow-args.json が埋め込み args の正本（build_workflow_args.py が同時に書く）
// workflow-args.json next to the assembled script is the source of the embedded args (written together by build_workflow_args.py)
function embeddedArgs(path) {
  const parsed = JSON.parse(readFileSync(join(dirname(path), 'workflow-args.json'), 'utf8'))
  if (!parsed.runDir || !Array.isArray(parsed.systems)) throw new Error(`workflow-args.json に runDir/systems が無い: ${path}`)
  return parsed
}

function createSemaphore(limit) {
  let active = 0
  const waiting = []
  return {
    acquire: () => new Promise((ok) => { if (active < limit) { active++; ok() } else waiting.push(ok) }),
    release: () => { const next = waiting.shift(); if (next) next(); else active-- },
  }
}
