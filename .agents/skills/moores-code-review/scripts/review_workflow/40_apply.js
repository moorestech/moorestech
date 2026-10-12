// ---- Apply: 確定修正の自動適用＋compile＋最終diff＋機械的動作確認＋post-check選択（report-only では省略）----
// ---- Apply: auto-apply confirmed fixes, compile, final diff, mechanical checks, post-check selection ----
let apply = null
let postChecks = A.postChecks || []
let postCheckSelection = A.reportOnly ? { source: 'build_workflow_args(report-only)', note: 'patch＋detchecks.json で選択' } : null
if (!A.reportOnly) {
  const finalDiffRule = `git diff <Base ref> -- <patch.diff が触ったファイル ∪ 自分が編集・新規作成したファイル> ':(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs'`
  const snapshotS1 = `\`python3 ${A.refixSnapshotScript} snapshot --repo-root ${A.repoRoot} --run-dir ${A.runDir} --name s1\` に続けて \`python3 ${A.refixSnapshotScript} diff --repo-root ${A.repoRoot} --run-dir ${A.runDir} --from s0 --to s1 --out ${A.runDir}/refix/round1.diff\``
  // bug-pass の post-check（コメント保全）は回さない。コメント規約は本レビューで確認済みで、最終バグ確認の対象外
  // bug-pass skips the comment post-checks: conventions were covered by the main review and are out of scope here
  const postCheckStep = BUG_PASS
    ? '(4) bug-pass では post-check を選択しない。post_checks は [] とし、post_check_selection_note に「bug-pass では post-check を回さない」と書く。'
    : `(4) \`python3 ${A.selectPostChecksScript} ${A.runDir}/final.diff ${A.runDir}/checks-final.json\` を実行し、出力TSV（<post-check絶対パス>\\t<モデル>）を post_checks として返す（空なら []）。スキップしたガードと理由を post_check_selection_note に1行で書く（黙って縮退しない）。`
  const applyPrompt = [
    `Read this : ${A.orchestratorStepsPath} — Step 6 と Step 6.5 の 1〜3 と「反映 diff の機械的動作確認」だけを実行する（Step 2〜5 は完了済み。post-check agent と反映 diff 再レビュー（applied-diff-correctness）の起動は親が行うので自分では起動しない）。`,
    `Run dir : ${A.runDir}`,
    `Integrated report : ${A.runDir}/integrated.md`,
    `Patch path : ${A.patchPath}`,
    `User prompt : ${A.userPromptPath}`,
    `Repo root : ${A.repoRoot}（修正はこの作業ツリーだけに加える）`,
    `Skill root : ${A.skillRoot}（integration-rules.md §3〜§5・scripts はこの配下の絶対パス）`,
    `Base ref : ${A.baseRef || '(未指定)'} — final.diff は「${finalDiffRule}」で作る（pathspec で絞る。作業ツリーが Base ref から別件で進んでいても無関係な差分を巻き込まないため）。未指定なら patch.diff に「git diff HEAD -- <同じファイル集合>」を連結する。`,
    `手順: (0) 何も編集する前に \`python3 ${A.refixSnapshotScript} snapshot --repo-root ${A.repoRoot} --run-dir ${A.runDir} --name s0\` を実行する（作業ツリーの snapshot。HEAD/index/作業ツリーは変わらない。Step 6.5-2.5 の反映 diff の基点）。続けて ${recordStep('s0')} を実行する。`,
    BUG_PASS
      ? '(1) integrated.md の採用Critical（bug-pass では全件が自動適用可・修正方針は症状を消す最小の変更）を適用する。design.md は書かない。'
      : '(1) integrated.md の採用Critical のうち適用区分が自動適用可のものだけ適用する。設計判断は適用せず design.md（症状→原因→推奨と選択肢。コードを開かずに選べる形。0件なら「なし」）へ書く。',
    '(2) .cs を変えたら `uloop compile --project-path <Repo root>/moorestech_client` でエラー0を確認する（Editor不在で実行不能なら compile=skipped と返す）。',
    `(3) final.diff を書き、\`python3 ${A.deterministicChecksScript} <final.diff> --repo-root <Repo root>\` を ${A.runDir}/checks-final.json へ書く（--context は渡さない）。自分の修正が新たに生んだ confirmed/比較演算子違反はその場で直す。`,
    `(3.5) ${snapshotS1} を実行し、出力 JSON の scope を refix_scope に、files/source_files の要約を refix_note に返す（反映 diff は親が applied-diff-correctness で再レビューする）。スクリプトが失敗したら refix_scope=error とし stderr を refix_note に書く（黙って source/none にしない）。`,
    verifyStep('(3.6)', `${A.runDir}/refix/round1.diff`, 's0', 's1', `(3.5) をやり直して s1 と round1.diff を取り直し、final.diff と checks-final.json も (3) と同じ作り方で作り直す`),
    postCheckStep,
    '修正は外科的に行い、ReadはEdit対象の現物確認に絞る。返答は構造化出力のみ。',
  ].join('\n')
  apply = await agent(applyPrompt, { label: 'apply', phase: 'Apply', model: 'sonnet', schema: APPLY_SCHEMA })
  if (!apply) throw new Error('apply agent が応答しなかった。integrated.md は残っているので親が Step 6 だけ再派遣する')
  postChecks = BUG_PASS ? [] : apply.post_checks.map((p) => ({ kind: 'postcheck', name: `postcheck-${p.path.split('/').pop().replace(/\.md$/, '')}`, path: p.path, model: p.model }))
  postCheckSelection = { source: 'apply(select_post_checks.py)', note: apply.post_check_selection_note }
  log(`Apply 完了: 適用 ${apply.applied} / compile ${apply.compile} / 設計判断 ${apply.design_items} / 反映diff ${apply.refix_scope} / 動作確認 ${apply.verify_note} / post-check ${postChecks.length}（${apply.post_check_selection_note}）`)
}
