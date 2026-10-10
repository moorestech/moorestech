// =====================================================================
// ⚠ scripts/ 配下を変更したら必ず回帰テストを実行すること:
//     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
// Workflow の agent() 1回を codex exec 1プロセスとして実行する。構造化出力は --output-schema、結論は -o の JSON。
// 失敗（起動失敗・非0終了・時間切れ・JSON 不正）は Workflow ランタイムと同じく null を返し、再起動と欠員の数え上げはスクリプト側に任せる。
//
// ⚠ Run the regression suite after ANY change under scripts/.
// Runs one Workflow agent() call as one codex exec process (--output-schema for shape, -o for the JSON answer).
// Failures return null like the Workflow runtime, leaving retries and gap accounting to the script.
// =====================================================================
import { spawn } from 'node:child_process'
import { existsSync, readFileSync, writeFileSync, openSync, closeSync } from 'node:fs'
import { join } from 'node:path'
import { toStrictSchema } from './strict_schema.mjs'

export function createCodexAgent(ctx) {
  let seq = 0
  return async function agent(prompt, opts) {
    // model 未指定・未知の model は割り当て漏れなので起動前に止める
    // A missing or unknown model is a wiring bug, so stop before launching
    if (!opts || !opts.model) throw new Error(`model 未指定の agent(): ${String(prompt).slice(0, 60)}`)
    const target = ctx.modelMap[opts.model]
    if (!target) throw new Error(`codex_model_map.json に無い model: ${opts.model}（label=${opts.label}）`)

    const index = ++seq
    const stem = join(ctx.logDir, `${String(index).padStart(3, '0')}-${safeName(opts.label || 'agent')}`)
    const fire = { index, label: opts.label, phase: opts.phase || null, model: opts.model, codexModel: target.model, effort: target.effort, ok: false, reason: null, seconds: null }
    ctx.fires.push(fire)

    await ctx.semaphore.acquire()
    const started = Date.now()
    try {
      const answer = await runOnce(prompt, opts, target, stem)
      fire.ok = answer.value !== null
      fire.reason = answer.reason
      if (!fire.ok) ctx.log(`agent ${opts.label}: 失敗（${answer.reason}）→ null を返す`)
      return answer.value
    } finally {
      fire.seconds = Math.round((Date.now() - started) / 1000)
      ctx.semaphore.release()
    }
  }

  function runOnce(prompt, opts, target, stem) {
    writeFileSync(`${stem}.prompt.md`, prompt)
    const finalPath = opts.schema ? `${stem}.final.json` : `${stem}.final.md`
    const argv = ['exec', '--skip-git-repo-check', '-s', ctx.sandbox, '-C', ctx.repoRoot, '-c', `model_reasoning_effort=${target.effort}`]
    if (target.model) argv.push('-m', target.model)
    if (opts.schema) {
      writeFileSync(`${stem}.schema.json`, JSON.stringify(toStrictSchema(opts.schema), null, 2))
      argv.push('--output-schema', `${stem}.schema.json`)
    }
    argv.push('-o', finalPath, '-')
    return new Promise((resolve) => {
      const outFd = openSync(`${stem}.out.md`, 'w')
      const child = spawn(ctx.codexBin, argv, { stdio: ['pipe', outFd, outFd] })
      const timer = setTimeout(() => child.kill('SIGTERM'), ctx.timeoutMinutes * 60 * 1000)
      child.on('error', (e) => finish(`起動失敗: ${e.message}`))
      child.on('close', (code, signal) => finish(signal ? `シグナル終了 ${signal}（時間切れ ${ctx.timeoutMinutes}分の可能性）` : code === 0 ? null : `終了コード ${code}`))
      child.stdin.end(prompt)

      let done = false
      function finish(failure) {
        if (done) return
        done = true
        clearTimeout(timer)
        closeSync(outFd)
        if (failure) return resolve({ value: null, reason: failure })
        resolve(readAnswer(finalPath, Boolean(opts.schema)))
      }
    })
  }
}

// -o の中身を返す。スキーマ付きは JSON として読み、壊れていれば null（欠員扱い）にする
// Return the -o content; schema answers must parse as JSON or count as a failure
function readAnswer(finalPath, isJson) {
  if (!existsSync(finalPath)) return { value: null, reason: '-o の出力が無い' }
  const text = readFileSync(finalPath, 'utf8').trim()
  if (!text) return { value: null, reason: '-o の出力が空' }
  if (!isJson) return { value: text, reason: null }
  // 外部プロセス（codex）の出力パース境界。不正な JSON は欠員として null を返し理由を残す
  // Parsing boundary for an external process's output; invalid JSON becomes a logged gap
  try {
    return { value: JSON.parse(text), reason: null }
  } catch (e) {
    return { value: null, reason: `JSON 不正: ${e.message}` }
  }
}

function safeName(label) {
  return label.replace(/[^A-Za-z0-9._-]+/g, '_').slice(0, 80)
}
