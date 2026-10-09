// =====================================================================
// moores-code-review Step 3.5〜6.5（mode=review）と Step 7.5 最終バグ確認（mode=bug-pass）の Workflow スクリプト。
// 仕様の正本は references/orchestrator-steps.md。sonnet オーケストレータ subagent が「待つだけで1ターン＝全コンテキスト再送」を
// 590〜625回繰り返し 1本 $194〜240 を空転で燃やしていた（2026-08-20 再計測）。待機を JS の await に置き換え、
// model明示・起動失敗の再起動・欠員の申告・Codex完了待ちを手順書の散文でなくコードで強制する。
// 同時実行数はランタイムが min(16, CPU-2) でキューイングする（CPU 18コア以上へ移すなら 12体/波の分割をここに足す）。
// 分割構成: Workflow スクリプトはファイルシステムも import も持たない（本体は関数本体として評価される）ため、
// scripts/review_workflow/*.js を build_workflow_args.py がファイル名順に結合して $RUNDIR/review_workflow.js を書く。
// 下の `const A = args` は結合時に workflow-args.json の中身（選択は済んでいる）へ置き換わる。変更時は tests/test_skill_wiring.py を必ず通すこと。
//
// Workflow script for moores-code-review Steps 3.5–6.5 (mode=review) and the Step 7.5 final bug pass (mode=bug-pass).
// The runtime has no filesystem or import, so build_workflow_args.py concatenates scripts/review_workflow/*.js
// in file-name order into $RUNDIR/review_workflow.js and embeds the args JSON in place of `const A = args`.
// All selection is done by build_workflow_args.py;
// this script only sequences launches, retries, Codex wait, integration and application.
// =====================================================================
export const meta = {
  name: 'moores-code-review',
  description: 'moores-code-review の系統並列発火→Codex完了待ち→統合→自動適用→反映diff再レビュー(Refix)→post-check を決定論的に実行する（mode=bug-pass は正しさ系だけの最終バグ確認）',
  phases: [
    { title: 'Review', detail: 'reviewer / Fable / investigator / verifier を並列発火（ファイルハンドオフ）' },
    { title: 'Integrate', detail: 'Codex の完了を待ってから opus integrator が agents/・Codex結論・checks.json を統合' },
    { title: 'Apply', detail: '確定修正の自動適用と uloop compile と反映diffの機械的動作確認（report-only では省略）' },
    { title: 'Refix', detail: '反映diff（修正の前後差分）だけを applied-diff-correctness で再レビューし、Critical なら直し直す（最大3周・report-only では省略）' },
    { title: 'PostCheck', detail: '最終diffで post-check を発火し結果を適用（bug-pass では省略）' },
  ],
}

const A = args
if (!A || !A.runDir || !A.patchPath || !A.userPromptPath || !A.repoRoot || !A.skillRoot || !Array.isArray(A.systems)) {
  throw new Error('args 不足: scripts/build_workflow_args.py が書いた $RUNDIR/review_workflow.js（args 埋め込み済み）を scriptPath に渡すこと')
}
if (A.systems.length === 0) {
  throw new Error('systems が空: セレクタが1系統も選ばなかった（checks.json / build_workflow_args.py の stderr を確認する）')
}

// mode は build_workflow_args.py が必ず書く。未知の値は黙って review に倒さず止める
// build_workflow_args.py always writes mode; never fall back to review on an unknown value
const BUG_PASS = A.mode === 'bug-pass'
if (A.mode !== 'review' && !BUG_PASS) throw new Error(`args.mode が不正: ${A.mode}（review / bug-pass）`)
if (BUG_PASS && A.reportOnly) throw new Error('bug-pass は report-only と併用しない（pr-independent-review からは回さない）')
if (BUG_PASS && !Array.isArray(A.carriedWarningSources)) throw new Error('bug-pass に carriedWarningSources が無い（--carry-from を渡す）')

// 再起動予算は1回（即時）。混雑で2回目も落ちたら欠員として申告し、親が再派遣する
// One immediate retry; if the second launch also fails, report a gap and let the parent relaunch
const RETRY_BUDGET = 2
const PLANNED_MODEL = new Map(A.systems.map((s) => [s.name, s.model]))
