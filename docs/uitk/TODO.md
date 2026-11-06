# UI Toolkit 移行の台帳

計画 `docs/uitk-migration/plan.md` の段階 1〜5（束）と、現行 Web UI の全機能の対応表。移行中に Web へ入った変更の記帳もここで持つ。CI の追従ゲート G1（`.github/workflows/uitk-web-follow-gate.yml`）が「## 束」「## 行」「## 追従記録」の表を読む。

## 読み方と運用

2026-11-06 から、UITK は長命ブランチ `feature/uitk` で全部作り、切替 PR で全束を一度に切り替える（計画 §0・§1.1・§4.7）。

- **この台帳は `feature/uitk` 側で記帳する。master の作業者は触らなくてよい。** G1 は `feature/uitk` への PR と、`feature/uitk` から master への切替 PR だけで走り、master への PR では走らない
- 束 = 計画 §1 の段階 1〜5。状態は `Web`（まだ作っていない）→ `暗転`（`feature/uitk` の上で作っている。作り終えても切替 PR まではこのまま）→ `切替済み`（切替 PR で全束を一度に付ける）
- 束のレーンを始めるとき: その束の `状態` を `暗転` にし、`暗転の起点` に、そのとき `feature/uitk` に取り込んである最新の master のコミット（10 桁）を書く
- master を `feature/uitk` へ取り込むたびに、その回に入った暗転中の束の Web パスを触るコミットを「## 追従記録」に `写した`（UITK 側の写し先）か `捨てた`（理由）で書く。G1 は暗転中の束ごとに未記帳の件数を出すが、赤にはしない。取り込みの PR は件数が 0 であることを見てからマージする。追従（`写した` の実装）は未移植の画面の実装より先にする
- 切替 PR の前に、`feature/uitk` 上で Web を触ったコミット（休止の撤去・撤去 5c の削除・取り込み以外の Web 変更）も各束の「## 追従記録」に `捨てた（理由）` で記帳し、`.github/scripts/check-uitk-web-follow.py --base origin/master --head feature/uitk` をローカルで回して未記帳 0 件を見る。切替 PR は head=`feature/uitk` から出す（中継ブランチ禁止）
- 切替 PR: 全束の `状態` を `切替済み`、全行の `所有者` を `UITK` にする（切替は全束一度）。G1 は、各束の起点より後にその束の Web パスを触った非マージコミットが全部記帳されていなければ落とす
- 束を切替 PR より前に `切替済み` にしない。切替済みの束の Web パスへの追加・変更は G1 が落とすので、master の取り込みで入る Web の変更まで落ち、Web を凍結したのと同じになる
- `Web パス` は `moorestech_web/webui/` からの相対。末尾 `/` はディレクトリ、それ以外はファイル。ファイルの持ち主は最長一致の行（同じ長さなら全部）
- `moorestech_web/webui/src/` の下のファイルはどれかの行に入っていなければならない（G1 が落とす）。master の取り込みで新しい Web のファイルが入ったら、その取り込みの PR でここに行を足すか既存の行のパスに入れる
- 取り込みで `e2e/` の下に新しい spec・fixture・撮影スクリプトが入ったら、その画面の行の `Web パス` に足す。足さないと受け皿の「Web の検証基盤」（束 5）の持ち物になり、その束の切替で記帳を求められない（G1 は `e2e/` の網羅を検査しない）
- セルに `|` を書かない。空欄は `—`
- 全体スイッチ（計画 §3.3 R1〜R4）で描く系を選ぶ: PlayMode を止めた状態で Unity のメニュー `moorestech/UI System/GameUi (UITK)`（戻すときは `moorestech/UI System/Web`）を選ぶか、`uloop execute-dynamic-code --project-path ./moorestech_client --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi);'` を実行してから PlayMode に入る。値は起動ごとに 1 回だけ読まれ、Editor を閉じると既定の Web に戻る。GameUi の起動では Web ホストと CEF を起動せず、UITK にまだ無い画面は使えない（裁定）
- 束を切り替える前（その段の ladder）に `node tools/uitk/test-name-parity.mjs --bundle <束>` で Web テスト名の差分 0 を見る。書式と処遇の表は `docs/uitk/disposition.md`

## 束

| 束 | 中身 | 状態 | 暗転の起点 | 物差し |
|---|---|---|---|---|
| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 暗転 | 979725bcd1 | `uitk-lifeline` |
| 2 | 常駐表示（入力の無い HUD・通知・ツールチップ・ワールドピン・チュートリアル枠・スキット） | Web | — | `uitk-opening-hud` |
| 3 | 建築の束（ビルドメニュー・ホットバー・BP 名入力） | Web | — | `uitk-build-bp` |
| 4 | 持ち物の束（インベントリ・装備・掴み・レシピ・ブロック UI 全族・研究） | Web | — | `uitk-production-loop`・`uitk-research-train` |
| 5 | 最後の束と撤去（チャレンジ一覧・言語ゲート・チュートリアル枠・Web の横断層） | Web | — | `uitk-challenge-gate` |

## 行

所有者の列は切替 PR で全行 `UITK` に付け替える列（今は全部 Web）。`agent 既定` は行に効く agent 既定（全体は「## agent 既定（ユーザー未裁定）」）、`実測の結論` は行に効く実測（全体は「## 実測の結論」）。

| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |
|---|---|---|---|---|---|---|---|
| pauseMenu（ポーズ・バグ報告・入れ子のポーズ） | 機能 | 1 | Web | `src/features/pauseMenu/` `e2e/tests/system/pauseMenu.spec.ts` `e2e/tests/system/pauseMenuSettingsLayout.spec.ts` `e2e/tests/system/feedbackReportLabels.spec.ts` `e2e/mock-host/pauseMenuActions.ts` `e2e/mock-host/topics/pauseMenuControls.ts` | 裁定 2=A（第一弾は命綱） | UITK のポーズ（PlayMode・ScreenCapture・フレーム番号と時刻の透かし入り）: トップ 1280×720 https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-2026-11-06-uitk-screens-fix-pause-top-720p/fix-pause-top-720p.png 、2560×1080 https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-2026-11-06-uitk-screens-fix-pause-top-2560x1080/fix-pause-top-2560x1080.png 、バグ報告 1280×720 https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-2026-11-06-uitk-screens-fix-pause-bug-report-720p/fix-pause-bug-report-720p.png 、2560×1080 https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-2026-11-06-uitk-screens-fix-pause-bug-report-2560x1080/fix-pause-bug-report-2560x1080.png 。面の決め方は `translucent-tokens.md` | 1-pause-top 1-pause-disconnected 1-pause-delivery-pending 1-pause-in-skit 1-pause-in-train 1-bug-report |
| settings（画質・言語・ダッシュ方式） | 機能 | 1 | Web | `src/features/settings/` | 裁定 2=A | — | 1-settings 1-settings-graphics-recovered |
| toast | 機能 | 1 | Web | `src/features/toast/` `e2e/tests/regression/actionErrors.spec.ts` | 裁定 2=A。見た目は Web 版と同じ（2026-11-06 裁定） | — | 1-toast |
| commonHud（クロスヘア） | 機能 | 2 | Web | `src/features/commonHud/` `e2e/tests/system/commonHud.spec.ts` | — | — | 2-game-hud |
| keyHint | 機能 | 2 | Web | `src/features/keyHint/` | — | — | 未撮影 |
| modeHud（配置 HUD・削除モードの帯） | 機能 | 2 | Web | `src/features/modeHud/` `e2e/tests/modeHud/` `e2e/support/operationHudAssertions.ts` | — | — | 2-placement 2-placement-unavailable 2-placement-connect-tool 2-delete-bar |
| progress（採掘ゲージ） | 機能 | 2 | Web | `src/features/progress/` `e2e/tests/progress.spec.ts` `e2e/capture-mining-progress.ts` | — | — | 2-mining 2-progress-labeled |
| trainHud（乗車中の分岐表示） | 機能 | 2 | Web | `src/features/trainHud/` | 見た目は Web 版と同じ（2026-11-06 裁定） | — | 2-train-hud |
| notification（獲得通知） | 機能 | 2 | Web | `src/features/notification/` `e2e/tests/notification/` `e2e/captures/capture-connection-line-notifications.ts` | 確認=演出は実時間 | — | 2-notifications 動画 2-notification-enter-aggregate-exit |
| skit（会話窓・選択肢・ツールバー・暗転） | 機能 | 2 | Web | `src/features/skit/` `src/app/skitTokens.css` `e2e/tests/skit.spec.ts` `e2e/mock-host/skitActions.ts` `e2e/capture-skit.ts` | 確認=演出は実時間 | — | 2-skit-text 2-skit-choices 2-skit-transition |
| challenge: CurrentChallengeHud | 機能 | 2 | Web | `src/features/challenge/CurrentChallengeHud.tsx` `src/features/challenge/CurrentChallengeHud.module.css` `e2e/tests/challenge/` `e2e/challenge-hud/` `e2e/support/challengeHudAssertions.ts` | — | — | 2-challenge-hud-multiple-long |
| tutorial: KeyControlHintHud | 機能 | 2 | Web | `src/features/tutorial/KeyControlHintHud.tsx` `src/features/tutorial/KeyControlHintHud.test.ts` `src/features/tutorial/keyControlHint.module.css` `src/features/tutorial/tutorialElement.ts` `src/features/tutorial/tutorialAttentionDesign.test.ts` `e2e/mock-host/topics/tutorialControls.ts` `src/features/tutorial/index.ts` `e2e/tests/system/tutorial.spec.ts` | 確認=演出は実時間 | — | 2-key-control-hint 動画 2-key-control-hint-pulse |
| tutorial: WorldPinOverlay | 機能 | 2 | Web | `src/features/tutorial/WorldPinOverlay.tsx` `src/features/tutorial/worldPin.module.css` `e2e/tests/system/worldPin.spec.ts` `e2e/capture-world-pin-arrow.ts` | 確認=演出は実時間 | — | 2-world-pin-onscreen 2-world-pin-offscreen 動画 2-world-pin-pulse |
| CursorTooltip | 共通部品 | 2 | Web | `src/shared/tooltip/` | — | — | 2-tooltip 2-tooltip-multiline |
| オーバーレイ: 背景スキット | オーバーレイ | 2 | Web | `src/app/App.tsx` `e2e/tests/phaseC4.spec.ts` | 前回は状態機械だけで洗い出して見落とした（`docs/uitk-migration/research/understand-shipping.md` §6.2） | — | 2-skit-background |
| オーバーレイ: Ctrl+U の全体非表示 | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | 未撮影（下表） |
| オーバーレイ: カットシーン中の退避 | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | 未撮影（下表） |
| オーバーレイ: 背面の通知層（stage の裏） | オーバーレイ | 2 | Web | `src/app/App.tsx` | — | — | 未撮影 |
| オーバーレイ: カーソル追従（ツールチップ・掴みの絵） | オーバーレイ | 2 4 | Web | `src/app/App.tsx` | — | — | 未撮影 |
| オーバーレイ: 常駐 HUD の可否（ホットバー・装備） | オーバーレイ | 3 4 | Web | `src/app/App.tsx` | — | — | 未撮影 |
| オーバーレイ: 画面のマウント点と背景ディム | オーバーレイ | 1 2 3 4 5 | Web | `src/app/App.tsx` `src/app/App.module.css` `src/app/App.architecture.test.ts` | 一気切替（2026-11-06）。所有表は使わない | — | 未撮影（下表） |
| オーバーレイ: 再接続・辞書エラー・エラー境界 | オーバーレイ | 5 | Web | `src/app/App.tsx` `src/app/AppErrorBoundary.tsx` `e2e/tests/regression/connection.spec.ts` | — | — | 5-reconnect |
| buildMenu | 機能 | 3 | Web | `src/features/buildMenu/` `e2e/tests/regression/buildMenu.spec.ts` `e2e/tests/regression/buildMenuLayout.spec.ts` `e2e/capture-buildmenu.ts` `e2e/mock-host/fixtures/buildMenuFixtures.ts` | — | — | 3-build-menu |
| hotbar | 機能 | 3 | Web | `src/features/hotbar/` `e2e/tests/hotbar/` `e2e/mock-host/fixtures/hotbarFixtures.ts` | — | — | 3-hotbar-equipment-empty |
| modal（BP 名入力） | 機能 | 3 | Web | `src/features/modal/` `e2e/tests/modal.spec.ts` | — | — | 3-modal |
| placementTarget（配置対象の表示名） | 共通部品 | 2 3 4 | Web | `src/shared/placementTarget/` | — | — | 未撮影 |
| pointerGesture（押下・ドラッグの判定） | 共通部品 | 3 4 5 | Web | `src/shared/pointerGesture/` | — | — | 未撮影 |
| inventory（インベントリ・装備・掴み） | 機能 | 4 | Web | `src/features/inventory/` `e2e/tests/inventory/` `e2e/mock-host/inventoryModel.ts` | 一気切替（2026-11-06）。所有表は使わない | — | 4-player-inventory 4-player-inventory-full |
| recipe（レシピ・アイテム一覧・長押しクラフト） | 機能 | 4 | Web | `src/features/recipe/` `e2e/tests/recipe/` `e2e/mock-host/fixtures/recipeFixtures.ts` `e2e/craft-chrome/` `e2e/support/craftChromeAssertions.ts` | 確認=演出は実時間 | — | 4-item-list-large |
| blockInventory（汎用族・特殊族・列車族） | 機能 | 4 | Web | `src/features/blockInventory/` `e2e/tests/block/` `e2e/tests/train/` `e2e/tests/train.spec.ts` `e2e/tests/regression/sectionStack.spec.ts` `e2e/mock-host/fixtures/blocks/` `e2e/mock-host/blockDetailFixtures.ts` `e2e/mock-host/detailActions.ts` `e2e/mock-host/detailActions.test.ts` `e2e/mock-host/train/` `e2e/mock-host/fixtures/trainInventoryFixtures.ts` `e2e/capture-machine-qa.ts` `e2e/capture-train-timetable.ts` `e2e/fixtures/` | — | — | 4-block-chest 4-block-tank 4-block-machine 4-block-gearMachine 4-block-generator 4-block-miner 4-block-filterSplitter 4-block-gearMiner 4-block-pump 4-block-pumpNoVein 4-block-gearPump 4-block-generic 4-block-gearGenerator 4-block-electricToGear 4-block-train 4-block-trainTimetable 4-block-trainError 4-block-trainPlatform 4-block-trainFluidPlatform 4-block-electricPole 4-block-train-timetable-tab 4-block-machine-recipe-selection |
| blockInventory 特殊族: ロケット（Tier9 #1551） | 機能 | 4 | Web | `src/features/blockInventory/details/rocket/` | — | — | 未撮影（下表） |
| blockInventory 特殊族: 宇宙エレベーター（Tier9 #1552） | 機能 | 4 | Web | `src/features/blockInventory/details/spaceElevator/` | — | — | 未撮影（下表） |
| research（研究ツリー・詳細ペイン） | 機能 | 4 | Web | `src/features/research/` `e2e/tests/research/` `e2e/mock-host/researchFixtures.ts` `e2e/capture-research-qa.ts` `e2e/support/panSettle.ts` | — | — | 4-research |
| tutorial: DragGuide（インベントリ内） | 機能 | 4 | Web | `src/features/tutorial/overlay/DragGuide.tsx` `src/features/tutorial/overlay/style.module.css` `src/features/tutorial/overlay/TutorialOverlay.tsx` `src/features/tutorial/overlay/TutorialOverlay.test.ts` `src/features/tutorial/tutorialElement.ts` `src/features/tutorial/tutorialAttentionDesign.test.ts` `e2e/mock-host/topics/tutorialControls.ts` `src/features/tutorial/index.ts` | 確認=演出は実時間 | — | 動画 4-drag-guide |
| itemMove（掴みの計画） | 共通部品 | 4 | Web | `src/shared/itemMove/` `e2e/tests/regression/itemSlot.spec.ts` | — | — | 未撮影 |
| ownedCounts | 共通部品 | 4 | Web | `src/shared/ownedCounts.ts` `src/shared/ownedCounts.test.ts` | — | — | 未撮影 |
| treeView（パン・ズーム・線） | 共通部品 | 4 5 | Web | `src/shared/treeView/` | — | — | 未撮影 |
| challenge: ChallengePanel | 機能 | 5 | Web | `src/features/challenge/` `e2e/tests/challenge.spec.ts` | 見た目は Web 版と同じ（2026-11-06 裁定） | — | 5-challenge-list |
| eventLanguageGate（出展モードの言語選択） | 機能 | 5 | Web | `src/features/eventLanguageGate/` | — | — | 5-event-language-gate |
| tutorial: TutorialOverlay（ハイライト枠） | 機能 | 5 | Web | `src/features/tutorial/` `e2e/tests/system/tutorial.spec.ts` `e2e/tests/system/tutorialHighlightClip.spec.ts` `e2e/support/pulseFreeze.ts` `e2e/mock-host/topics/tutorialControls.ts` `src/features/tutorial/overlay/style.module.css` `src/features/tutorial/overlay/TutorialOverlay.tsx` `src/features/tutorial/overlay/TutorialOverlay.test.ts` `src/features/tutorial/tutorialElement.ts` `src/features/tutorial/tutorialAttentionDesign.test.ts` `src/features/tutorial/index.ts` | 確認=演出は実時間 | — | 5-tutorial-outline-label 5-tutorial-recipe-item 5-tutorial-research-node 動画 5-tutorial-pulse |
| tutorialAnchor（アンカーの登録と解決） | 共通部品 | 1 2 3 4 5 | Web | `src/shared/tutorialAnchor/` | — | — | 未撮影 |
| shared/ui（枠・スロット・ボタン・ゲージ） | 共通部品 | 1 2 3 4 5 | Web | `src/shared/ui/` `src/shared/clamp01.ts` `src/shared/clamp01.test.ts` `src/shared/materialTooltipText.ts` `src/app/slotTokens.css` `src/app/iconTextOutlineDesign.test.ts` `e2e/capture-iconbutton.ts` `e2e/capture-icon-text-outline.ts` `e2e/support/layoutAssertions.ts` | 見た目は Web 版と同じ（2026-11-06 裁定） | — | 未撮影 |
| i18n | 横断 | 1 2 3 4 5 | Web | `src/shared/i18n/` `scripts/` `e2e/tests/system/i18n.spec.ts` `e2e/mock-host/localization/` `e2e/mock-host/topics/dictionaryControl.ts` | — | — | 5-japanese-inventory |
| uiState（画面ルーティング・入力排他） | 横断 | 1 2 3 4 5 | Web | `src/shared/uiState/` `e2e/tests/uiState.spec.ts` `e2e/tests/regression/inputLayer.spec.ts` | 一気切替（2026-11-06）。所有表は使わない | — | 未撮影 |
| uiScale（stage の一様拡縮） | 横断 | 2 5 | Web | `src/shared/uiScale/` `e2e/tests/system/portalLayerScaling.spec.ts` `e2e/captures/portal-scaling.ts` | — | — | 未撮影 |
| bridge（通信・契約・ストア） | 横断 | 5 | Web | `src/bridge/` | 裁定 3=(a) `HttpListener`（Web ホストの撤去と同じ段階） | — | 未撮影 |
| アプリ殻（起動・トークン・層） | 横断 | 5 | Web | `src/app/` `src/main.tsx` `src/vite-env.d.ts` `index.html` `e2e/tests/system/textSelection.spec.ts` | — | — | 未撮影 |
| Web の検証基盤（mock-host・Playwright・撮影） | 横断 | 5 | Web | `e2e/` | — | — | 未撮影 |
## 追従記録

