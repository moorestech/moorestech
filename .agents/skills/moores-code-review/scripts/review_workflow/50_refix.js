// ---- Refix: 反映 diff（修正の前後差分）だけを applied-diff-correctness で再レビュー（report-only では省略）----
// レビューの出力を反映した diff はどの工程の入力にもならず誰にも再レビューされない（2026-09-08 cmux-connector c9baa79:
// 裁定の反映が判定式の評価時点を誤り 2 日間の機能停止。事後実測で行単位reviewerはその diff で Critical 到達）。
// 2 周目以降は「前回レビュー以降に変わった行」だけを見せ、問いを「壊れていないか・方針どおりか」に絞る。
// A diff that applies review output is never re-reviewed otherwise (c9baa79). Later rounds see only what changed
// since the last review and ask only "did the fix break something / does it match the stated fix".
const REFIX_MAX_ROUNDS = A.refixMaxRounds || 3
const refix = { enabled: !A.reportOnly, scope: apply ? apply.refix_scope : null, note: apply ? apply.refix_note : '', rounds: [], unresolved: false }
if (apply) {
  if (apply.refix_scope === 'error') {
    throw new Error(`反映 diff を作れなかった（${apply.refix_note}）。修正は適用済みなので、親が refix_snapshot.py の snapshot/diff を手で通してから resumeFromRunId で再開する`)
  }
  let scope = apply.refix_scope
  let diffPath = `${A.runDir}/refix/round1.diff`
  let refixOf = `${A.runDir}/integrated.md の「採用Critical」の修正方針`
  if (scope !== 'source') log(`Refix: 反映 diff が ${scope}（doc/テスト/コメントのみ or 差分なし）→ 再レビュー不要（0トークン）`)
  for (let round = 1; scope === 'source' && round <= REFIX_MAX_ROUNDS; round++) {
    const sys = { kind: 'refix', name: `refix-correctness-r${round}`, path: A.refixReviewerPath, model: 'opus', patchOverride: diffPath, refixOf }
    const r = await runSystem(sys, 'Refix')
    if (!r.ok) throw new Error(`${sys.name} が応答しなかった。${diffPath} は残っているので親が該当 round だけ再派遣する`)
    const entry = { round, name: r.name, model: r.model, critical: r.result.critical_count, design: r.result.design_judgement, report: `${A.runDir}/agents/${r.name}.md` }
    refix.rounds.push(entry)
    if (r.result.critical_count === 0) { log(`Refix r${round}: Critical なし → 収束`); break }
    if (round === REFIX_MAX_ROUNDS) {
      // 上限で止める: 指摘は尽きないので「再現可能な誤動作が無くなること」が収束条件。超えたら親へ未収束として渡す
      // Stop at the cap: findings never run out; convergence means no reproducible wrong behavior. Beyond it, hand back as unresolved
      refix.unresolved = true
      log(`Refix r${round}: Critical ${r.result.critical_count} 件が上限 ${REFIX_MAX_ROUNDS} 周で未収束 → 親へ申告`)
      break
    }
    const next = round + 1
    const nextDiff = `${A.runDir}/refix/round${next}.diff`
    const snapshotNext = `\`python3 ${A.refixSnapshotScript} snapshot --repo-root ${A.repoRoot} --run-dir ${A.runDir} --name s${next}\` に続けて \`python3 ${A.refixSnapshotScript} diff --repo-root ${A.repoRoot} --run-dir ${A.runDir} --from s${round} --to s${next} --out ${nextDiff}\``
    // bug-pass は設計判断を出さない: §4 に当たる件も症状を消す最小の変更で直し、直せないものだけ escalate する
    // bug-pass yields no design items: fix §4-type findings with the minimal symptom-removing change; escalate only what cannot be fixed
    const designRule = BUG_PASS
      ? '§4 の設計判断に当たる件も design.md へ回さず、§4 の推奨規則（報告された症状を消す最小の変更）で直す。最小の変更でも直せないものと「裁定そのものが誤り」型だけ escalated に数え、warnings に1行で書く（親は Warning として報告する）。'
      : '§4 の設計判断に当たる件と「裁定そのものが誤り」型は適用せず design.md へ追記し escalated に数える（Step 7 で AskUserQuestion に載る）。'
    const fixPrompt = [
      `Read this : ${A.integrationRulesPath}（§3〜§5 の適用区分・安全規則）— 反映 diff 再レビューの Critical を直し直す係。`,
      `Refix report : ${entry.report}`,
      `Run dir : ${A.runDir}`,
      `Repo root : ${A.repoRoot}（修正はこの作業ツリーだけに加える）`,
      `Skill root : ${A.skillRoot}`,
      `Base ref : ${A.baseRef || '(未指定)'}`,
      `手順: (0) 何も編集する前に ${recordStep(`s${round}`)} を実行する。(1) レポートの Critical のうち修正方針が具体名つきで選択の余地が無いものだけ §3 の規則どおり適用する（具体名どおり・波及先すべて）。${designRule}`,
      '(2) .cs を変えたら `uloop compile --project-path <Repo root>/moorestech_client` でエラー0を確認する（Editor不在で実行不能なら compile=skipped と理由。黙って省略しない）。',
      `(3) final.diff と checks-final.json を Apply と同じ作り方で作り直す（\`git diff <Base ref> -- <patch.diff が触ったファイル ∪ 編集・新規作成したファイル> ':(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs'\` → ${A.runDir}/final.diff、\`python3 ${A.deterministicChecksScript} ${A.runDir}/final.diff --repo-root ${A.repoRoot}\` → ${A.runDir}/checks-final.json）。自分の修正が新たに生んだ confirmed/比較演算子違反はその場で直す。`,
      `(4) ${snapshotNext} を実行し、scope を refix_scope に返す（失敗したら error と stderr）。`,
      verifyStep('(5)', nextDiff, `s${round}`, `s${next}`, `(4) をやり直して s${next} と round${next}.diff を取り直す`),
      'レポートの Warning / Info は1件1行で warnings / infos に転記する（親が最終報告へ載せる。黙って落とさない）。',
      '修正は外科的に行い、ReadはEdit対象の現物確認に絞る。返答は構造化出力のみ。',
    ].join('\n')
    const fix = await agent(fixPrompt, { label: `refix-apply-r${round}`, phase: 'Refix', model: 'sonnet', schema: REFIX_APPLY_SCHEMA })
    if (!fix) throw new Error(`refix-apply-r${round} が応答しなかった。${entry.report} は残っているので親が該当 round だけ再派遣する`)
    if (fix.refix_scope === 'error') throw new Error(`refix-apply-r${round} が反映 diff を作れなかった（${fix.refix_note}）。親が snapshot/diff を手で通してから再開する`)
    Object.assign(entry, { applied: fix.applied, escalated: fix.escalated, compile: fix.compile, verify: fix.verify_note, warnings: fix.warnings, infos: fix.infos })
    if (fix.applied === 0) {
      // 何も直していないのに次周へ進むと同じ Critical を同じ diff で見続けるだけ。未収束として親へ渡す
      // Moving on with nothing applied would re-review the same diff; hand back as unresolved instead
      refix.unresolved = true
      log(`Refix r${round}: 適用 0 件（escalated ${fix.escalated}）→ 未収束として親へ申告`)
      break
    }
    scope = fix.refix_scope
    diffPath = nextDiff
    refixOf = `${entry.report} の Critical の修正方針`
    if (scope !== 'source') log(`Refix r${round}: 直し直しが ${scope} のみ → 再レビュー終了`)
  }
}
