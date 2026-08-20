// =====================================================================
// moores-code-review Step 3.5〜6.5 の Workflow スクリプト（正本は references/orchestrator-steps.md）。
// sonnet オーケストレータ subagent が「待つだけで1ターン＝全コンテキスト再送」を590〜625回繰り返し
// 1本 $194〜240 を空転で燃やしていた（2026-08-20 再計測）。待機を JS の await に置き換え、
// 12体/波・model明示・起動失敗の再起動・欠員の申告を手順書の散文でなくコードで強制する。
// 入力 args は scripts/build_workflow_args.py が生成した workflow-args.json の中身（選択は済んでいる）。
// 変更時は tests/test_skill_wiring.py（Workflow配線検査）を必ず通すこと。
//
// Workflow script for Steps 3.5–6.5 of moores-code-review (prose source of truth:
// references/orchestrator-steps.md). Replaces the sonnet orchestrator subagent whose idle
// polling turns cost $194–240 per review. All selection is done by build_workflow_args.py;
// this script only sequences launches, retries, integration and application.
// =====================================================================
export const meta = {
  name: 'moores-code-review',
  description: 'moores-code-review の系統並列発火→統合→自動適用→post-check を決定論的に実行する',
  phases: [
    { title: 'Review', detail: 'lens / reviewer / Fable / investigator / verifier を並列発火（ファイルハンドオフ）' },
    { title: 'Integrate', detail: 'opus integrator が agents/・Codex結論・checks.json を統合' },
    { title: 'Apply', detail: '確定修正の自動適用と uloop compile（report-only では省略）' },
    { title: 'PostCheck', detail: '最終diffで post-check を発火し結果を適用' },
  ],
}

const A = args
if (!A || !A.runDir || !A.patchPath || !A.userPromptPath || !A.repoRoot || !A.skillRoot || !Array.isArray(A.systems)) {
  throw new Error('args 不足: scripts/build_workflow_args.py が出力した workflow-args.json の中身を args に渡すこと')
}

// 各系統の返答は件数だけ（本文は agents/<name>.md へ）/ Each system returns counts only; body goes to file
const REPORT_SCHEMA = {
  type: 'object',
  properties: {
    critical_count: { type: 'integer' },
    design_judgement: { type: 'boolean' },
    summary: { type: 'string' },
    report_written: { type: 'boolean' },
  },
  required: ['critical_count', 'design_judgement', 'summary', 'report_written'],
}
const INTEGRATOR_SCHEMA = {
  type: 'object',
  properties: {
    critical: { type: 'integer' }, warning: { type: 'integer' }, info: { type: 'integer' },
    suppressed: { type: 'integer' }, design_items: { type: 'integer' },
    missing_systems: { type: 'array', items: { type: 'string' } },
    integrated_written: { type: 'boolean' },
  },
  required: ['critical', 'warning', 'info', 'suppressed', 'design_items', 'missing_systems', 'integrated_written'],
}
const APPLY_SCHEMA = {
  type: 'object',
  properties: {
    applied: { type: 'integer' },
    compile: { type: 'string', enum: ['ok', 'error', 'skipped'] },
    tests: { type: 'string' },
    design_items: { type: 'integer' },
    post_checks: {
      type: 'array',
      items: { type: 'object', properties: { path: { type: 'string' }, model: { type: 'string' } }, required: ['path', 'model'] },
    },
    notes: { type: 'string' },
  },
  required: ['applied', 'compile', 'design_items', 'post_checks'],
}
const POSTFIX_SCHEMA = {
  type: 'object',
  properties: { applied: { type: 'integer' }, escalated: { type: 'integer' }, compile: { type: 'string', enum: ['ok', 'error', 'skipped'] }, notes: { type: 'string' } },
  required: ['applied', 'escalated', 'compile'],
}

const footer = [
  '',
  `Repo root（コードを読む作業ツリー。Bash/Read はこの配下で行い、他のworktreeを読まない）: ${A.repoRoot}`,
  `Skill root（観点・ルール・scripts はこの配下の絶対パスで参照する）: ${A.skillRoot}`,
  '返答は構造化出力（Critical件数・設計判断あり/なし・一行要約・report_written）だけ。指摘本文は返答に書かず、必ず `Write full report to` のファイルへ全文を書く。',
]

function reviewPrompt(s) {
  const head = [`Read this : ${s.path}`]
  if (s.kind === 'investigator') head.push(`Chunk files : ${s.chunkFiles}`, `Chunks TSV : ${A.chunksTsv}`)
  if (s.kind === 'verifier' || s.kind === 'postcheck') head.push(`Candidates : ${s.candidatesPath || A.checksPath}`)
  const lines = [
    `Patch path : ${s.patchOverride || A.patchPath}`,
    `User prompt : ${A.userPromptPath}`,
    `Output contract : ${A.contractPath}`,
  ]
  return [...head, ...lines, `Write full report to : ${A.runDir}/agents/${s.name}.md`, ...footer].join('\n')
}

