// =====================================================================
// moores-code-review Step 3.5〜6.5 の Workflow スクリプト（仕様の正本は references/orchestrator-steps.md）。
// sonnet オーケストレータ subagent が「待つだけで1ターン＝全コンテキスト再送」を590〜625回繰り返し
// 1本 $194〜240 を空転で燃やしていた（2026-08-20 再計測）。待機を JS の await に置き換え、
// model明示・起動失敗の再起動・欠員の申告・Codex完了待ちを手順書の散文でなくコードで強制する。
// 同時実行数はランタイムが min(16, CPU-2) でキューイングする（手順書の「1メッセージ12体」は
// Agent ツール直起動時の規律。CPU 18コア以上のホストへ移すなら 12体/波の分割をここに足す）。
// 入力 args は scripts/build_workflow_args.py が生成した workflow-args.json の中身（選択は済んでいる）。
// 変更時は tests/test_skill_wiring.py（Workflow配線検査）を必ず通すこと。
//
// Workflow script for Steps 3.5–6.5 of moores-code-review (prose source of truth:
// references/orchestrator-steps.md). Replaces the sonnet orchestrator subagent whose idle
// polling turns cost $194–240 per review. All selection is done by build_workflow_args.py;
// this script only sequences launches, retries, Codex wait, integration and application.
// =====================================================================
export const meta = {
  name: 'moores-code-review',
  description: 'moores-code-review の系統並列発火→Codex完了待ち→統合→自動適用→post-check を決定論的に実行する',
  phases: [
    { title: 'Review', detail: 'lens / reviewer / Fable / investigator / verifier を並列発火（ファイルハンドオフ）' },
    { title: 'Integrate', detail: 'Codex 3本の完了を待ってから opus integrator が agents/・Codex結論・checks.json を統合' },
    { title: 'Apply', detail: '確定修正の自動適用と uloop compile（report-only では省略）' },
    { title: 'PostCheck', detail: '最終diffで post-check を発火し結果を適用' },
  ],
}

const A = args
if (!A || !A.runDir || !A.patchPath || !A.userPromptPath || !A.repoRoot || !A.skillRoot || !Array.isArray(A.systems)) {
  throw new Error('args 不足: scripts/build_workflow_args.py が出力した workflow-args.json の中身を args に渡すこと')
}
if (A.systems.length === 0) {
  throw new Error('systems が空: セレクタが1系統も選ばなかった（checks.json / build_workflow_args.py の stderr を確認する）')
}

