# Web UI ネイティブ化 マスタープラン

**策定**: 2026-07-18 / **方針**: uGUI 共存を終了し、平面 UI の表示をすべて Web（CEF + React）へ移行する。
本書が移行の**唯一の入口**。進捗チェックは `TODO.md`、各 Phase の作業詳細は `plans/` の実行計画書を参照。

---

## 1. 方針（2026-07-18 決定）

Web UI が安定したため「uGUI 凍結・並走 → パリティ到達後に一斉カットオーバー（旧 D6）」を廃し、
**Web を表示の正とするネイティブ化**へ転換した。

| 決定 | 内容 |
|---|---|
| 全面移行 | インゲームの平面（スクリーンスペース）UI は Web 表示を正とする。新規 UI は Web 側のみに実装 |
| uGUI コード | 撤去判断まで残置。表示のみ `WebUiScreenGate.IsWebUiMode` で抑止（従来方式を継続） |
| ワールド空間 UI | **uGUI のまま維持（移行対象外）**: MapObject HP バー・ブロック進捗バー・チュートリアルマップピン・電線設置ラベル等の 3D ビルボード系 |
| クラフトツリープランナー | **機能ごと削除**（`InGame/CraftTree/` 9ファイル。移行せず撤去 — Phase D） |
| スキット / チュートリアル | 単純移植不可。**Web 向け再設計**を前提に設計から行う（Phase C4） |
| メインメニュー | 旧 D3 維持でスコープ外（`Client.MainMenu` 別シーンは当面 uGUI のまま） |
| デバッグ UI / エディタツール | 非出荷のため移行対象外 |

## 2. 完了定義（Definition of Done）

1. 対象の全平面 UI が Web で表示され、対応する uGUI ビューが全て `WebUiScreenGate` で抑止されている
2. CEF バイナリが手動回避なしで解決される（INFRA-1）
3. 入力・IME・フォーカスが実機で破綻しない（INFRA-2）
4. Vite dev サーバ非依存の本番配信形態で起動できる（INFRA-9）
5. クラフトツリー機能が削除済み
6. PlayMode 実機での全画面遷移スモークがパスする

uGUI コードの物理削除は上記達成後の**別判断**（本プランのスコープ外）。

## 3. アーキテクチャ（現行・確立済み）

- **描画**: CEF（UPM `jp.juha.cefunitysample`）を透明オーバーレイとして重畳。RawImage raycastTarget=0 で世界クリック貫通
- **配信**: Unity プロセス内 Kestrel（`Client.WebUiHost/Boot/KestrelServer.cs`、`127.0.0.1:5050`）。フロントは Vite dev サーバを Unity が自動 spawn（`Vite/ViteProcess.cs`）
- **フロント**: `moorestech_web/webui/`（React 18 + Mantine v8 + Zustand + TS）。UI 装飾の画像アセット化は禁止（CSS/DOM/インライン SVG 限定）
- **ブリッジ**: WebSocket JSON RPC（`Boot/WebSocketHub.cs`）。**Topic**（server→web 配信）と **Action**（web→unity RPC）
- **契約の単一ソース**: `webui/src/bridge/transport/protocol.ts` ⇔ C# `WireFixtures/*.json`。`wireContract.test.ts` / `WireContractTest.cs` が両側で型一致を強制
- **状態主権**: uGUI の `UIStateControl` が唯一の状態機械。Web は `ui_state.current` topic で追従し、遷移要求は `ui_state.request` action（許可済み intent 限定）
- **切替**: `WebUiScreenGate`（実効 web モード = Ctrl+I トグル ON かつホスト起動成功）。置換済み uGUI ビューは `SetActive(isActive && !WebUiScreenGate.IsWebUiMode)` で自己抑止

## 4. Phase 構成と依存関係

3トラック並行 + 最終 Phase。**A はいつでも並行可・最優先は A1**。B は小さく即効、C は大物画面、D が締め。