// 起動失敗・report未書込は1回だけ再起動。fable は quota 切れに備え opus で再起動（手順書の規則）
// Retry once on failure / unwritten report; fable retries on opus (quota fallback rule)
async function runSystem(s, phaseName) {
  let model = s.model
  let result = null
  for (let attempt = 1; attempt <= 2; attempt++) {
    result = await agent(reviewPrompt(s), { label: `${s.kind}:${s.name}`, phase: phaseName, model, schema: REPORT_SCHEMA })
    if (result && result.report_written) return { name: s.name, kind: s.kind, model, attempts: attempt, ok: true, result }
    log(`${s.name}: ${result ? 'report 未書込' : '応答なし（起動失敗/上限）'} → 再起動 ${attempt}/1`)
    if (model === 'fable') model = 'opus'
  }
  return { name: s.name, kind: s.kind, model, attempts: 2, ok: false, result }
}

// ---- Review: 全系統を並列発火（同時数はランタイムがキューイング）----
log(`Review: ${A.systems.length} 系統を発火（lens/reviewer/Fable/investigator/verifier）`)
const reviewed = (await parallel(A.systems.map((s) => () => runSystem(s, 'Review')))).filter(Boolean)
const missing = reviewed.filter((r) => !r.ok).map((r) => r.name)
const fallbacks = reviewed.filter((r) => r.ok && r.model !== A.systems.find((s) => s.name === r.name).model).map((r) => `${r.name}→${r.model}`)
log(`Review 完了: 回収 ${reviewed.length - missing.length}/${A.systems.length}` + (missing.length ? ` 欠員 ${missing.join(', ')}` : '') + (fallbacks.length ? ` fallback ${fallbacks.join(', ')}` : ''))

// ---- Integrate: opus integrator 1体 ----
const integratorPrompt = [
  `Read this : ${A.integratorPath}`,
  `Run dir : ${A.runDir}`,
  `Patch path : ${A.patchPath}`,
  `User prompt : ${A.userPromptPath}`,
  `Write integrated report to : ${A.runDir}/integrated.md`,
  `Repo root : ${A.repoRoot}`,
  `Skill root : ${A.skillRoot}（integration-rules.md・codex_recover.py はこの配下の絶対パスで参照する）`,
  `起動済み系統 : ${A.systems.map((s) => s.name).join(', ')}`,
  `起動失敗で欠員の系統 : ${missing.length ? missing.join(', ') : 'なし'}（2回起動しても回収できなかった。系統別回収状況に欠員として記録する）`,
  'Codex 3本の結論は `.final.md`。不在なら codex_recover.py を先に走らせ、終了コード（0=結論あり / 3=未完走 / 4=セッション無し / 5=認証失効）を系統別回収状況に併記する。',
  '返答は件数サマリ（Critical/Warning/Info/suppressed/設計判断件数）と欠員系統名のみ。',
].join('\n')
const integrated = await agent(integratorPrompt, { label: 'integrator', phase: 'Integrate', model: 'opus', schema: INTEGRATOR_SCHEMA })
if (!integrated || !integrated.integrated_written) {
  throw new Error('integrator が integrated.md を書けなかった（応答なし or integrated_written=false）。$RUNDIR/agents は残っているので親が integrator だけ再派遣する')
}
log(`Integrate 完了: C${integrated.critical} W${integrated.warning} I${integrated.info} S${integrated.suppressed} 設計判断${integrated.design_items}`)