暗転の起点より後に、暗転中の束の Web パスを触ったコミットの処遇。コミットは 10 桁以上。`捨てた` には理由が要る。

| 束 | コミット | 判定 | 写し先・理由 |
|---|---|---|---|
| 1 | a97575e5b5 | 捨てた | 旧 R1 の Web 全面 unmount（UITK が主パネルを出す間 webUiSuspended で App を外す）。全体スイッチ（albof.17）で UITK 側は Web ホストを起動しなくなり休止の仕組みごと要らないので、W0-switch で外して master と同じ Web に戻した |
| 1 | 05b9bdefa8 | 捨てた | Web が休止・カットシーンから描画を再開するときの stage 倍率の測り直しで、Web 自身の描画再開にだけ要る。UITK 側に写すものが無い |
| 1 | 1095cab8bf | 捨てた | 旧 R1 の休止解除時の入力状態の送り直し。全体スイッチで休止が無くなり要らないので、W0-switch で外した |
| 1 | ccb3d512f7 | 捨てた | 旧 R1 の休止解除後に DOM 変化ごとにポインタを当たり判定し直す処理。全体スイッチで休止が無くなり要らないので、W0-switch で外した |
| 1 | 3e946e4bd3 | 写した | D-8 の拒否理由ごとのトースト文言。Web 側は Localization/localization.csv への追加に連動した生成物 src/shared/i18n/generated/localizationKeys.ts の再生成だけで、写し先は同じコミットの C# UiCommandRejectReasonTraits.ToLocalizationKey と localization.csv の文言 |
| 1 | 5421c7f9f0 | 捨てた | 旧 R1（画面別の Web 休止）の入力排他フックを src/shared/uiState/inputExclusivity/ へ移した自動修正。全体スイッチの作り直し（W0-switch・albof.17）で旧 R1 の Web 側変更ごと外して master と同じ Web に戻すので、UITK 側に写すものが無い |
| 1 | f044f5f5c4 | 捨てた | W0-switch（albof.17）の撤去コミット。旧 R1 の Web 側変更（a97575e5b5・05b9bdefa8・1095cab8bf・ccb3d512f7・5421c7f9f0）を外し、取り込み済み master と同じ Web に戻した（生成物 localizationKeys.ts は 3e946e4bd3 のまま）。UITK 側に写すものが無い |

## 参照キャプチャ

Web 版の全画面と主要な状態（移行後も「意図した見た目」を辿るため）。mock-host（DEMO の高密度データとプレースホルダアイコン）で撮った。画素の一致は目標にしない（計画 §4.6）。

- 撮影元: moorestech `a9d5123537`。撮り方: `moorestech_web/webui/e2e/uitk-reference/capture.ts`（撮影表は同じディレクトリの `referenceShots.ts`・`referenceVideos.ts`）
- 置き場: `moorestech_logs` の `uitk/reference-captures/web-a9d5123537/`（一覧は `index.md`、機械向けは `manifest.json`）
- URL: https://github.com/moorestech/moorestech_logs/tree/main/uitk/reference-captures/web-a9d5123537
- 目視検査: 同じ置き場の `visual-review.md` と `qa/` に全64静止画・全5動画の確認とフレーム証拠を残す。通知動画は入場・集約・全退出を含む12秒、ほかは3〜4秒
- 各行の `参照キャプチャ` 列は撮影 id（PNG は `<id>.png`、動画は `<id>.webm`）。束を切り替える前に撮り直したら、ここの撮影元と置き場を新しい方へ書き換える

## 未撮影

mock-host では撮れず、PlayMode（unityプレイ録画テスト）でしか撮れない状態。各束の暗転を始めるときに、その束の分を撮って上の置き場に足す。

| 束 | 行 | 状態 | 撮れない理由 |
|---|---|---|---|
| 1〜5 | 全行 | 実ゲームの 3D 画面に CEF が合成された見え方 | mock-host は Web 単体で、合成と透過が無い |
| 1〜5 | 全行 | 実マスタのアイコン・名前・研究ノード | DEMO はプレースホルダのアイコンと合成データ |
| 2 | オーバーレイ: Ctrl+U の全体非表示 | Ctrl+U 中に 3D だけが映ること | Web 側は全部 unmount するだけで絵が無い |
| 2 | オーバーレイ: カットシーン中の退避 | カットシーン中に UI が消えること | 同上 |
| 2 | tutorial: WorldPinOverlay | 動く対象への追従・画面外矢印の回転 | mock は固定座標 |
| 2 | skit（会話窓・選択肢・ツールバー・暗転） | 実キャラクターの背景とタイプライターの動き | mock は固定文と固定背景 |
| 2 | trainHud（乗車中の分岐表示） | 実走行中の分岐表示 | 乗車は PlayMode でしか起きない |
| 3 | buildMenu | ビルドメニューからホットバーへのドラッグの最中 | ドラッグ中の合成と配置の実挙動 |
| 4 | inventory（インベントリ・装備・掴み） | 掴み・分割ドラッグ・右ドラッグ配りの最中、ホイールでの装備選択 | ゲーム側の入力と実インベントリ |
| 4 | recipe（レシピ・アイテム一覧・長押しクラフト） | 長押しクラフトの進捗の動き | 実クラフトの進行 |
| 4 | research（研究ツリー・詳細ペイン） | パン・ズームの慣性の動き | 動画は mock でも撮れるが、実ノード 7 件のアンカーと合わせて PlayMode で撮る |
| 4 | blockInventory 特殊族: ロケット（Tier9 #1551） | 全状態 | 撮影時点の master に画面が無い（#1551 のマージ後に撮る） |
| 4 | blockInventory 特殊族: 宇宙エレベーター（Tier9 #1552） | 全状態 | 同上（#1552） |
| 4 | blockInventory（汎用族・特殊族・列車族） | 機械 UI を開いたまま中身が更新される動き | 実機械の稼働 |
| 1〜5 | i18n | IME の変換中の入力欄（駅名・BP 名・検索・バグ報告） | 人手の確認（S9） |
| 1〜5 | オーバーレイ: 画面のマウント点と背景ディム | CEF が固まって最後のフレームが残る状態 | CEF の実プロセスが要る |

## agent 既定（ユーザー未裁定）

計画 §0 の残りの裁定は、ユーザーの指示「動く前提で並列で開始して」により、計画の推奨を agent 既定として進めている。ユーザー裁定ではない（`.decisions/` には書かない）。覆ったらここと該当行を直す。

