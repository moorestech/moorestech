// ---- Review: 全系統を並列発火（同時数はランタイムがキューイング）----
// ---- Review: launch every system in parallel (the runtime queues beyond its concurrency cap) ----
log(`Review(${A.mode}): ${A.systems.length} 系統を発火`)
const reviewed = accountFor(A.systems, await parallel(A.systems.map((s) => () => runSystem(s, 'Review'))))
const noResponse = reviewed.filter((r) => !r.ok).map((r) => r.name)
const fallbacks = reviewed.filter((r) => r.ok && r.model !== PLANNED_MODEL.get(r.name)).map((r) => `${r.name}→${r.model}`)
log(`Review 完了: 応答 ${reviewed.length - noResponse.length}/${A.systems.length}` + (noResponse.length ? ` 応答なし ${noResponse.join(', ')}` : '') + (fallbacks.length ? ` fallback ${fallbacks.join(', ')}` : ''))

// ---- Integrate(1/2): Codex の完了を待つ（手順書 Step 5「Codex の完了を確認する（未完了なら待つ）」の実行形）----
// ---- Integrate(1/2): wait for the Codex jobs launched by the parent ----
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
// ---- Integrate(2/2): one opus integrator; it owns gap detection because it can see agents/*.md ----
const integratorLines = [
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
  `Codex の結論は起動された各ジョブ（${codexJobs.map((j) => j.name).join(', ') || 'なし'}）の \`.final.md\`${codexWait ? `（完了待ち結果: ${JSON.stringify(codexWait.results)}）` : ''}。不在なら codex_recover.py を先に走らせ、終了コード（0=結論あり / 3=未完走 / 4=セッション無し / 5=認証失効）を系統別回収状況に併記する。`,
  '返答は件数サマリ（Critical/Warning/Info/suppressed/設計判断件数）と欠員系統名のみ。',
]
if (BUG_PASS) integratorLines.push(
  `Mode : bug-pass — ${A.integrationRulesPath} §7（最終バグ確認の統合）で統合する。免責による suppress を効かせない・Critical は再現手順を1文で書けるものだけ・設計判断は常に0件（design_items=0）。`,
  `前周から持ち込む Warning の出所 : ${A.carriedWarningSources.length ? A.carriedWarningSources.join(', ') : 'なし'}（各ファイルの Warning 節を §7 の格上げ/破棄判定にかける）`,
)
const integrated = await agent(integratorLines.join('\n'), { label: 'integrator', phase: 'Integrate', model: 'opus', schema: INTEGRATOR_SCHEMA })
if (!integrated || !integrated.integrated_written) {
  throw new Error('integrator が integrated.md を書けなかった（応答なし or integrated_written=false）。$RUNDIR/agents は残っているので親が integrator だけ再派遣する')
}
log(`Integrate 完了: C${integrated.critical} W${integrated.warning} I${integrated.info} S${integrated.suppressed} 設計判断${integrated.design_items}` + (integrated.missing_systems.length ? ` 欠員 ${integrated.missing_systems.join(', ')}` : ''))
