// =====================================================================
// ⚠ scripts/ 配下を変更したら必ず回帰テストを実行すること:
//     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
// Codex 外部監査（args.codexJobs）を Workflow 本体より先に切り離して起動する。Claude 本体は Bash の run_in_background で投げるが、
// Codex ホストにはその手段が無いので runner が代行する。本体がプロンプトを書いただけで未起動のジョブ（.final.md も .out.md も無い）だけ起動する。
//
// ⚠ Run the regression suite after ANY change under scripts/.
// Launches the external Codex audits (args.codexJobs) detached before the workflow body, which Claude does via run_in_background.
// Only jobs the parent wrote a prompt for but never started (no .final.md and no .out.md) are launched.
// =====================================================================
import { spawn } from 'node:child_process'
import { existsSync, openSync, closeSync } from 'node:fs'

export function launchCodexJobs(jobs, ctx) {
  const launched = []
  for (const job of jobs || []) {
    // 既に本体が起動済み（成果物あり）なら二重起動しない
    // Never double-launch a job the parent already started
    if (existsSync(job.final) || existsSync(job.out)) {
      ctx.log(`codex job ${job.name}: 起動済みのため runner は起動しない`)
      continue
    }
    const inFd = openSync(job.prompt, 'r')
    const outFd = openSync(job.out, 'w')
    const child = spawn(ctx.codexBin, ['exec', '--sandbox', 'read-only', '--skip-git-repo-check', '-C', ctx.repoRoot, '-o', job.final, '-'], {
      stdio: [inFd, outFd, outFd], detached: true,
    })
    child.on('error', (e) => ctx.log(`codex job ${job.name}: 起動失敗 ${e.message}（integrator が codex_recover.py で欠員を確定する）`))
    child.unref()
    closeSync(inFd)
    closeSync(outFd)
    launched.push(job.name)
  }
  ctx.log(`codex job 起動: ${launched.length ? launched.join(', ') : 'なし'}`)
  return launched
}
