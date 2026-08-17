# 実装トークン効率の比較実験(moorestech版): codex(luna, xhigh) 委譲 vs Sonnet sdd 直接実装

目的: 今後の実装のトークン効率改善に向けて、**同一planの同一Phase**を別構成で二重実装し、
トークン・時間・手戻り・品質を比較する(2026-08-16 開始)。
原型: `../cmux-connector/docs/research/2026-08-16-impl-model-comparison.md`。cmux版との最大の違いは
(a) 同一タスクの二重実装で**タスク差の交絡が無い**こと、(b) Unity検証を1段目が順次代行すること。

## 構成(三段・セッションは1本のみ)

| 段 | 役割 | 実体 | モデル |
|---|---|---|---|
| 1段目 | 全体オーケストレーション + 検証代行 + アームAB評価 + 事後品質ゲート | セッション本体 | Opus |
| 2段目 | アーム別の実装オーケストレーション(委譲・計測) | `.claude/agents/arm-a-codex-orchestrator.md` / `arm-b-sdd-orchestrator.md`(各 `isolation: worktree`) | **Sonnet + effort: high**(定義で固定) |
| 3段目 | 実際の実装 | A: Codex CLI `gpt-5.6-luna` + `--effort max`(=xhigh) / B: sddのimplementer・fix・task-reviewer subagent(`model: sonnet` 明示) | 左記 |

| アーム | 題材 | ブランチ |
|---|---|---|
| A | plan `docs/superpowers/plans/2026-08-16-mapmaking-visual-parity.md` の **Task 1〜4(Phase A: Transform貫通)** | `exp/phase-a-codex` |
| B | 同上(同一タスクの二重実装) | `exp/phase-a-sdd` |

両ブランチの起点は `feat/mapmaking-visual-parity`(plan・ADRを含む)。
交絡: タスク差なし。ただし**検証は1段目直列のため検証待ちが所要時間に乗る** — 時間は
「実装時間」と「検証待ち時間」を分けて記録する。

## 実験上のplan上書き(オーケストレータ指示はplanに優先。裁定3件: .decisions/2026-08-16-実装比較実験*/比較実験*)

- **`../moorestech_master` への変更・ブランチ作成・コミットは行わない**(共有checkoutのため二重実装が衝突する)。
  Task 2はスクリプト作成+**repo内テストデータ3件のみ**移行実行。master側の実行・ピン更新は勝者統合後に1段目が1回行う
- **uloopは使わない**(アームworktreeにはEditorが無い)。planの `uloop compile`/`uloop run-tests` ステップは
  **1段目への検証依頼に置換**する: タスク完了ごとに `SendMessage(to:"main")` で
  `VERIFY arm=<A|B> branch=<br> commit=<hash> tests=<正規表現>` を送り、返答(コンパイル結果+テスト結果)を待つ。
  失敗なら修正して再依頼(往復数を計測)。TDDの「失敗を確認する」ステップは省略し、タスク完了時の一括検証に置換
- Task 4 Step 6(templateワールド起動確認)はアームでは行わない(勝者確定後に1段目が実施)
- plan最終タスク(moores-code-review)・統合・pushは実施しない(評価フェーズ後に1段目が勝者ブランチにのみ行う)
- アームは `.decisions/`・bd・plan本体のチェックボックス以外のdocsを変更しない(計測ログは除く)

## 1段目の検証代行手順(直列FIFO)

1. main worktree(単一Editor)で `git checkout --detach <依頼コミット>`
   (アームworktreeがブランチを保持しているため**ブランチ名でのcheckoutは不可**。必ずdetach)
   ※main treeの `.moorestech-external-revisions.json` はdirtyのまま跨いでよい(どのブランチも未変更のため)
2. `uloop compile --project-path ./moorestech_client` → エラー全文を控える
3. コンパイル成功時のみ `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<依頼のtests>"`
4. 結果(エラー/失敗テストの全文)を依頼元アームへSendMessageで返す
5. 両アームの依頼が重なったら到着順。検証中に来た依頼は待たせる
6. 全検証完了後 `git checkout feat/mapmaking-visual-parity` へ戻す

