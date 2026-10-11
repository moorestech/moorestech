# Feedback Report Labels Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モードは同スキルの規模ゲートに従う。

**Goal:** 感想送信を感想として案内する。
**Architecture:** BugReportFormが選択種別に対応する送信UI文言を選ぶ。送信開始時の種別をpayloadと成功通知に共有する。翻訳の正本CSVからTSキーを正規生成する。
**Tech Stack:** React、TypeScript、Mantine、Vitest、Playwright、Unity/CEF。

## Requirements

- 感想の送信ボタンと成功通知は感想用文言。4言語で空訳・キー抜けがないこと。
- バグの送信ボタンと成功通知は既存文言を保持。
- 送信中の入力無効、欠損通知、拒否時のdraft保持、成功時のreset、payloadを保持。種別を開始時に固定。
- 感想の送信中表示も感想として案内する（agent既定）。
- 実機UIの録画、対象テスト、moores-code-review、push・独立PR作成まで実行。マージしない。

## Global Constraints

- 専用worktreeのみ変更。main repo配下へアクセスしない。
- コード各ファイル200行未満。生成コードを手編集しない。
- 自分のEditorだけ使用。PIDは親が記録済み。親の疎通通知前はUnity操作しない。
- バックグラウンドのポーリングで待たず全部前景で実行する／区切りごとにコミットする。
- 当セッション内で実行する明示委任が新規セッションhandoffより優先。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 既存フォームと翻訳の正本に閉じる
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱0・第3バケツ0 / 翻訳欠損の既存方針と新キー検証を明記して解消

## 配置インベントリ

| 項目 | 層 | 機構・前例 |
|---|---|---|
| BugReportForm.tsx | WebUI pauseMenu | useStateとdispatchAction、既存send経路 |
| Localization/localization.csv | 翻訳の正本 | ui.bugReportの同型キー |
| generated/localizationKeys.ts | 自動生成 | gen:i18n |
| BugReportFormTests/reportKindLabels.test.ts・testSupport.ts | unit | 既存rendererと辞書スタブ |
| e2e/tests/system/feedbackReportLabels.spec.ts | UIテスト | mockControlとpayloadsOf |
| scenarios/misc/feedback-report-labels.cs | 録画操作台本 | PlaytestDriverとPlaytestDomQuery |

データフロー: 種別入力→useBugReportDraft→BugReportForm→dispatchAction→成功通知。変更は表示読者に閉じ、通信の書き手を増やさない。

## Task 1: 文言の選択と翻訳

Files: Modify: moorestech_web/webui/src/features/pauseMenu/BugReportForm.tsx, Localization/localization.csv, moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts

- [x] CSVへui.feedbackReport.send/sent/sendingを追加。日本語は「感想を送信」「感想をoutboxに書き出しました」「感想を書き出しています…」。英語は既存Impressionsに、独語Eindrücke、韓国語감상に合わせる。
- [x] フォームの種別別文言を一箇所へ集め、送信開始時kindをpayloadと成功通知へ使う。

```ts
function reportLabels(kind: PauseMenuReportKind) {
  return kind === PauseMenuReportKinds.feedback ? L.ui.feedbackReport : L.ui.bugReport;
}
const kind = draft.kind;
const accepted = await dispatchAction("bug_report.submit", { description: trimmedDescription, kind });
// accepted後、欠損がない場合だけ t(reportLabels(kind).sent) を表示する
```

- [x] `pnpm --dir moorestech_web/webui gen:i18n`でキー生成。git差分で追加キーのみ確認。
- [x] Task 1をコミットする。

## Task 2: 状態・画面検証

Files: Modify: moorestech_web/webui/src/features/pauseMenu/BugReportFormTests/testSupport.ts; Create: moorestech_web/webui/src/features/pauseMenu/BugReportFormTests/reportKindLabels.test.ts, moorestech_web/webui/e2e/tests/system/feedbackReportLabels.spec.ts, .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/feedback-report-labels.cs

- [x] テストでfeedback/bugのボタン・成功、拒否時のfeedback draft保持、missing時の既存通知、送信中の無効と文言、遅延中kindが変更された際の開始時通知を確認する。

