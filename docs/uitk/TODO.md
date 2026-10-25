# UI Toolkit 移行の台帳

計画 `docs/uitk-migration/plan.md` の段階 1〜5（束）と、現行 Web UI の全機能の対応表。束ごとの暗転・切替と、移行中に Web へ入った変更の記帳もここで持つ。CI の追従ゲート G1（`.github/workflows/uitk-web-follow-gate.yml`）が「## 束」「## 行」「## 追従記録」の表を読む。

## 読み方と運用

- 束 = 計画 §1 の段階 1〜5。状態は `Web`（まだ作っていない）→ `暗転`（UITK 版を配布画面に出さずに作っている）→ `切替済み`（所有表で UITK に切り替えた）
- 暗転に入るとき: その束の `状態` を `暗転` にし、`暗転の起点` にその時点の master のコミット（10 桁）を書く
- 暗転中に Web のその束のパスを触るコミットが入ったら: 「## 追従記録」に `写した`（UITK 側の写し先）か `捨てた`（理由）を書く。G1 は暗転中の未記帳の件数を毎回出す
- 切替 PR: `状態` を `切替済み`、その束だけを持つ行の `所有者` を `UITK` にする。G1 は、起点より後にその束の Web パスを触った非マージコミットが全部記帳されていなければ落とす
- 切替済みの束の Web パスへの追加・変更は G1 が落とす（描かれないコードへの変更は失われる。直すなら UITK 側）。削除は通す。複数の束が持つパスは、全部の束が切替済みになってから落ちる
- 切替を戻すときは `状態` を `暗転` に戻す（G1 は base と head の両方で切替済みのパスだけを落とす）
- `Web パス` は `moorestech_web/webui/` からの相対。末尾 `/` はディレクトリ、それ以外はファイル。ファイルの持ち主は最長一致の行（同じ長さなら全部）
- `moorestech_web/webui/src/` の下のファイルはどれかの行に入っていなければならない（G1 が落とす）。新しい Web 画面を足すときは、ここに行を足すか既存の行のパスに入れる
- `e2e/` の下に新しい spec・fixture・撮影スクリプトを足すときは、その画面の行の `Web パス` に足す。足さないと受け皿の「Web の検証基盤」（束 5）の持ち物になり、その束の切替で記帳を求められない（G1 は `e2e/` の網羅を検査しない）
- セルに `|` を書かない。空欄は `—`

## 束

| 束 | 中身 | 状態 | 暗転の起点 | 物差し |
|---|---|---|---|---|
| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | Web | — | `uitk-lifeline` |
| 2 | 常駐表示（入力の無い HUD・通知・ツールチップ・ワールドピン・チュートリアル枠・スキット） | Web | — | `uitk-opening-hud` |
| 3 | 建築の束（ビルドメニュー・ホットバー・BP 名入力） | Web | — | `uitk-build-bp` |
| 4 | 持ち物の束（インベントリ・装備・掴み・レシピ・ブロック UI 全族・研究） | Web | — | `uitk-production-loop`・`uitk-research-train` |
| 5 | 最後の束と撤去（チャレンジ一覧・言語ゲート・チュートリアル枠・Web の横断層） | Web | — | `uitk-challenge-gate` |

## 行

所有者の列が所有表の初版（全部 Web）。`agent 既定` は行に効く agent 既定（全体は「## agent 既定（ユーザー未裁定）」）、`実測の結論` は行に効く実測（全体は「## 実測の結論」）。

| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |
|---|---|---|---|---|---|---|---|
| pauseMenu（ポーズ・バグ報告・入れ子のポーズ） | 機能 | 1 | Web | `src/features/pauseMenu/` `e2e/tests/system/pauseMenu.spec.ts` `e2e/tests/system/pauseMenuSettingsLayout.spec.ts` `e2e/tests/system/feedbackReportLabels.spec.ts` `e2e/mock-host/pauseMenuActions.ts` `e2e/mock-host/topics/pauseMenuControls.ts` | 裁定 2=A（第一弾は命綱） | — | — |
| settings（画質・言語・ダッシュ方式） | 機能 | 1 | Web | `src/features/settings/` | 裁定 2=A | — | — |
| toast | 機能 | 1 | Web | `src/features/toast/` `e2e/tests/regression/actionErrors.spec.ts` | 裁定 2=A。様式化 (a) | — | — |
| commonHud（クロスヘア） | 機能 | 2 | Web | `src/features/commonHud/` `e2e/tests/system/commonHud.spec.ts` | — | — | — |
| keyHint | 機能 | 2 | Web | `src/features/keyHint/` | — | — | — |
| modeHud（配置 HUD・削除モードの帯） | 機能 | 2 | Web | `src/features/modeHud/` `e2e/tests/modeHud/` `e2e/support/operationHudAssertions.ts` | — | — | — |
| progress（採掘ゲージ） | 機能 | 2 | Web | `src/features/progress/` `e2e/tests/progress.spec.ts` `e2e/capture-mining-progress.ts` | — | — | — |
| trainHud（乗車中の分岐表示） | 機能 | 2 | Web | `src/features/trainHud/` | 様式化 (a) | — | — |
| notification（獲得通知） | 機能 | 2 | Web | `src/features/notification/` `e2e/tests/notification/` `e2e/captures/capture-connection-line-notifications.ts` | 確認=演出は実時間 | — | — |
| skit（会話窓・選択肢・ツールバー・暗転） | 機能 | 2 | Web | `src/features/skit/` `src/app/skitTokens.css` `e2e/tests/skit.spec.ts` `e2e/mock-host/skitActions.ts` `e2e/capture-skit.ts` | 確認=演出は実時間 | — | — |
| challenge: CurrentChallengeHud | 機能 | 2 | Web | `src/features/challenge/CurrentChallengeHud.tsx` `src/features/challenge/CurrentChallengeHud.module.css` `e2e/tests/challenge/` `e2e/challenge-hud/` `e2e/support/challengeHudAssertions.ts` | — | — | — |
| tutorial: KeyControlHintHud | 機能 | 2 | Web | `src/features/tutorial/KeyControlHintHud.tsx` `src/features/tutorial/KeyControlHintHud.test.ts` `src/features/tutorial/keyControlHint.module.css` | 確認=演出は実時間 | — | — |
| tutorial: WorldPinOverlay | 機能 | 2 | Web | `src/features/tutorial/WorldPinOverlay.tsx` `src/features/tutorial/worldPin.module.css` `e2e/tests/system/worldPin.spec.ts` `e2e/capture-world-pin-arrow.ts` | 確認=演出は実時間 | — | — |
| CursorTooltip | 共通部品 | 2 | Web | `src/shared/tooltip/` | — | — | — |
| オーバーレイ: 背景スキット | オーバーレイ | 2 | Web | `src/app/App.tsx` `e2e/tests/phaseC4.spec.ts` | 前回は状態機械だけで洗い出して見落とした（`docs/uitk-migration/research/understand-shipping.md` §6.2） | — | — |
| オーバーレイ: Ctrl+U の全体非表示 | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: カットシーン中の退避 | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: 背面の通知層（stage の裏） | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: カーソル追従（ツールチップ・掴みの絵） | オーバーレイ | 2 4 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: 常駐 HUD の可否（ホットバー・装備） | オーバーレイ | 3 4 | Web | `src/app/App.tsx` | — | — | — |
| オーバーレイ: 画面のマウント点と背景ディム | オーバーレイ | 1 2 3 4 5 | Web | `src/app/App.tsx` `src/app/App.module.css` `src/app/App.architecture.test.ts` | 裁定 1=A（束単位で暗転のまま作り所有表で切り替える） | — | — |
| オーバーレイ: 再接続・辞書エラー・エラー境界 | オーバーレイ | 5 | Web | `src/app/App.tsx` `src/app/AppErrorBoundary.tsx` `e2e/tests/regression/connection.spec.ts` | — | — | — |
| buildMenu | 機能 | 3 | Web | `src/features/buildMenu/` `e2e/tests/regression/buildMenu.spec.ts` `e2e/tests/regression/buildMenuLayout.spec.ts` `e2e/capture-buildmenu.ts` `e2e/mock-host/fixtures/buildMenuFixtures.ts` | — | — | — |
| hotbar | 機能 | 3 | Web | `src/features/hotbar/` `e2e/tests/hotbar/` `e2e/mock-host/fixtures/hotbarFixtures.ts` | — | — | — |
| modal（BP 名入力） | 機能 | 3 | Web | `src/features/modal/` `e2e/tests/modal.spec.ts` | — | — | — |
| placementTarget（配置対象の表示名） | 共通部品 | 2 3 4 | Web | `src/shared/placementTarget/` | — | — | — |
| pointerGesture（押下・ドラッグの判定） | 共通部品 | 3 4 5 | Web | `src/shared/pointerGesture/` | — | — | — |
| inventory（インベントリ・装備・掴み） | 機能 | 4 | Web | `src/features/inventory/` `e2e/tests/inventory/` `e2e/mock-host/inventoryModel.ts` | 裁定 1=A（持ち物の束は束ごと一度に切り替える） | — | — |
| recipe（レシピ・アイテム一覧・長押しクラフト） | 機能 | 4 | Web | `src/features/recipe/` `e2e/tests/recipe/` `e2e/mock-host/fixtures/recipeFixtures.ts` `e2e/craft-chrome/` `e2e/support/craftChromeAssertions.ts` | 確認=演出は実時間 | — | — |
| blockInventory（汎用族・特殊族・列車族） | 機能 | 4 | Web | `src/features/blockInventory/` `e2e/tests/block/` `e2e/tests/train/` `e2e/tests/train.spec.ts` `e2e/tests/regression/sectionStack.spec.ts` `e2e/mock-host/fixtures/blocks/` `e2e/mock-host/blockDetailFixtures.ts` `e2e/mock-host/detailActions.ts` `e2e/mock-host/detailActions.test.ts` `e2e/mock-host/train/` `e2e/mock-host/fixtures/trainInventoryFixtures.ts` `e2e/capture-machine-qa.ts` `e2e/capture-train-timetable.ts` `e2e/fixtures/` | — | — | — |
| blockInventory 特殊族: ロケット（Tier9 #1551） | 機能 | 4 | Web | `src/features/blockInventory/details/rocket/` | — | — | — |
| blockInventory 特殊族: 宇宙エレベーター（Tier9 #1552） | 機能 | 4 | Web | `src/features/blockInventory/details/spaceElevator/` | — | — | — |
| research（研究ツリー・詳細ペイン） | 機能 | 4 | Web | `src/features/research/` `e2e/tests/research/` `e2e/mock-host/researchFixtures.ts` `e2e/capture-research-qa.ts` `e2e/support/panSettle.ts` | — | — | — |
| tutorial: DragGuide（インベントリ内） | 機能 | 4 | Web | `src/features/tutorial/overlay/DragGuide.tsx` | 確認=演出は実時間 | — | — |
| itemMove（掴みの計画） | 共通部品 | 4 | Web | `src/shared/itemMove/` `e2e/tests/regression/itemSlot.spec.ts` | — | — | — |
| ownedCounts | 共通部品 | 4 | Web | `src/shared/ownedCounts.ts` `src/shared/ownedCounts.test.ts` | — | — | — |
| treeView（パン・ズーム・線） | 共通部品 | 4 5 | Web | `src/shared/treeView/` | — | — | — |
| challenge: ChallengePanel | 機能 | 5 | Web | `src/features/challenge/` `e2e/tests/challenge.spec.ts` | 様式化 (a) | — | — |
| eventLanguageGate（出展モードの言語選択） | 機能 | 5 | Web | `src/features/eventLanguageGate/` | — | — | — |
| tutorial: TutorialOverlay（ハイライト枠） | 機能 | 5 | Web | `src/features/tutorial/` `e2e/tests/system/tutorial.spec.ts` `e2e/tests/system/tutorialHighlightClip.spec.ts` `e2e/support/pulseFreeze.ts` `e2e/mock-host/topics/tutorialControls.ts` | 確認=演出は実時間 | — | — |
| tutorialAnchor（アンカーの登録と解決） | 共通部品 | 1 2 3 4 5 | Web | `src/shared/tutorialAnchor/` | — | — | — |
| shared/ui（枠・スロット・ボタン・ゲージ） | 共通部品 | 1 2 3 4 5 | Web | `src/shared/ui/` `src/shared/clamp01.ts` `src/shared/clamp01.test.ts` `src/shared/materialTooltipText.ts` `src/app/slotTokens.css` `src/app/iconTextOutlineDesign.test.ts` `e2e/capture-iconbutton.ts` `e2e/capture-icon-text-outline.ts` `e2e/support/layoutAssertions.ts` | 様式化 (a) | — | — |
| i18n | 横断 | 1 2 3 4 5 | Web | `src/shared/i18n/` `scripts/` `e2e/tests/system/i18n.spec.ts` `e2e/mock-host/localization/` `e2e/mock-host/topics/dictionaryControl.ts` | — | — | — |
| uiState（画面ルーティング・入力排他） | 横断 | 1 2 3 4 5 | Web | `src/shared/uiState/` `e2e/tests/uiState.spec.ts` `e2e/tests/regression/inputLayer.spec.ts` | 裁定 1=A | — | — |
| uiScale（stage の一様拡縮） | 横断 | 2 5 | Web | `src/shared/uiScale/` `e2e/tests/system/portalLayerScaling.spec.ts` `e2e/captures/portal-scaling.ts` | — | — | — |
| bridge（通信・契約・ストア） | 横断 | 5 | Web | `src/bridge/` | 裁定 3=(a) `HttpListener`（Web ホストの撤去と同じ段階） | — | — |
| アプリ殻（起動・トークン・層） | 横断 | 5 | Web | `src/app/` `src/main.tsx` `src/vite-env.d.ts` `index.html` `e2e/tests/system/textSelection.spec.ts` | — | — | — |
| Web の検証基盤（mock-host・Playwright・撮影） | 横断 | 5 | Web | `e2e/` | — | — | — |