## 記録手順

- 2段目の各アームは自分のブランチに専用ログを作りタスク/委譲ごとに追記・コミットする:
  - アームA: `docs/research/impl-comparison-arm-a-log.md` — 委譲ごとにwrapper stderrの
    `[usage]` 行(input/cached/output/reasoning)と往復数・検証依頼往復数を転記
  - アームB: `docs/research/impl-comparison-arm-b-log.md` — タスクごとにimplementer/fix/task-reviewer
    派遣数と手戻り内容・検証依頼往復数
- 1段目は検証代行ごとに時刻・対象アーム・結果を本台帳の「検証ログ」節へ追記する
- 1段目は両アーム完了後 `ccuse` でセッション合計トークン(全subagent込み)を記録する。
  **計測の限界(cmux版と同じ・明記)**: Claude側トークンはアーム別に厳密分離できない。
  厳密なのはcodex側 `[usage]` 合算のみ。アーム別Claudeトークンは完了報告からの概算に留める
- 共通で記録: 開始/終了時刻(実装と検証待ちを分離)、検証往復数、初回一発通過か、
  最終diff規模(`git diff --stat feat/mapmaking-visual-parity`)

計測前提(cmux 2026-08-16 実測の流用): codex-cli 0.147.0で `gpt-5.6-luna`+`xhigh` 受理。
codex側はsuperpowersプラグイン読込で1呼び出し ~37k input(大半cached)の固定費。
wrapperは `~/.agents/skills/codex-implement/scripts/codex-implement.mjs`(stderrに `[usage]` 合算)。

## 評価フェーズ(両アーム完了後・同一セッションの1段目が実施)

1. **定量集計**: アームA/Bの記録から、実装トークン(実装者+監督を分けて)・ドル換算
   (単価はclaude-apiスキルとcodex-auditスキルの現行価格表で再確認)・所要時間(検証待ち分離)・
   検証往復数・diff規模を表にする。codex固定費(~37k input/呼び出し)は別行で明示
2. **定性評価(クロスレビュー)**: 両ブランチのdiffを、planの `## Requirements` R1〜R4を採点表として評価。
   評価者2系統:
   - Opus reviewer subagent(観点: 要件充足 / 家風・コメント規律(2行セット等AGENTS.md準拠) /
     fail-fast規律 / テストの質。各1-5点+指摘列挙)
   - codex-audit(`gpt-5.6-sol` 固定 — どちらの実装者とも別モデルで審判の肩入れを避ける)に同じ採点表
3. **判定**: スコアと定量値を突き合わせ結論欄を埋める。「品質同等ならトークン安、
   品質差があるなら手戻り込みの実効コスト」で比較し、**勝者アームを1つ選ぶ**
4. **事後品質ゲート(勝者のみ)**: 勝者ブランチへmoores-code-review → 指摘修正 →
   `feat/mapmaking-visual-parity` へ統合(masterへは行かない) → その後1段目が
   Task 2のmoorestech_master側実行+ピン更新とTask 4 Step 6(template起動確認)を消化。
   レビュー指摘数・修正diff量を評価結果表へ追記。敗者ブランチはマージせず記録として残置。
   worktreeは統合後に撤去

## 検証ログ(1段目が追記)

| 時刻 | アーム | コミット | compile | tests | 備考 |
|---|---|---|---|---|---|

## 評価結果

| 観点 | アームA(codex luna xhigh) | アームB(sdd Sonnet) |
|---|---|---|
| 実装者トークン / $ | | |
| 監督トークン / $ | | |
| 所要時間(実装/検証待ち) | | |
| 検証往復数 | | |
| 要件充足(Opus採点 / codex採点) | | |
| 家風・コメント規律 | | |
| テストの質 | | |
| 事後moores-code-reviewの指摘数 / 修正量(勝者のみ) | | |

## 結論(評価フェーズで記入)

- トークン(実装1Phaseあたり・ドル換算含む):
- 手戻り・品質:
- 勝者と次に採る構成:
