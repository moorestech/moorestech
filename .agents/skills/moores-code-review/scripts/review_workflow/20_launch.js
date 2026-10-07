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
function verifyStep(label, diffPath, fromName, toName, retakeHint) {
  const check = `\`python3 ${A.appliedDiffChecksScript} check ${diffPath} --repo-root ${A.repoRoot} --run-dir ${A.runDir} --from ${fromName} --to ${toName}\``
  return `${label} 反映 diff の機械的動作確認（${A.orchestratorStepsPath} の「反映 diff の機械的動作確認」）: ${check} の JSON を見る（.cs に触れていれば録画シナリオ全件を反映後の状態でコンパイルし、${fromName} の記録＝反映前と比べる。uloop compile でエラー0にした後に回す。compile=skipped なら Unity の型が反映前のままなので、結果に関わらずシナリオは「未確認（compile 未実施）」と書く）。`
    + ' scenarios.status が new_errors なら、scenarios.new（反映前に無く反映後に増えた診断＝この反映の破壊。名前を含まない型不一致もここに入る）が0件になるまで録画シナリオを反映に追従させて直す。'
    + ` シナリオを直したら ${retakeHint}。その後に同じ check を再実行する（シナリオの追従も反映 diff に含めて再レビューさせる）。scenarios.existing（反映前にもあった診断）は直さず本数だけ書く。`
    + ' unverified なら scenarios.unverified の理由をそのまま「未確認」と書く（反映前の診断が無いものを既存扱いにしない）。not_required なら該当なし。'
    + ' save_load.touched が true なら `uloop run-tests --project-path <Repo root>/moorestech_client --filter-type regex --filter-value \'<save_load.test_regex>\'` を回し、この反映が落としたテストは直す。save_load.unverified の各行は「未確認」として verify_note に書き写す。'
    + ' 結果（シナリオ 増分 N 件→追従後 0 件・既存 N 件／未確認と理由、セーブ往復 N passed／未確認と理由、該当なし）を verify_note に1行で返す。'
}

// 反映前の録画シナリオ診断を記録する（編集前に1回。.cs に触れなければ check が使わないだけ）
// Record the pre-change scenario diagnostics before editing (unused when the diff touches no .cs)
function recordStep(name) {
  return `\`python3 ${A.appliedDiffChecksScript} record --repo-root ${A.repoRoot} --run-dir ${A.runDir} --name ${name} --if-missing\`（反映前の録画シナリオ全件のコンパイル診断。.cs を編集する見込みが無ければ省いてよい。Editor 不在なら status=unavailable が記録され、後の check は「未確認」になる）`
}