| 項目 | agent 既定 | 帰結 |
|---|---|---|
| 裁定 1 移行中の出荷形 | 2026-11-06 に覆った → 長命ブランチで一気切替（`.decisions/2026-11-06-UITK移行は長命ブランチで作り一気に切り替える.md`） | 切替の日までテスターに届かない |
| 裁定 2 第一弾の画面 | A: 命綱（ポーズ・設定・バグ報告・トースト） | CEF が死んでも Esc でセーブと報告ができる |
| 裁定 3 遠隔実行の待受 | (a) `HttpListener`。配布ビルドで動かなければ (b) 遠隔実行専用の最小 Kestrel | ADR 0072 の改訂が要る |
| 確認 UI の演出の時間源 | 実時間（パネルのスケジューラ） | AGENTS.md の時間の規則はサーバーのゲームロジックの規則と読む |
| 通信失敗のトースト | 全体スイッチの UITK 側では Web ホストも WS も起動しないので、Web の通信失敗のトーストは出ない。UITK のコマンドは C# を直接呼び、拒否は UiCommandResultToaster.Report が唯一の口として拒否理由ごとのローカライズ文言で出す。Web 側は master と同じ Web の toast がそのまま出す | W0-switch で確かめた: Client.Ui と Client.Ui.State の asmdef が Client.WebUiHost を参照しない（grep 0 件）、L3 PauseMenuGameUiTest と GameUiSkeletonTest で UITK 側の WebUiHost.Hub が null |
| 様式化 Mantine の素の見た目の 4 画面 | 2026-11-06 ユーザー裁定: Web 版と同じ見た目（様式化しない） | 該当行に「見た目は Web 版と同じ」と書いた |

裁定 4（Web の扱い）は 2026-10-26 にユーザー裁定済み（PR #1555 の `.decisions/2026-10-26-UITKが安定するまでWeb-UIの変更を続け現行機能をすべてUITKへ移す.md`）。G1 はその帰結の追従ゲート。

## 実測の結論

計画 §4.8 の実測。結論は各レーンが書く（証拠のパス付き）。

