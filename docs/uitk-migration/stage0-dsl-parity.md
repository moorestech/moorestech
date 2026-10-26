# 段階0 0-7 DSL 改名・照会置換の合否突き合わせ

基準: origin/master `e855a58667`（改名前）。変更後: ブランチ `feature/uitk-s0-dsl`。どちらも Record=false、同じ前提・同じ順で流した。
対象外: 壊れている 7 本（bd `moorestech-wwxy`）。

| シナリオ | 種別 | 基準 Success / Assert数 / 失敗Assert / Error先頭 | 変更後 Success / Assert数 / 失敗Assert / Error先頭 | 判定 | 備考 |
|---|---|---|---|---|---|
| building/delete-tool/delete-hold-mode-reentry | 照会置換 | true / 16 / - / - | | |  |
| building/delete-tool/delete-hold-text-focus | 照会置換 | true / 20 / - / - | | |  |
| building/placement-reason-tooltip | 照会置換 | true / 14 / - / - | | |  |
| building/terrain-sink-normal-and-belt-via-ui | 照会置換 | true / 20 / - / - | | |  |
| building/ui-place-then-open-machine | 照会置換 | false / 6 / Web側のblock inventoryパネルが描画されている / - | | | 1回目の結果。再現確認は後続で実施 |
| connect/electric-wire-tool-extend-chain-via-ui | 照会置換 | true / 16 / - / - | | |  |
| connect/rotary-generator-ui | 照会置換 | false / 38 / DOM状態文字列: 発電中, DOM状態がサーバーenumと一致, サーバー供給量, 需要消失後のサーバー状態は待機中・供給0 / TimeoutException: Until '需要消失後のサーバー状態は待機中・供給0' timed out after 30s | | |  |
| graphics-quality/quality-recovery-ui | 照会置換 | 未測定 | | |  |
| graphics-quality/quality-ui-switching | 照会置換 | false / 0 / - / シナリオのコンパイル失敗(result.json無し) | | | HEADのシナリオが CS0103 (renderer/pipe/waterLevelView/networkPipes/networkWater 未定義)で投入できない。基準失敗 |
| misc/cursor-tooltip-follows-crosshair | 照会置換 | false / 7 / 退避先の隅にツールチップが追従した（前提がWeb側へ届いた） / TimeoutException: UI state did not reach PlayerInventory (current: GameScreen) | | |  |
| misc/feedback-report-labels | 照会置換 | true / 2 / - / - | | |  |
| misc/pause-menu-hierarchy | 照会置換 | 未測定 | | |  |
| misc/sprint/sprint-toggle-mode | 照会置換 | 未測定 | | |  |
| misc/sprint/sprint-hold-restart | 照会置換 | 未測定 | | |  |
| misc/sprint/sprint-invalid-recovery | 照会置換 | 未測定 | | |  |
| misc/vein-hand-mining-smoke | 照会置換 | 未測定 | | |  |
| playtest-receiver/developer-report-delivery | 照会置換 | 未測定 | | |  |
| train/fluid-inventory-via-interact | 照会置換 | 未測定 | | |  |
| building/blueprint-large-paste-flicker | 改名のみ | 未測定 | | |  |
| building/blueprint-place-like-block | 改名のみ | 未測定 | | |  |
| building/blueprint-terrain-sink-partial-paste | 改名のみ | 未測定 | | |  |
| connect/electric-wire-tool-isolated-place-via-ui | 改名のみ | 未測定 | | |  |
| connect/electric-wire-tool-pole-cycle-rotate-via-ui | 改名のみ | 未測定 | | |  |
| connect/electric-wire-tool-pole-ghost-hide-paths-check | 改名のみ | 未測定 | | |  |
| connect/electric-wire-tool-pole-ghost-visibility-check | 改名のみ | 未測定 | | |  |
| misc/localization-language-switch-via-ui | 改名のみ | 未測定 | | |  |
| misc/walk-while-menus-open | 改名のみ | 未測定 | | |  |
| misc/webui-buildmenu-click | 改名のみ | 未測定 | | |  |
| train/train-station-default-name | 改名のみ | 未測定 | | |  |
| train/train-timetable-via-ui | 改名のみ | 未測定 | | |  |