// 再起動予算は1回（即時）。混雑で2回目も落ちたら欠員として申告し、親が再派遣する
// One immediate retry; if the second launch also fails, report a gap and let the parent relaunch
const RETRY_BUDGET = 2
const PLANNED_MODEL = new Map(A.systems.map((s) => [s.name, s.model]))

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
const CODEX_WAIT_SCHEMA = {
  type: 'object',
  properties: {
    results: {
      type: 'array',
      items: { type: 'object', properties: { name: { type: 'string' }, exit_code: { type: 'integer' }, status: { type: 'string' } }, required: ['name', 'exit_code', 'status'] },
    },
    waited_minutes: { type: 'number' },
  },
  required: ['results', 'waited_minutes'],
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
    post_check_selection_note: { type: 'string' },
    notes: { type: 'string' },
  },
  required: ['applied', 'compile', 'design_items', 'post_checks', 'post_check_selection_note'],
}
const POSTFIX_SCHEMA = {
  type: 'object',
  properties: {
    applied: { type: 'integer' }, escalated: { type: 'integer' },
    compile: { type: 'string', enum: ['ok', 'error', 'skipped'] },
    warnings: { type: 'array', items: { type: 'string' } },
    infos: { type: 'array', items: { type: 'string' } },
    notes: { type: 'string' },
  },
  required: ['applied', 'escalated', 'compile', 'warnings', 'infos'],
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

// 起動失敗・report未書込は1回だけ再起動。fable は quota 切れ（応答なし）に限り opus で再起動（手順書の規則）
// Retry once on failure / unwritten report; fable falls back to opus only on a null response (quota rule)
async function runSystem(s, phaseName) {
  let model = s.model
  let result = null
  for (let attempt = 1; attempt <= RETRY_BUDGET; attempt++) {
    result = await agent(reviewPrompt(s), { label: `${s.kind}:${s.name}`, phase: phaseName, model, schema: REPORT_SCHEMA })
    if (result && result.report_written) return { name: s.name, kind: s.kind, model, attempts: attempt, ok: true, result }
    const reason = result ? 'report 未書込' : '応答なし（起動失敗/上限）'
    if (attempt < RETRY_BUDGET) log(`${s.name}: ${reason} → 再起動 ${attempt}/${RETRY_BUDGET - 1}`)
    else log(`${s.name}: ${reason} → 予算切れ、欠員として申告`)
    if (model === 'fable' && result === null) model = 'opus'
  }
  return { name: s.name, kind: s.kind, model, attempts: RETRY_BUDGET, ok: false, result }
}

// parallel の falsy 枠を名前付きの欠員へ戻す（添字で突き合わせ、`.filter(Boolean)` で名前を失わない）
// Map falsy slots back to named gaps by index so no system silently disappears from the accounting
function accountFor(plans, raw) {
  return plans.map((p, i) => raw[i] || { name: p.name, kind: p.kind, model: p.model, attempts: RETRY_BUDGET, ok: false, result: null })
}

// ---- Review: 全系統を並列発火（同時数はランタイムがキューイング）----
log(`Review: ${A.systems.length} 系統を発火（lens/reviewer/Fable/investigator/verifier）`)
const reviewed = accountFor(A.systems, await parallel(A.systems.map((s) => () => runSystem(s, 'Review'))))
const noResponse = reviewed.filter((r) => !r.ok).map((r) => r.name)
const fallbacks = reviewed.filter((r) => r.ok && r.model !== PLANNED_MODEL.get(r.name)).map((r) => `${r.name}→${r.model}`)
log(`Review 完了: 応答 ${reviewed.length - noResponse.length}/${A.systems.length}` + (noResponse.length ? ` 応答なし ${noResponse.join(', ')}` : '') + (fallbacks.length ? ` fallback ${fallbacks.join(', ')}` : ''))

// ---- Integrate(1/2): Codex 3本の完了を待つ（手順書 Step 5「Codex 3本の完了を確認する（未完了なら待つ）」の実行形）----
let codexWait = null
const codexJobs = A.codexJobs || []
if (codexJobs.length) {
  const waitPrompt = [
    `Codex 外部監査 ${codexJobs.length} 本の完了を待つ係。各ジョブについて \`.final.md\` が非空になるまで待ち、結果を返す。`,
    `Jobs: ${JSON.stringify(codexJobs)}`,
    `手順: Bash の until ループ1本（30秒間隔・最大 ${A.codexWaitMaxMinutes || 20} 分）で全ジョブの final が非空になるまで待つ。echo/sleep を1ターンずつ回さない。`,
    `期限までに非空にならないジョブは \`python3 ${A.codexRecoverScript} --prompt <prompt> --out <out>\` を1回走らせ、終了コード（0=結論あり / 3=未完走 / 4=セッション無し / 5=認証失効）と status 文字列を返す。final が非空なら exit_code 0・status ok。`,
    'ファイルの中身は読まない。返答は構造化出力のみ。',
  ].join('\n')
  codexWait = await agent(waitPrompt, { label: 'codex-wait', phase: 'Integrate', model: 'haiku', schema: CODEX_WAIT_SCHEMA })
  if (!codexWait) log('codex-wait が応答しなかった。integrator 側の codex_recover.py に委ねる')
  else log(`Codex 完了待ち: ${codexWait.results.map((r) => `${r.name}=${r.status}(${r.exit_code})`).join(', ')} / ${codexWait.waited_minutes}分`)
}

// ---- Integrate(2/2): opus integrator 1体。欠員の権威は FS を持つ integrator（agents/*.md の実在・非空）----
const integratorPrompt = [
  `Read this : ${A.integratorPath}`,
  `Run dir : ${A.runDir}`,
  `Patch path : ${A.patchPath}`,
  `User prompt : ${A.userPromptPath}`,
  `Write integrated report to : ${A.runDir}/integrated.md`,
  `Repo root : ${A.repoRoot}`,
  `Skill root : ${A.skillRoot}（integration-rules.md・codex_recover.py はこの配下の絶対パスで参照する）`,
  `起動計画の系統 : ${A.systems.map((s) => s.name).join(', ')}`,
  `応答が無かった系統（自己申告ベース） : ${noResponse.length ? noResponse.join(', ') : 'なし'}`,
  '欠員の確定はあなたが行う: 起動計画の各系統について `agents/<name>.md` の実在と非空を突き合わせ、無いものを `missing_systems` に返し系統別回収状況に欠員として記録する（応答の有無は参考情報にすぎない）。',
  `Codex 3本の結論は \`.final.md\`${codexWait ? `（完了待ち結果: ${JSON.stringify(codexWait.results)}）` : ''}。不在なら codex_recover.py を先に走らせ、終了コード（0=結論あり / 3=未完走 / 4=セッション無し / 5=認証失効）を系統別回収状況に併記する。`,
  '返答は件数サマリ（Critical/Warning/Info/suppressed/設計判断件数）と欠員系統名のみ。',
].join('\n')
const integrated = await agent(integratorPrompt, { label: 'integrator', phase: 'Integrate', model: 'opus', schema: INTEGRATOR_SCHEMA })
if (!integrated || !integrated.integrated_written) {
  throw new Error('integrator が integrated.md を書けなかった（応答なし or integrated_written=false）。$RUNDIR/agents は残っているので親が integrator だけ再派遣する')
}
log(`Integrate 完了: C${integrated.critical} W${integrated.warning} I${integrated.info} S${integrated.suppressed} 設計判断${integrated.design_items}` + (integrated.missing_systems.length ? ` 欠員 ${integrated.missing_systems.join(', ')}` : ''))

// ---- Apply: 確定修正の自動適用＋compile＋最終diff＋post-check選択（report-only では省略）----
let apply = null
let postChecks = A.postChecks || []
let postCheckSelection = A.reportOnly ? { source: 'build_workflow_args(report-only)', note: 'patch＋detchecks.json で選択' } : null
if (!A.reportOnly) {
  const applyPrompt = [
    `Read this : ${A.orchestratorStepsPath} — Step 6 と Step 6.5 の 1〜3 だけを実行する（Step 2〜5 は完了済み。post-check agent の起動は親が行うので自分では起動しない）。`,
    `Run dir : ${A.runDir}`,
    `Integrated report : ${A.runDir}/integrated.md`,
    `Patch path : ${A.patchPath}`,
    `User prompt : ${A.userPromptPath}`,
    `Repo root : ${A.repoRoot}（修正はこの作業ツリーだけに加える）`,
    `Skill root : ${A.skillRoot}（integration-rules.md §3〜§5・scripts はこの配下の絶対パス）`,
    `Base ref : ${A.baseRef || '(未指定)'} — final.diff は「git diff <Base ref> -- <patch.diff が触ったファイル ∪ Step 6 で自分が編集・新規作成したファイル> ':(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs'」で作る（pathspec で絞る。作業ツリーが Base ref から別件で進んでいても無関係な差分を巻き込まないため）。未指定なら patch.diff に「git diff HEAD -- <同じファイル集合>」を連結する。`,
    '手順: (1) integrated.md の採用Critical のうち適用区分が自動適用可のものだけ適用する。設計判断は適用せず design.md（症状→原因→推奨と選択肢。コードを開かずに選べる形。0件なら「なし」）へ書く。',
    '(2) .cs を変えたら `uloop compile --project-path <Repo root>/moorestech_client` でエラー0を確認する（Editor不在で実行不能なら compile=skipped と返す）。',
    `(3) final.diff を書き、\`python3 ${A.deterministicChecksScript} <final.diff> --repo-root <Repo root>\` を ${A.runDir}/checks-final.json へ書く（--context は渡さない）。自分の修正が新たに生んだ confirmed/比較演算子違反はその場で直す。`,
    `(4) \`python3 ${A.selectPostChecksScript} ${A.runDir}/final.diff ${A.runDir}/checks-final.json\` を実行し、出力TSV（<post-check絶対パス>\\t<モデル>）を post_checks として返す（空なら []）。スキップしたガードと理由を post_check_selection_note に1行で書く（黙って縮退しない）。`,
    'Read規律: Edit対象の該当範囲だけを offset/limit で読む。ファイル全文Readしない。返答は構造化出力のみ。',
  ].join('\n')
  apply = await agent(applyPrompt, { label: 'apply', phase: 'Apply', model: 'sonnet', schema: APPLY_SCHEMA })
  if (!apply) throw new Error('apply agent が応答しなかった。integrated.md は残っているので親が Step 6 だけ再派遣する')
  postChecks = apply.post_checks.map((p) => ({ kind: 'postcheck', name: `postcheck-${p.path.split('/').pop().replace(/\.md$/, '')}`, path: p.path, model: p.model }))
  postCheckSelection = { source: 'apply(select_post_checks.py)', note: apply.post_check_selection_note }
  log(`Apply 完了: 適用 ${apply.applied} / compile ${apply.compile} / 設計判断 ${apply.design_items} / post-check ${postChecks.length}（${apply.post_check_selection_note}）`)
}

// ---- PostCheck: 選択された post-check だけ発火（空なら0トークン）----
let postResults = []
let postfix = null
let postMissing = []
if (postChecks.length) {
  // post-check は「最終diff＋最終checks」を見る（report-only では patch と Step 2 の決定論JSON）
  // Post-checks read the final diff and final checks (patch + Step 2 deterministic JSON in report-only)
  const diffPath = A.reportOnly ? A.patchPath : `${A.runDir}/final.diff`
  const candidatesPath = A.reportOnly ? A.detchecksPath : `${A.runDir}/checks-final.json`
  postResults = accountFor(postChecks, await parallel(postChecks.map((p) => () => runSystem(
    { ...p, kind: 'postcheck', patchOverride: diffPath, candidatesPath }, 'PostCheck',
  ))))
  postMissing = postResults.filter((r) => !r.ok).map((r) => r.name)
  if (postMissing.length) {
    throw new Error(`post-check が応答しなかった: ${postMissing.join(', ')}。final.diff と checks は残っているので親が Step 6.5 の該当ガードだけ再派遣する`)
  }
  const anyCritical = postResults.some((r) => r.result && r.result.critical_count > 0)
  if (!A.reportOnly && anyCritical) {
    const postfixPrompt = [
      `Read this : ${A.orchestratorStepsPath} — Step 6.5 の 4〜6 だけを実行する。`,
      `Run dir : ${A.runDir}`,
      `Post-check reports : ${postResults.map((r) => `${A.runDir}/agents/${r.name}.md`).join(', ')}`,
      `Repo root : ${A.repoRoot}`,
      `Skill root : ${A.skillRoot}`,
      '手順: rationale-guard の Critical は自動復元せず design.md へ追記（復元タグ案付き）。convention-guard は `機械的` を自動適用し `要判断` はガードの裁定で完結させる（webui は要判断も短縮適用）。同一行で衝突したら根拠保全を優先。.cs を変えたら uloop compile を再実行する。',
      '両レポートの Warning / Info は1件1行で warnings / infos に転記する（親が最終報告へ載せる。黙って落とさない）。',
      '返答は構造化出力のみ（適用数・escalate数・compile結果・warnings・infos）。',
    ].join('\n')
    postfix = await agent(postfixPrompt, { label: 'postfix', phase: 'PostCheck', model: 'sonnet', schema: POSTFIX_SCHEMA })
    if (!postfix) throw new Error('postfix agent が応答しなかった。final.diff と post-check レポートは残っているので親が Step 6.5 の 4〜6 だけ再派遣する')
  } else if (!A.reportOnly) {
    log('PostCheck: 両ガードとも Critical なし → postfix 省略（手順書 Step 6.5-6）')
  }
} else {
  log(`PostCheck: 発火条件未達でスキップ（0トークン）${postCheckSelection ? ` — ${postCheckSelection.note}` : ''}`)
}

return {
  systems: {
    planned: A.systems.length,
    expected: A.expectedSystems || null,
    responded: reviewed.length - noResponse.length,
    noResponse,
    fallbacks,
    // 欠員の権威は integrator（agents/*.md の実在確認）。noResponse は自己申告ベースの参考値
    // The integrator (file existence) is authoritative for gaps; noResponse is self-reported
    missing: integrated.missing_systems,
    perSystem: reviewed.map((r) => ({ name: r.name, ok: r.ok, model: r.model, attempts: r.attempts, critical: r.result ? r.result.critical_count : null, design: r.result ? r.result.design_judgement : null })),
  },
  codexWait,
  integrated,
  apply,
  postCheckSelection,
  postChecks: postResults.map((r) => ({ name: r.name, ok: r.ok, critical: r.result ? r.result.critical_count : null, report: `${A.runDir}/agents/${r.name}.md` })),
  postfix,
  paths: { integrated: `${A.runDir}/integrated.md`, design: `${A.runDir}/design.md`, finalDiff: A.reportOnly ? A.patchPath : `${A.runDir}/final.diff` },
}
