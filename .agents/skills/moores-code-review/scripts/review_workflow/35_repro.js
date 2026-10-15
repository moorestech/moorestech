// 有効時だけ採用ロジックバグを反証し、根拠付き結果で再統合する。
// Only when enabled, challenge adopted logic bugs and reintegrate the evidence.
const reproResults = []
if (A.verifyByRepro) {
  if (!Array.isArray(integrated.logic_bugs)) throw new Error('実証対象 logic_bugs が無い')
  for (const [index, finding] of integrated.logic_bugs.entries()) {
    const report = `${A.runDir}/repro-${index + 1}.md`
    const prompt = [
      `Read this : ${A.reproVerifierPath}`,
      `Finding : ${JSON.stringify(finding)}`,
      `Patch path : ${A.patchPath}`,
      `User prompt : ${A.userPromptPath}`,
      `Main worktree : ${A.repoRoot}（編集禁止。git による隔離作成元としてだけ使用可）`,
      `読んでよいパス : ${A.skillRoot}, ${A.runDir}, ${A.userPromptPath}, 環境ローカル規約とその参照先、作成した隔離 worktree。メイン repo 配下の現物を直接読まない。`,
      `Write full report to : ${report}`,
      'バックグラウンドのポーリングで待たず全部前景で実行する。区切りごとに隔離側だけコミットする。',
    ].join('\n')
    const schema = { type: 'object', properties: {
      status: { type: 'string', enum: ['実証済み', '反証済み', '再現できず'] },
      evidence: { type: 'string', minLength: 1 }, cleaned: { type: 'boolean' }, report_written: { type: 'boolean' },
    }, required: ['status', 'evidence', 'cleaned', 'report_written'] }
    const result = await agent(prompt, { label: `repro-${index + 1}`, phase: 'Integrate', model: 'opus', schema })
    if (!result || !result.report_written || !result.cleaned || !result.evidence.trim()) {
      throw new Error(`実証 ${finding.id}: 報告または隔離環境の後片付けが未完了。親が ${report} を確認する`)
    }
    reproResults.push({ id: finding.id, report, ...result })
  }
  if (reproResults.length) {
    const prompt = [...integratorLines,
      `実証結果 : ${JSON.stringify(reproResults)}`,
      '結果のレポートを Read して再統合する。反証済みは §1.5 の構成的根拠を引用できる場合だけ棄却し件数を報告。再現できずは棄却・降格の理由にせず元の Critical を残す。実証済みは根拠を追記する。',
    ].join('\n')
    integrated = await agent(prompt, { label: 'integrator-repro', phase: 'Integrate', model: 'opus', schema: INTEGRATOR_SCHEMA })
    if (!integrated || !integrated.integrated_written) throw new Error('実証後の再統合が未完了')
  }
}
