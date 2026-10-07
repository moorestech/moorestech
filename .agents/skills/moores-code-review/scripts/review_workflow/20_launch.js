// ---- 系統の起動・再起動・欠員の数え上げ ----
// ---- Launching systems, retrying, and accounting for gaps ----

const footer = [
  '',
  `Repo root（コードを読む作業ツリー。Bash/Read はこの配下で行い、他のworktreeを読まない）: ${A.repoRoot}`,
  `Skill root（観点・ルール・scripts はこの配下の絶対パスで参照する）: ${A.skillRoot}`,
  ...(BUG_PASS ? ['Mode : bug-pass（最終バグ確認。Output contract 末尾の bug-pass 規則が観点本文より優先する）'] : []),
  '返答は構造化出力（Critical件数・設計判断あり/なし・一行要約・report_written）だけ。指摘本文は返答に書かず、必ず `Write full report to` のファイルへ全文を書く。',
]

function reviewPrompt(s) {
  const head = [`Read this : ${s.path}`]
  if (s.kind === 'investigator') head.push(`Chunk files : ${s.chunkFiles}`, `Chunks TSV : ${A.chunksTsv}`)
  if (s.kind === 'verifier' || s.kind === 'postcheck') head.push(`Candidates : ${s.candidatesPath || A.checksPath}`)
  if (s.kind === 'refix') head.push(
    `Refix of : ${s.refixOf}`,
    '前提: Patch path はレビュー済み変更に対する修正の反映差分だけで、元の変更全体ではない。問いは 2 つ。(1) この反映で新たに壊れたものは無いか（囲む関数全体を Read して判定）。(2) 反映は Refix of の修正方針どおりか — 方針と食い違う実装（条件式の評価時点・対象の取り違え・一部だけの適用・別の直し方への読み替え）は Critical にする。',
  )
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

// 反映 diff の機械的動作確認（orchestrator-steps.md「反映 diff の機械的動作確認」の実行形）。Apply と Refix の反映役が共有する
// Mechanical checks on an applied diff (executable form of the orchestrator-steps.md section); shared by Apply and Refix
function verifyStep(label, diffPath, snapshotName, retakeHint) {
  return `${label} 反映 diff の機械的動作確認（${A.orchestratorStepsPath} の「反映 diff の機械的動作確認」）: \`python3 ${A.appliedDiffChecksScript} ${diffPath} --repo-root ${A.repoRoot} --out-dir ${A.runDir}/refix/${snapshotName}-verify\` の JSON を見る。`
    + ' scenario_files が非空なら、改名・削除・シグネチャ変更に追従してその録画シナリオを直し、同じコマンドを再実行して作り直された compile_snippets を1本ずつ `uloop execute-dynamic-code --project-path <Repo root>/moorestech_client --code-file <snippet>` で流し、CompilationErrors のうちメッセージが changed_api の名前を含むもの（この反映の破壊）が0件になるまで直す。それ以外のエラーは master 由来の既存の壊れなので直さず本数だけ書く（Editor 不在なら「未確認（Editor 不在）」）。'
    + ` シナリオを直したら ${retakeHint}（シナリオの追従も反映 diff に含めて再レビューさせる）。`
    + ' save_load.touched が true なら `uloop run-tests --project-path <Repo root>/moorestech_client --filter-type regex --filter-value \'<save_load.test_regex>\'` を回し、この反映が落としたテストは直す。save_load.unverified の各行は「未確認」として verify_note に書き写す。'
    + ' 結果（シナリオ N 本追従・compile ok/未確認、セーブ往復 N passed/未確認と理由、どちらも該当なし）を verify_note に1行で返す。'
}
