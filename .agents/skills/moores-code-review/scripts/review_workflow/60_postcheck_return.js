// ---- PostCheck: 選択された post-check だけ発火（空なら0トークン。bug-pass では Apply が選択しない）----
// ---- PostCheck: launch only the selected post-checks (zero tokens when empty; bug-pass never selects any) ----
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
      '各レポートの Warning / Info は1件1行で warnings / infos に転記する（親が最終報告へ載せる。黙って落とさない）。',
      '返答は構造化出力のみ（適用数・escalate数・compile結果・warnings・infos）。',
    ].join('\n')
    postfix = await agent(postfixPrompt, { label: 'postfix', phase: 'PostCheck', model: 'sonnet', schema: POSTFIX_SCHEMA })
    if (!postfix) throw new Error('postfix agent が応答しなかった。final.diff と post-check レポートは残っているので親が Step 6.5 の 4〜6 だけ再派遣する')
  } else if (!A.reportOnly) {
    log('PostCheck: 全ガード Critical なし → postfix 省略（手順書 Step 6.5-6）')
  }
} else {
  log(`PostCheck: スキップ（0トークン）${postCheckSelection ? ` — ${postCheckSelection.note}` : ''}`)
}

return {
  mode: A.mode,
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
  refix,
  postCheckSelection,
  postChecks: postResults.map((r) => ({ name: r.name, ok: r.ok, critical: r.result ? r.result.critical_count : null, report: `${A.runDir}/agents/${r.name}.md` })),
  postfix,
  paths: { integrated: `${A.runDir}/integrated.md`, design: `${A.runDir}/design.md`, finalDiff: A.reportOnly ? A.patchPath : `${A.runDir}/final.diff` },
}