## 追従記録

暗転の起点より後に、暗転中の束の Web パスを触ったコミットの処遇。コミットは 10 桁以上。`捨てた` には理由が要る。

| 束 | コミット | 判定 | 写し先・理由 |
|---|---|---|---|

## 参照キャプチャ

（Task 5 が埋める）

## 未撮影

（Task 5 が埋める）

## agent 既定（ユーザー未裁定）

計画 §0 の残りの裁定は、ユーザーの指示「動く前提で並列で開始して」により、計画の推奨を agent 既定として進めている。ユーザー裁定ではない（`.decisions/` には書かない）。覆ったらここと該当行を直す。

| 項目 | agent 既定 | 帰結 |
|---|---|---|
| 裁定 1 移行中の出荷形 | A: 束単位で暗転のまま作り、所有表で切り替える | 見た目の混在が届く。インベントリ系の CEF 不具合は段階 4 の終わりまで残る |
| 裁定 2 第一弾の画面 | A: 命綱（ポーズ・設定・バグ報告・トースト） | CEF が死んでも Esc でセーブと報告ができる |
| 裁定 3 遠隔実行の待受 | (a) `HttpListener`。配布ビルドで動かなければ (b) 遠隔実行専用の最小 Kestrel | ADR 0072 の改訂が要る |
| 確認 UI の演出の時間源 | 実時間（パネルのスケジューラ） | AGENTS.md の時間の規則はサーバーのゲームロジックの規則と読む |
| 様式化 Mantine の素の見た目の 4 画面 | 未裁定（計画の推奨は (a) 移すときに様式化） | 該当行に「様式化 (a)」と書いた |

裁定 4（Web の扱い）は 2026-10-26 にユーザー裁定済み（PR #1555 の `.decisions/2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md`）。G1 はその帰結の追従ゲート。

## 実測の結論

計画 §4.8 の実測。結論は各レーンが書く（証拠のパス付き）。

| 実測 | 中身 | 担当 | 結論 |
|---|---|---|---|
| S1 | CEF と UITK の前後 | bd `moorestech-albof.1` | 未着手 |
| S2 | 録画とバグ報告動画への写り込み | bd `moorestech-albof.1` | 未着手 |
| S3 | 入力注入 | bd `moorestech-albof.1` | 未着手 |
| S15 | 委譲中の Web の入力停止 | bd `moorestech-albof.1` | 未着手 |
| S9 | 日本語 IME（人手） | bd `moorestech-albof.8` | 未着手 |
