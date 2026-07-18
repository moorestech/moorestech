# 旧台帳 全項目 disposition 表

旧台帳 `archive/cef-webui-migration-todo.md` の全 FEAT-*/INFRA-* 59 項目について、
新計画（`MIGRATION.md` / `plans/`）での処遇を1件ずつ確定する追跡表。
**「済」「吸収先 Phase」「除外（理由）」のいずれかに必ず分類される**（未分類 = 計画の欠陥）。
外部監査（2026-07-18・Codex 3系統）の Blocker 指摘「項目別対応表がない」への対応。

## INFRA

| ID | 内容 | 処遇 |
|---|---|---|
| INFRA-1 | CEF バイナリ恒久統合 | **A1** |
| INFRA-2 | 入力・IME・フォーカス排他 | **A2** |
| INFRA-3 | uGUI/CEF 表示切替 | **済**（`WebUiCefToggle` Ctrl+I） |
| INFRA-4 | C#→TS 型自動生成 | **D**（任意） |
| INFRA-5 | アセット配信拡張（立ち絵等の汎用画像） | **A3**（配信基盤・2026-07-18 完了 `/api/assets/`）。スキット立ち絵はUnity描画残置決定（skit-web-redesign.md）のため利用側なし |
| INFRA-6 | UIState 橋渡し | **済**（最小版）。GameStateType（第2状態機械）Topic 化は **C4** |
| INFRA-7 | サーバーイベント push 規約（revision・デバウンス・再接続整合性） | **A4**（2026-07-18 完了。`topic-conventions.md`） |
| INFRA-8 | Windows/Linux 対応 | **A3**（Windows のみ DoD。Linux は非 DoD・将来枠） |
| INFRA-9 | 本番配信堅牢性（静的配信・成果物整合・動的ポート・多重起動） | **A3**（2026-07-18 実装完了。Windows実機確認はD最終検証） |
| INFRA-10 | CEF 音声専有 | **C4**（スキットのボイス方式決定に内包。決定責務は C4） |
| INFRA-11 | i18n | **A5**（2026-07-18 基盤完了）+ **D**（既存画面の変換） |
| INFRA-12 | Web UI 要素 ID 規約 | **A5**（2026-07-18 規約策定完了 `anchor-convention.md`）+ 各画面 Phase（付与済み）+ **C4**（registry実装中） |
| INFRA-13 | CEF/接続堅牢性（死活・クラッシュ復帰・再接続 snapshot 復元) | **A4**（2026-07-18 完了。heartbeat+restoring復元+再接続テスト） |

## FEAT