| 実測 | 中身 | 担当 | 結論 |
|---|---|---|---|
| S1 | CEF と UITK の前後 | bd `moorestech-albof.1` | 合格。PanelSettings.sortingOrder で前後を決められ、背面パネルも置ける（-1 で背面、1 以上で前面、同値 0 は UITK が前）。PR #1558・`docs/uitk-migration/poc/stage0-measurements/` |
| S2 | 録画とバグ報告動画への写り込み | bd `moorestech-albof.1` | 合格。Recorder・ScreenCapture・ScreenFrameReader のいずれにも UITK と CEF が写る。同上 |
| S3 | 入力注入 | bd `moorestech-albof.1` | 合格。注入クリックが InputSystemUIInputModule→PanelEventHandler で UITK に届き座標往復が一致、IsPointerOverGameObject も UITK を拾う。同上 |
| S15 | 委譲中の Web の入力停止 | bd `moorestech-albof.1` | 不可。action は止まるが Web の pointerOverUi が true のまま固着する。UITK が主パネルかモーダルを出すあいだ Web を全面オフにする（R1） |
| S9 | 日本語 IME（OS 段の合成キー） | bd `moorestech-albof.8` | 成立（CEF あり／なし・単行／複数行）。確定後の ESC・IME モードの取り合い・候補窓の位置は段階 1 の要件（bd `moorestech-albof.12`）。https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-10-28-uitk-s9-ime-result/ |
| S7 | Linear 色空間の半透明 3 案と 10〜13px の文字（720p・1080p・4K） | theme レーン（bd `moorestech-albof.16`） | 既定どおり (a)+(b)。半透明: 背景 #9c7b42 に黒 α0.6 は UITK 既定で #664f29（Chrome #3e311a）。α を 1-(1-α)^2.2 に補正（α0.867）すると #3b2d15 で Chrome とほぼ一致、navy α0.8→0.956 も #241e1e（Chrome #272423）。(c) PanelSettings の forceGammaRendering は sRGB 合成で完全一致（復号値 #3e311a）だが targetTexture 経由の合成と入力座標の付け替えが要るので採らない。Tokens.uss の半透明は (b) の式で初期値を入れ、部品の段で実機で決める（(a)）。文字: 720p で 10〜12px は和文が潰れ 13px から判読、1080p・4K は全サイズ判読。12px 以下を 13px へ上げる既定を維持（アイコン上の個数は除く）。https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-05-2026-11-05-uitk-s7-translucency-text/ |
| S10 | TextCore の SDF FontAsset を Editor で作ってコミットできるか | theme レーン（bd `moorestech-albof.16`） | 成立。`FontAsset.CreateFontAsset(NotoSansJP-Medium.otf, 90, 9, SDFAA, 1024, 1024, Dynamic, true)` → `AssetDatabase.CreateAsset` → atlas と material を `AddObjectToAsset` → 再読込で sourceFontFile・atlas・material が残り、`TryAddCharacters("電力・鉄鉱石の粉")` が成功（chars=8、2026-11-05 uloop execute-dynamic-code の戻り値）。Dynamic の字形追加で Editor 上の資産が汚れるので `clearDynamicDataOnBuild` を立て、コミット前に `ClearFontAssetData` で空にする。配布ビルドでの描画は未確認 |
| customStyle | C# が `CustomStyleProperty` でトークンを読めるか（骨組みの持ち越し） | theme レーン（bd `moorestech-albof.16`） | 条件付きで成立。`:root` の変数は当てた要素自身の customStyle でだけ読め、子へは継承されない（子の `--panel-edge-fade` は False、ルートの `--surface-navy` は True）。部品の USS で `--x: var(--token)` と受け直すと解決後の値が読める（色は `Color` で True）。px・ms 付きの値は `CustomStyleProperty<float>` では読めず（False）、`string` で読める（"12px"・"160ms"）。単位なしの数は float で読める。よって部品 USS で受け直し、長さ・時間は文字列で読んで解釈する（UiTestHost の EditMode プローブ、2026-11-05） |
| S4 | UI 専用アクション資産でのナビゲーション遮断 | 段階1 input レーン | 合格。MainGame の `InputSystemUIInputModule` を `Assets/Asset/Common/MoorestechUiInputActions.inputactions`（UI マップの Point・Click・RightClick・MiddleClick・ScrollWheel・TrackedDevicePosition・TrackedDeviceOrientation のみ）へ差し替え、move・submit・cancel は null。入力欄フォーカス中の注入（QueueStateEvent）で数えたルート規則より前の件数は、差し替え前（既定の資産）が W・A・S・D・矢印 4 つで move=1、Enter で submit=1、Space は 0。差し替え後は全 10 キーで move=0・submit=0。規則を越えて要素へ届いた件数は前後とも 0。MainGame の uGUI Selectable は HP バーの Slider 1200 個（全て非アクティブ・Navigation=Automatic）だけで、選択移動を使うコード（SetSelectedGameObject・Select）は無い。タイトル（MainMenu）は別の EventSystem（StandaloneInputModule）で変更なし。証拠: TestResults の `GameUiInputTest.UI資産の差し替え前後でWASDと矢印とSpaceとEnterがナビゲーションイベントを出すかを数える`（Passed）のログ `[S4] phase=before summary W:move=1,submit=0 … Space:move=0,submit=0 Enter:move=0,submit=1`・`[S4] phase=after summary W:move=0,submit=0 … Enter:move=0,submit=0` |
| S5 | TextField の ESC と UIStateControl の先後 | 段階1 input レーン | 先後は決まっていない。`PanelEventHandler` と `UIStateControl` はどちらも実行順 0（`MonoImporter.GetExecutionOrder`）で `DefaultExecutionOrder` 属性も無い。1 回の PlayMode では 34 フレームとも PanelEventHandler.Update → UIStateControl.Update の順だった（一時計測。コミットしない）が、同順位の Update の先後は保証されない。よって R-4 の両順で閉じない設計で受ける（先に UITK が処理すればラッチ、先に UIStateControl が処理すれば入力欄フォーカス中の Keyboard 抑止）。ESC そのものの先後は注入が IMGUI のキューに入らないため L3 では測れず、OS 段の合成キーで Task 7 が確かめる |
| S13 | UI Test Framework の L1 が CI の batchmode で動くか | 段階1 input レーン | 合格。CI run https://github.com/moorestech/moorestech/actions/runs/42044060591（workflow_dispatch・ref feature/uitk-s1-input・head a0798b66fa）。shard client-remainder の editmode-results.xml で `Client.Tests.Ui.Motion.PanelTimeAdvanceTest`（OpacityTransitionIsMidwayAt100msAndDoneAfterDuration・ScheduledItemFiresOnlyAfterItsDelay）と `Client.Tests.Ui.Layers.UiLayerStackTest` 3 件が Passed（transition と schedule が batchmode で進む）。shard client-play-1 では `GameUiInputTest` 2 件が Passed、`GameUiSkeletonTest` が「対照の要素の上で IsPointerOverGameObject が false」で Failed。背面・batchmode では Mouse が無効化され QueueStateEvent が捨てられる環境要因で、ローカルでも同じ形で再現し入力設定の切替で通ることを確認。`EditorBackgroundInputScope` を当てて修正し、ローカルで Passed（CI での再実行はしていない） |
| S9 段階1 | GameTextField の IME（Mac の Editor・osascript の key code） | 段階1 input レーン | 合格（Mac の Editor）。PlayMode の MainGame で Screen 層に単行・複数行の `GameTextField` を置き、日本語入力で確かめた。(1) 確定後の ESC: 値が残りフォーカスが外れ、再フォーカスもポーズも無い（単行・複数行）。(2) 変換中の ESC: Mac では Escape の KeyDown が Unity に届かず、OS が変換だけを取り消す。フォーカス後に確定した文字は残る（巻き戻らない）。フォーカスは保たれ、ポーズに入らない。(3) 下線: 変換中だけ出て、右端はキャレットと 1px 以内で一致する（1 打遅れは無い）。(4) 候補窓: 修正前は上下が反転して出た（S9 と同じ）。Mac は `Input.compositionCursorPos` の y を下原点で読むため、`ImeCaretScreenPosition` に原点の引数を足して Mac だけ反転させた。修正後は単行・複数行ともキャレット行のすぐ下に出る。GameTextField が書いた値が UITK の値より後に効いていることも、これで確かめられた。(5) クリックで全選択しない（キャレットが末尾に置かれ、打った文字は後ろに足される）。(6) IME モード: 欄の外では Off で、日本語入力のまま打っても素の文字になる。フォーカスが外れたフレームだけ Auto で、次のフレームで Off に戻る。実機で見つけた不具合: 下線要素を TextElement の子にしていたため文字の計測が止まり、単行欄の文字が見えなかった。下線を兄弟要素へ移して直し、L1 を足した。未確認: Editor の Game View はパネルとスクリーンがどちらも 1280×720 なので、倍率の換算（Retina・全画面のスタンドアロン）は確かめていない。Windows は検証機の配布ビルドが要るので、screens レーンのバグ報告画面の確認へ回す。Web の欄の日本語（D-10）も同じく screens レーンで確かめる。https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-uitk-s1-input-ime/ |
| uitk-lifeline | 束 1 の物差し（Web ホストを起動しない状態で Esc→設定→バグ報告→セーブ→セーブして終了） | 段階1 screens レーン | 合格。`scenarios/uitk/uitk-lifeline.cs`（受け口は起動前に `MOORESTECH_PLAYTEST_RECEIVER_BASE=http://127.0.0.1:9` へ差し替え）。日本語へ切替・日本語複数行の本文で送信→失敗トースト「欠けている項目: video, serverSnapshot」・書かれた箱の manifest は schemaVersion 6 で `webUiHealth` = hostStartup `skippedByEditorSwitch`・cefProcess `running`・secondsSinceLastWebMessage `null`。送信ログは「0 box(es) sent」。https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-uitk-s1-screens-evidence/uitk-lifeline/ |
| R-13 | 入れ子ポーズ（スキット・列車 HUD）を UITK のポーズで開いて閉じる | 段階1 screens レーン | 合格（2 経路の録画）。スキット: Esc→入れ子ポーズ→設定→戻る→Esc で閉じ、外側は Story のまま・閉じた後 pause-menu は見えない（`uitk-nested-pause-skit.cs`）。列車: 乗車→Esc→入れ子ポーズ→設定→戻る→閉じて列車 HUD・乗車のまま（`uitk-nested-pause-train.cs`）。ADR 0035 どおりポーズ中もスキットは背後で流れ続ける。開幕チャレンジが続けて流す背景スキットの終了（`SkitPresentationStateStore.End`）が、シナリオから直接起動したスキットの送り待ちを取り消して終わらせるので、シナリオは背景スキットの終了を待ってから起動する。https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-uitk-s1-screens-evidence/ |
| R-14 | R1（Web 全面オフ）中に Web が action を出さないか | 段階1 screens レーン | 合格。CEF ありで UITK のポーズを開き、Web のポーズのボタン位置 5 か所のクリック・文字キー 6 打・設定ページの Esc を注入して、Web から届いた action を数えた（一時計測の `[R14-MEASURE]` 行。計測後に外した）。ポーズ中の利用者由来の action は 0 件（DSL の位置問い合わせ応答 `playtest.*` は除外）。閉じた 2 秒後も pointerOverWeb=False・textFocused=False。`uitk-pause-web-action-count.cs`。cef-unity の改修は不要 |
| R-16 | 段階1 screens の L1 が CI で通るか | 段階1 screens レーン | 合格。CI run https://github.com/moorestech/moorestech/actions/runs/42109885552（workflow_dispatch・ref feature/uitk-s1-screens・head 3d7dd7b5c6・全 shard success）。client-remainder で `Client.Tests.Ui.Screens.PauseMenu.PauseMenuViewTest` 10・`Settings.SettingsViewTest` 10・`BugReport.BugReportFormViewTest` 10・`Toast.ToastHostViewTest` 7・`Ui.Input.UiInputExclusivityWebSilenceTest` 4 が Passed。client-play-1 で L3 `PauseMenuGameUiTest` が Passed |
| R-15 | バグ報告の本文欄（GameTextField 複数行）の日本語 IME（Windows の配布ビルド・Mac の Editor）と CEF ありの Web の欄 | 段階1 screens レーン | 一部合格・一部測れず。Windows 検証機（CI の開発ビルド 3d7dd7b5c6・SendInput の VK+スキャンコード・Microsoft IME）: ウィンドウ 1280×720 と全画面 1920×1080（パネル 1280×720 の 1.5 倍）の両方で、変換中の下線・予測窓と候補一覧が変換中の文字の直下・Enter 1 回で確定・確定後の Enter で改行・変換中の ESC は変換だけ取消・確定後の ESC は値を残してフォーカスだけ外れ（IME は Auto→次フレーム Off）、ポーズは開いたまま。Mac の Editor（osascript・CEF あり）: 同じ挙動。候補一覧からの 1 回目の Return が確定にならず 2 回目で確定するのは Web の欄・input レーンの記録でも同じで、Mac の Editor 側の挙動（推測）。Game ビューを 0.46 倍に縮めると UITK の候補窓だけ約 200px ずれる（Web の欄は正しい。Editor だけの換算の差と推測。Windows の 1.5 倍では正しい）。Web の欄: Mac の Editor でビルドメニュー検索欄にフォーカスすると IME On になり日本語が入る（S9 の Auto 固着は解消）。測れなかったもの: Windows の Web の欄（このビルドでは CEF のサーバーが LoadUrl 直後に 4 回落ちて RecoveryFailed。同日の master の CI ビルド fbd9804 でも同じなので本ブランチの退行ではない）、Mac の配布ビルドの全画面（CI ビルドに非公開アセットが無くワールド初期化に失敗）、Retina（この Mac mini は 1920×1080 の非 Retina）。https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-uitk-s1-screens-ime/ |
| S12 | Unity の外での `dotnet test`（L0 の純ロジック） | L0-s3 | 成立（部分）。UnityEngine・UniRx・Localize に触れない L0 を `~/.dotnet/dotnet test`（net8.0・NUnit 3.14.0）で回せる。エンジン非依存の 54 件（Editor の L0 79 件のうち）が緑。UniRx・Localize に触れる L0（スクロール追従・素材ツールチップ・表示名）は Editor でだけ回す。csproj は `docs/superpowers/plans/2026-11-06-uitk-l0-s3.md` Task 1 Step 2（個別の `Compile Include` に `Condition="Exists(...)"` を足して使った。無いと未作成ファイルで CS2001）。CI には載せていない |
| W0-switch IME | Web 起動での IME モードの戻し | W0-switch | 未実測。Web 起動では `ImeCompositionModeApplier` を登録しない（GameUi の系だけ）ので、CEF が On にした IME を Off へ戻す者がいない。master と同じ挙動で、実害は確かめていない。W0-switch 計画の操作の死活表の「Web 起動で IME は生きる」はこの点を含まない |
| C2-a craft の枠 | UITK の枠幅・absolute の基準・パネル倍率での丸め（GamePanel の craft） | C2-a Task 3 | absolute の `top: 0` は 1px の枠の内側（padding box）が基準（倍率 1 で枠 1px のパネルの子が x=1）。枠幅は物理画素へ丸まり、0 より大きい枠は最低 1 物理画素になる（`resolvedStyle.borderLeftWidth` の実測: 3.132px→倍率 1 で 3・1.5 で 3.333・2 で 3、3.524px→4・3.333・3.5、0.392px→1・0.667・0.5、Web の倍率 2.5547 では 3.1315・3.5229・0.3914）。よって Web の 3.524px の輪に 3.132px の線を重ねる写し方は倍率 1.5（1920×1080）で輪が消える。見える帯（0.392px）だけを線の子（線の内縁が基準の inset 0）に別の枠で置き、どの倍率でも 1 物理画素を残す。輪を線と別にパネル基準で置くと、端数の座標（倍率 1 の x=0.4、1.5 の x=0.4）で線と輪が別々に丸まり、1px の隙間や輪の消失が出た（RenderTexture へ描いて実測）。面は `-1px` の子要素だと倍率 1.5 で外周 1 物理画素が塗られないので、パネル自身の background-color にした。UITK の背景は透明の枠の下まで塗られる（倍率 1.5 で 1px 透明枠の 20×10 要素が 30×15 物理画素すべて塗られるのを RenderTexture で実測）（`GamePanelCraftTest`） |
| GamePanel の影 | Web の default の `::before` の `box-shadow: 0 0 12px 7px rgb(9 11 20 / 45%)` が画面に出るか | C2-a | 出ない。同じ `::before` の `mask-image`（既定の `mask-clip: border-box`）が箱の外を切るので、外側の影は描かれない。参照 `web-a9d5123537/4-player-inventory.png` の持ち物パネルの左端（y=300）で x=14〜36 は世界の色 (139,100,57) のまま一定で、x=38 から面のフェードに入る（影なら外側 19px が暗くなる）。上端（x=250）も y=130〜154 は世界の縦グラデのまま。craft（`2-placement.png` の x=955〜967 は (245,200,153) のまま）と hud も外側は暗くならない。UITK でも影は描かない（`GamePanelShadow` は作らない）。描かないので負荷の実測は無い |
| C2-a ギャラリーの半透明 | 部品ギャラリー（EditorWindow）の撮影で α 補正トークンの色を Web と比べられるか | C2-a | 比べられない。EditorWindow は sRGB のまま合成し、Linear 用に α を下げたトークン（S7）が Web より淡く出る。FadeRule の頂点（`--game-panel-rule-bottom-peak` α0.2034）を hud の面 (10,15,27) に載せると、ギャラリーは (35,38,54)（面との差 25〜27）、ランタイムの PanelSettings（`targetTexture` に描く Linear 合成）は面 (46,40,40) に (72,71,83)（差 26〜43）で、Web の (87,85,98) on (57,52,54)（差 30〜44）と同じ。色はランタイムのパネルで撮って比べる（ギャラリーは配置・寸法・装飾の有無だけ）。撮り方は C2-a の作業報告 |
| C2-a 面の α と縁フェード | 暗い半透明の面（`--surface-navy`・`--game-panel-craft-face`・`--skit-window-face`）の α と、GamePanelFace の縁フェードの曲線がランタイム（Linear 合成）で Web に合うか | C2-a | α: S7 の式 1-(1-a)^2.2 の値は全体の面には不透明すぎ、面が Web より 10〜12 段暗い。ランタイムのパネルに単色を α を変えて描き Web の面と比べると、navy・craft（Web α0.8）は暗い世界 (140,109,77) で 0.944、明るい世界 (240,203,166) で 0.956、skit（Web α0.88）は (228,143,69) で 0.98 が最も近い（指数にすると約 1.85）。推奨は navy・craft 0.95、skit 0.98（トークンは integrator が決める）。G・B は Linear 合成の色相のずれで、明るい世界ほど B が 5〜8 段低いまま残る。縁フェード: 頂点の α を線形に補間すると暗い面が明るい世界の上で縁から遅れて濃くなり、50% 点がスキット上端で +8.9px（Web y=29.9、UITK 38.8）ずれた。フェードの帯を 8 等分し、各頂点の α をトークンの α から S7 の式を逆に解いた Web の α に mask を掛けて同じ式で補正し直すと（`GamePanelFaceOpacityRamp`）、スキット +0.6px・hud +0.2〜1.2px・持ち物 -0.8px になった（ランタイムのパネルで描いて実測。α トークンを替えても曲線の位置は変わらない、計算） |