```ts
expect(sendButton(renderer).children.join("")).toBe("感想を送信");
expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", {description:"感想", kind:"feedback"});
expect(mocks.emitToast).toHaveBeenCalledWith("感想を書き出しました", "info");
```

- [x] `pnpm --dir moorestech_web/webui test -- src/features/pauseMenu src/shared/i18n/localizationKeysFreshness.test.ts`、`build`、`lint`を実行しPASSを確認。
- [x] Playwrightで4言語の実CSVのラベル・成功通知を確認しスクショと動画を保存。既存pauseMenu.spec.tsも通す。
- [x] 自分のUnityで録画シナリオを実行し実ゲーム視点・feedback/bugのDOMテキストを確認、録画スクショをshareへ保全し、PRにはdocs/pr-assets/feedback-report-labelsの全言語画像を掲載する（録画オーバーレイを含む画像は検証証拠として分ける）。送信そのものはmock/unitで検証し、実機から不要な感想バンドルを外部投入しない（agent前提、ユーザー依頼は文言修正）。
- [x] Task 2と検証結果をコミットする。

## Task 3: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること

- [ ] Codex runnerで選択された全系統を実行。Step 7.5 bug-passも実行。修正があれば対象テスト・UI録画を反映後再実行。
- [ ] 記録はmoorestech_logsに保存し成果物を保持する。

## Task 4: セッション終了可能状態にすること

- [ ] pr-createスキルで日本語PRを作成し実画面画像のGitHub URLを本文へ掲載。全変更コミット・push済みでコンフリクトがないことを確認。マージしない。
- [ ] /tmp/moorestech-task-a.final.mdへPR URL・SHA・review rundir・録画path・検証結果を記録。worktree破棄・Notion更新は親担当。

## 判断記録（ADR）

- 設計ADR: docs/adr/0078-feedback-report-submission-labels.md
- 出所: ユーザー裁定 2026-10-11 原文「感想選択時は感想用文言、バグ選択時は従来バグ文言へ合わせ全言語を揃える」。Task 1・2で実現。
- 出所: ユーザー裁定 2026-10-11 原文「送信成功・失敗・送信中の既存状態や送信データを壊さない」。Task 1の開始時スナップショットとTask 2の状態テストで実現。
- 出所: agent前提（同型掃引・既存翻訳の正本・BugReportFormの表示所有権）。sendingも切り替える。共通基盤は新設しない。
- 出所: agent前提（実機の余計な報告投入を避ける検証構成）。実機は本番DOMボタン表示、送信データ・成功/拒否/欠損はunitとmockホストで検証。
- 独立質問generate: 2026-10-11 questions=[]、filterはADR確定後に実行。

- 出所: agent前提（fresh-context Phase 2.6）。翻訳は型付き生成キーを使い、追加3キー×4言語non-emptyとfreshnessを検査する。翻訳欠損は既存tの処理へ従い変更しない。
- user-simulator review: Critical0・Warning0・要裁定0。レポート /tmp/moorestech-a-plan-review.md。採点: 自律委任のためユーザー反応未取得、予測を承認へ昇格しない。
- 独立質問filter: remaining0、thread 01a1289e-afe8-7d13-9ad4-160d399bc5d3。

## 単独セッション検証記録（2026-10-11）

- HEAD 930d06220bとcleanを確認して所有権を引継ぎ。感想送信文言だけを継続。
- Web再実行: unit129ファイル/992件PASS、Playwright8件PASS、build/lint成功。
- CSV追加3キー×4言語non-empty PASS。生成キーfreshnessを含むunit成功。
- Unity compile: Success=true、ErrorCount=0、WarningCount=0。
- 録画前提を既存任意スキットスキップ、設定UIによる日本語固定、既存RequestTransition、非フォーカスの検証用InputSystem設定へ補正。製品コードは変更しない。
- Unity録画: 20261011_110640/feedback-report-labels、Success=true、Assert2/2PASS、ErrorLogs0、動画6019812bytes。実プレイ視点と感想/バグの期待表示を画像で確認、動画全フレームデコード成功。
- 証拠保全: https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-11-feedback-report-labels-20261011/
- 旧planの親担当工程は、この独立セッションが引き継ぐ（Notion更新、worktree片付け）。