| Phase | 内容 | 規模 | 依存 | 計画書 |
|---|---|---|---|---|
| **A1** | CEF バイナリ恒久統合（INFRA-1） | 中 | なし・**最優先** | `plans/phase-a-infra.md` |
| **A2** | 入力・IME・フォーカス排他（INFRA-2） | 中 | A1（実機検証に必要） | 同上 |
| **A3** | 本番静的配信 + Vite 死活 + Windows/Linux（INFRA-9/8） | 中 | なし | 同上 |
| **B1** | ギア伝達系レジストリ登録 + ElectricToGearGenerator 専用ビュー | 中 | なし | `plans/phase-b1-gear-blocks.md` |
| **B2** | 列車 PF インベントリ + 電柱ネットワーク情報 | 中 | なし | `plans/phase-b2-train-pf-electric-pole.md` |
| **B3** | 細部パリティ（ドラッグ配分・クラフト表示・品質フォロー） | 小粒×9 | なし・1件ずつ独立 | `plans/phase-b3-detail-parity.md` |
| **C1** | チャレンジ（リスト+ツリー+HUD）— ツリー描画基盤の確立 | 大 | なし | `plans/phase-c1-challenge.md` |
| **C2** | ポーズメニュー + モード系 HUD + 共通部品 | 中〜大 | なし | `plans/phase-c2-pause-mode-common.md` |
| **C3** | 列車乗車 HUD + 列車インベントリ | 大 | A2（乗車入力）、B2 推奨 | `plans/phase-c3-train-hud.md` |
| **C4** | スキット / チュートリアル / カットシーン（**再設計から**） | 特大 | A2・C1〜C3 完了推奨（要素 ID 規約は全画面に波及） | `plans/phase-c4-skit-tutorial.md` |
| **D** | カットオーバー完了: 全面ゲート化・i18n・クラフトツリー削除・最終検証 | 中 | A〜C 全部 | `plans/phase-d-cutover.md` |

推奨着手順: **A1 → B1〜B3（並行で A2/A3）→ C1 → C2 → C3 → C4 → D**。
大物画面（C1〜C4）は着手時に本計画書を元へ writing-plans 形式の詳細計画（完全コード付き）を切ってから実装する。

## 5. 共通手順: 1画面を Web 化する定型フロー

順序規約: **実装漏れ確定（uGUI 実コード確認）→ Topic/Action 拡充 → ビュー実装**。

1. **C# Topic**: `Client.WebUiHost/Game/Topics/` に `ITopicHandler` 実装（`GetSnapshotJsonAsync` + 変化時 `hub.Publish`）。データ源は client の `_blockStateMessagePack` republish かサーバ `*EventPacket` 中継（イベント + 初期 snapshot + 購読の3点セット）。連続変動値（トルク/RPM 等）は固定間隔サンプリングで publish
2. **C# Action**（操作があれば）: `Game/Actions/` に `IActionHandler` 実装
3. **登録**: `Game/WebUiGameBinder.Bind()` に `RegisterTopic/RegisterAction` を追加
4. **契約**: `protocol.ts` の Topics/ActionPayloads/ACTION_TYPES + `payloadTypes.ts` + `WireFixtures/*.json` + 両側契約テスト
5. **Web 実装**: `src/features/<画面>/` に component/css/logic/vitest。`App.tsx` に `ui_state.current` 由来のルーティング追加
6. **uGUI 抑止**: 対応 uGUI ビューに `&& !WebUiScreenGate.IsWebUiMode` を追加
7. **検証**: `pnpm vitest run` + Playwright e2e（mock-host, workers:1）+ `.cs` 変更は `uloop compile`

## 6. 横断の検証ゲート

- `.cs` 変更時: `uloop compile --project-path ./moorestech_client` → 関連テストを `--filter-type regex` で
- Web 変更時: tsc / vitest / 該当 e2e
- 各 Phase 完了ごと: コミット + `TODO.md` のチェック更新 + PlayMode 実機スモーク（`unity-playmode-recorded-playtest`）
- 品質バックログ: `design-debt-audit-2026-07-17.md`（CSS/設計負債。Phase 作業のついでに該当箇所を消化）

## 7. ドキュメントマップ

| ファイル | 役割 |
|---|---|
| `MIGRATION.md`（本書） | 移行の入口。方針・完了定義・Phase 構成 |
| `TODO.md` | 進捗チェックリスト（今やることだけ） |
| `plans/phase-*.md` | Phase ごとの実行計画書 |
| `design-debt-audit-2026-07-17.md` | Web 側 CSS/設計負債の監査結果（現役バックログ） |
| `subagent-execution-plan-2026-07-18.md` | 設計負債解消の WU 実行計画（現役・上記監査の処方箋） |
| `archive/` | 旧方針時代の台帳・計画・申し送り（履歴。根拠参照用） |

旧台帳 `archive/cef-webui-migration-todo.md` は FEAT-* の受け入れ条件の根拠アーカイブ。
各 Phase 計画書に必要分は転記済みだが、疑義があれば原典として参照する。