## 壊れた録画の割り振り（bd moorestech-wwxy）

`moorestech-dww3`（EDC のコンパイル失敗の無言素通し）は W0-tools で閉じ、コンパイルできないシナリオはランナーが exit 2 で止まる。壊れている 10 本は、その UI を移すレーンの締めで直すか削除し、全体スイッチの UITK 側と Web 側の両方で流す（計画 §2.0-2）。X-prerun で全部が片付いたら wwxy を閉じる。

| シナリオ（`scenarios/` から） | 壊れ方 | 束 | 直すレーン | 方針 |
|---|---|---|---|---|
| `tutorial-pebble-challenge.cs` | 待つチャレンジ・チュートリアルの GUID がピン先マスタに無い | 2 | S2-ladder | ピン先マスタの GUID を引く形へ書き直す（`uitk-opening-hud` の下敷き） |
| `tutorial-equip-challenge.cs` | 同上 | 2 | S2-ladder | 同上 |
| `tutorial-tree-pin-nearest-search.cs` | 同上 | 2 | S2-ladder | 同上 |
| `server-notification-smoke.cs` | 削除済みの研究 GUID を送る | 2 | S2-ladder（S2-notification が先に直す） | 現行の研究 GUID へ直し、`notification-host` を UITK のロケータで解決する |
| `building/blueprint-copy-paste-via-ui.cs` | 削除済みの uGUI 型を参照してコンパイルできない | 3 | S3-ladder | `PlaytestBuildMenuOps`・`BlueprintNameInputState` へ移植（`uitk-build-bp` の下敷き） |
| `building/blueprint-right-click-delete-via-ui.cs` | 同上 | 3 | S3-ladder | 同上 |
| `connect/electric-wire-connect-via-ui.cs` | 同上 | 3 | S3-ladder | 削除（後継 `connect/electric-wire-tool-extend-chain-via-ui.cs` がある） |
| `misc/fps-tps-view-toggle-via-ui.cs` | 削除済みの uGUI 型（CrosshairView）を参照 | 3 | S3-ladder | `CrosshairVisibility` の照会へ移植 |
| `tutorial-research-renumber.cs` | 削除済みの研究 GUID と旧チェーン前提 | — | X-prerun | 削除（ADR 0038 で研究 1〜3 が消え、役目が終わった） |
| `misc/research-node-state-label.cs` | 削除済みの研究 GUID を送る | 4 | X-prerun | 現行の研究 GUID へ書き直す（研究ツリーの UITK 化の後に流す） |