// ---- Apply: 確定修正の自動適用＋compile＋最終diff＋post-check選択（report-only では省略）----
let apply = null
let postChecks = A.postChecks || []
if (!A.reportOnly) {
  const applyPrompt = [
    `Read this : ${A.orchestratorStepsPath} — Step 6 と Step 6.5 の 1〜3 だけを実行する（Step 2〜5 は完了済み。post-check agent の起動は親が行うので自分では起動しない）。`,
    `Run dir : ${A.runDir}`,
    `Integrated report : ${A.runDir}/integrated.md`,
    `Patch path : ${A.patchPath}`,
    `User prompt : ${A.userPromptPath}`,
    `Repo root : ${A.repoRoot}（修正はこの作業ツリーだけに加える）`,
    `Skill root : ${A.skillRoot}（integration-rules.md §3〜§5・scripts はこの配下の絶対パス）`,
    `Base ref : ${A.baseRef || '(未指定)'} — final.diff は「git diff <Base ref> -- . ':(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs'」で作る。未指定なら patch.diff に「git diff HEAD」（Step 6 の未コミット変更）を連結する。`,
    '手順: (1) integrated.md の採用Critical のうち適用区分が自動適用可のものだけ適用する。設計判断は適用せず design.md（症状→原因→推奨と選択肢。コードを開かずに選べる形。0件なら「なし」）へ書く。',
    '(2) .cs を変えたら `uloop compile --project-path <Repo root>/moorestech_client` でエラー0を確認する（Editor不在で実行不能なら compile=skipped と返す）。',
    `(3) final.diff を書き、\`python3 ${A.deterministicChecksScript} <final.diff> --repo-root <Repo root>\` を ${A.runDir}/checks-final.json へ書く（--context は渡さない）。自分の修正が新たに生んだ confirmed/比較演算子違反はその場で直す。`,
    `(4) \`python3 ${A.selectPostChecksScript} ${A.runDir}/final.diff ${A.runDir}/checks-final.json\` を実行し、出力TSV（<post-check絶対パス>\\t<モデル>）を post_checks として返す（空なら []）。`,
    'Read規律: Edit対象の該当範囲だけを offset/limit で読む。ファイル全文Readしない。返答は構造化出力のみ。',
  ].join('\n')
  apply = await agent(applyPrompt, { label: 'apply', phase: 'Apply', model: 'sonnet', schema: APPLY_SCHEMA })
  if (!apply) throw new Error('apply agent が応答しなかった。integrated.md は残っているので親が Step 6 だけ再派遣する')
  postChecks = apply.post_checks.map((p) => ({ kind: 'postcheck', name: `postcheck-${p.path.split('/').pop().replace(/\.md$/, '')}`, path: p.path, model: p.model }))
  log(`Apply 完了: 適用 ${apply.applied} / compile ${apply.compile} / 設計判断 ${apply.design_items} / post-check ${postChecks.length}`)
}

// ---- PostCheck: 選択された post-check だけ発火（空なら0トークン）----
let postResults = []
let postfix = null
if (postChecks.length) {
  // post-check は「最終diff＋最終checks」を見る（report-only では patch と Step 2 の決定論JSON）
  // Post-checks read the final diff and final checks (patch + Step 2 deterministic JSON in report-only)
  const diffPath = A.reportOnly ? A.patchPath : `${A.runDir}/final.diff`
  const candidatesPath = A.reportOnly ? `${A.runDir}/detchecks.json` : `${A.runDir}/checks-final.json`
  postResults = (await parallel(postChecks.map((p) => () => runSystem(
    { ...p, kind: 'postcheck', patchOverride: diffPath, candidatesPath }, 'PostCheck',
  )))).filter(Boolean)
  if (!A.reportOnly) {
    const postfixPrompt = [
      `Read this : ${A.orchestratorStepsPath} — Step 6.5 の 4〜6 だけを実行する。`,
      `Run dir : ${A.runDir}`,
      `Post-check reports : ${postResults.map((r) => `${A.runDir}/agents/${r.name}.md`).join(', ')}`,
      `Repo root : ${A.repoRoot}`,
      `Skill root : ${A.skillRoot}`,
      '手順: rationale-guard の Critical は自動復元せず design.md へ追記（復元タグ案付き）。convention-guard は `機械的` を自動適用し `要判断` はガードの裁定で完結させる（webui は要判断も短縮適用）。同一行で衝突したら根拠保全を優先。.cs を変えたら uloop compile を再実行する。',
      '返答は構造化出力のみ（適用数・escalate数・compile結果）。',
    ].join('\n')
    postfix = await agent(postfixPrompt, { label: 'postfix', phase: 'PostCheck', model: 'sonnet', schema: POSTFIX_SCHEMA })
  }
} else {
  log('PostCheck: 発火条件未達でスキップ（0トークン）')
}

return {
  systems: {
    launched: A.systems.length,
    recovered: reviewed.length - missing.length,
    missing,
    fallbacks,
    perSystem: reviewed.map((r) => ({ name: r.name, ok: r.ok, model: r.model, attempts: r.attempts, critical: r.result ? r.result.critical_count : null, design: r.result ? r.result.design_judgement : null })),
  },
  integrated,
  apply,
  postChecks: postResults.map((r) => ({ name: r.name, ok: r.ok, critical: r.result ? r.result.critical_count : null })),
  postfix,
  paths: { integrated: `${A.runDir}/integrated.md`, design: `${A.runDir}/design.md`, finalDiff: A.reportOnly ? A.patchPath : `${A.runDir}/final.diff` },
}