| ID | 内容 | 処遇 |
|---|---|---|
| INV-1 | プレイヤーインベントリ（メイン+grab） | **済**（スプリット/右ドラッグ含め全件 2026-07-18 完了） |
| INV-2 | ホットバー | **済**。ホイール入力量累積もB3済（2026-07-18） |
| INV-3 | スロット共通部品・ツールチップ | **済**。ツールチップ基盤もC2で正式化済み（2026-07-18） |
| INV-4 | SubInventory 土台 | **済** |
| INV-5 | 列車（貨車）インベントリ | **C3**（2026-07-18 完了。SubInventory拡張+エラー表示） |
| INV-6 | 液体スロット共通部品 | **済** |
| BLK-1〜5, 8 | チェスト/発電機/機械/採掘機/ギア機械/フィルタ分岐器 | **済** |
| BLK-6 | ギア伝達系（レジストリ未登録で Generic 落ち） | **B1**（2026-07-18 完了。Shaft/Gear/GearBeltConveyor 21ブロック+網羅e2e） |
| BLK-7 | ElectricToGearGenerator | **B1**（2026-07-18 完了） |
| BLK-9 | 列車プラットフォーム（item/fluid） | **B2**（2026-07-18 完了。PF2種+TrainStation+冪等モード切替） |
| BLK-10 | ベースキャンプ | **除外（保留）**: v8 マスタに実体0。マスタに登場したら B 系として追加 |
| CRAFT-1 | クラフト長押し・連続クラフト | **B3**（既実装確認済み 2026-07-18） |
| CRAFT-2 | 機械レシピビューア | **済** |
| CRAFT-3 | レシピ対象アイテムリスト | **済**。可能数バッジもB3済（2026-07-18） |
| CRAFT-4 | クラフトツリー | **除外（機能削除・実施済み）**: 削除設計書 `docs/superpowers/specs/2026-07-18-craft-tree-removal-design.md`。削除実装を web-ui へ統合済み（2026-07-18 `db2b3f5ba`。回帰テスト CraftTreeRemovalTest/AssembleSaveJsonTextTest 合格） |
| CHAL-1 | チャレンジリスト/ツリー | **C1**（2026-07-18 完了。treeView共通基盤+challenge.tree） |
| CHAL-2 | 進行中チャレンジ HUD | **C1**（2026-07-18 完了。challenge.current常駐HUD） |
| RES-1 | 研究ツリー | **済**（報酬個数表示もB3-8で完了 2026-07-18。treeView共通基盤へ載せ替え済み） |
| MODE-1 | 設置モード HUD | **C2**（2026-07-18 完了） |
| MODE-2 | 削除モード HUD | **C2**（2026-07-18 完了。不可理由は契約のみ・既存実装に意味的理由なし） |
| MODE-3 | デバッグ系 UI | **除外**: 非出荷（2026-07-18 方針） |
| MODE-4 | 給電範囲オーバーレイ連携 | **C2**（2026-07-18 完了。placement_mode topicへ統合） |
| MODE-5 | 直接採掘 HUD | **C2**（2026-07-18 完了。ui.mining_hud） |
| TRAIN-1 | 列車乗車 HUD | **C3**（2026-07-18 完了。train.riding+入れ子Pause subState・実機一巡はD） |
| COM-1 | コンテキストメニュー | **C2**（2026-07-18 完了。ID照合Action） |
| COM-2 | モーダル | **済**（基盤+確認ダイアログ）。RequestModal 実プロデューサ配線は品質バックログ（実ユースケース決定待ち） |
| COM-3 | 汎用プログレスバー | **済**。D監査でProgressBarViewの二重表示ゲート漏れを検出し修正（2026-07-18）。ワールド空間のブロック進捗バーは**除外**（uGUI 維持） |
| COM-4 | キー操作ヒント | **C2**（2026-07-18 完了。ui.key_hints） |
| COM-5 | トースト | **済**（Web 新規基盤） |
| COM-6 | 全 UI 一括非表示（Ctrl+U） | **C2**（2026-07-18 完了。ui.visibility） |
| COM-7 | カーソル追従オーバーレイ | **C2**（2026-07-18 棚卸し完了: 個別Web移植不要。grabはGrabOverlay・ContextMenuはWeb pointer座標へ移行済み。uGUIフォールバック用C#実装は残置） |
| WORLD-1 | 3D オブジェクトのツールチップ | **C2 に部分吸収**（2026-07-18 完了: 判定Unity残置・表示はui.tooltip連携） |
| WORLD-2 | マップオブジェクト HP バー | **除外**: ワールド空間 UI は uGUI 維持（2026-07-18 方針） |
| WORLD-3 | マップ UI | **除外**: 実体が存在しない（調査済み） |
| TUT-1 | チュートリアル | **C4**（再設計。ワールド系ピン/矢印は uGUI 残置） |
| SKIT-1 | スキット | **C4**（再設計） |
| SKIT-2 | バックグラウンドスキット | **C4**（2026-07-18 完了。skit.presentation snapshot・音声Unity維持・文字表示ゲート） |
| CUT-1 | カットシーン | **C4**（2026-07-18 完了。game_state.current+Web全レイヤ退避） |
| SYS-1 | ポーズメニュー | **C2**（2026-07-18 完了。pause_menu.current+セーブ/復帰Action） |
| SYS-2 | メインメニュー | **除外**: 旧 D3 維持（2026-07-18 再確認） |
| SYS-3 | 設定画面 | **除外**: 実体が存在しない（言語設定はメインメニュー=スコープ外） |

## 運用

- Phase 完了時、本表の該当行に完了日を追記する（例: `**B1**（2026-07-20 完了）`）
- 新項目が発生したら本表に行を追加してから Phase へ割り当てる