## 見た目の例外

Web と同じにしない見た目の差（設計書 `docs/uitk-migration/execution/component-architecture.md` §3.4）。入れてよいのは「同じにするための対応が大きいか、ブラウザの癖の再現が要り、かつ差が一目で分からない」ものだけ。配置・色の系統・装飾の有無・文字の大きさの差は入れない。例外の USS・C# の行には `/* look-exception: LX-n */`（C# は `// look-exception: LX-n`）を付け、`LookExceptionLedgerTest` が台帳と印を照合する。

| ID | 画面・部品 | 差 | 同じにしない理由 | 推奨案 | 撮影 | 出所 |
|---|---|---|---|---|---|---|
| LX-1 | ポーズの題（`PauseMenu.uss` の `.pause-menu__title`） | Web は Mantine Title order=1 の 34px、UITK は 32px（行の高さ 44px は同じ） | GameUi のフォントは同じ px で字幅が約 7% 広く、34px では題の幅が Web より広がる。字の大きさの差は 2px で、行と幅は Web に合う | 32px のまま。フォントを替える段で 34px に戻せるか測る | https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-uitk-s1-web-look-evidence/index.html | agent 既定（段階 1 screens の実測） |
| LX-2 | `PanelActionButton` の文字 | Web は `text-shadow`、UITK は `-unity-text-outline-width: 0.3px` の縁取りで近似 | USS に text-shadow の任意のにじみが無く、同じにするには文字の描画を自作する必要がある。差は縁の柔らかさだけ | 縁取りの近似のまま | https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-uitk-s1-web-look-evidence/index.html | agent 既定（段階 1 theme） |
| LX-3 | `ModeSwitch` の選択肢の輪 | Web は `box-shadow` の ring、UITK は `--bevel-1` 幅の `border` で近似 | USS に box-shadow が無い。枠線は内側に入るが幅と色は同じで、並べて差が見えない | 枠線の近似のまま | https://sakastudiomac-mini.tail3a5aa5.ts.net:8443/2026-11-06-uitk-s1-web-look-evidence/index.html | agent 既定（段階 1 theme） |
| LX-4 | `GamePanel` craft の明るい輪（`GamePanel.uss` の `.game-panel__craft-rim`） | Web は 0.392px、UITK は最低 1 物理画素 | UITK の枠幅は物理画素へ丸まり、0 より大きい枠は 1 物理画素が下限（本ファイル「実測の結論」の C2-a craft の枠） | 1 物理画素の近似のまま | 本ファイル「実測の結論」C2-a craft の枠の実測 | agent 既定（C2-a 実測） |
