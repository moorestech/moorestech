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

1. `disposition.md` の全59項目が「済 / 吸収先 Phase 完了 / 除外」のいずれかで処理済み
2. 対象の全平面 UI が Web で表示され、対応する uGUI ビューが全て `WebUiScreenGate` で抑止されている
   （UIStateEnum 全 state に加え、**状態外オーバーレイ**〔常駐 HUD・Ctrl+U・バックグラウンドスキット・
   カットシーン退避・カーソル追従〕を別軸の一覧で確認する）
3. CEF バイナリが手動回避なしで解決される（INFRA-1）
4. 入力・IME・フォーカスが実機で破綻しない（INFRA-2）
5. Vite dev サーバ非依存の本番配信形態で起動できる（INFRA-9。Windows 実機確認を含む）
6. WS 切断→再接続・CEF リロード・フォーカス往復で各画面の状態が復元される（INFRA-13。fault-injection スモーク）
7. 全可視文字列が i18n 経由で、言語切替が全画面に反映される（INFRA-11）
8. クラフトツリー機能が削除済み
9. PlayMode 実機での全画面遷移スモーク + 画面ごとの操作パリティ受け入れ表がパスする
   （「開くだけ」でなく主要操作・更新の動作確認を含む）

uGUI コードの物理削除は上記達成後の**別判断**（本プランのスコープ外）。

## 3. アーキテクチャ（現行・確立済み）

- **描画**: CEF（UPM `jp.juha.cefunitysample`）を透明オーバーレイとして重畳。RawImage raycastTarget=0 で世界クリック貫通
- **配信**: Unity プロセス内 Kestrel（`Client.WebUiHost/Boot/KestrelServer.cs`、`127.0.0.1:5050`）。フロントは Vite dev サーバを Unity が自動 spawn（`Vite/ViteProcess.cs`）
- **フロント**: `moorestech_web/webui/`（React 18 + Mantine v8 + Zustand + TS）。UI 装飾の画像アセット化は禁止（CSS/DOM/インライン SVG 限定）
- **ブリッジ**: WebSocket JSON RPC（`Boot/WebSocketHub.cs`）。**Topic**（server→web 配信）と **Action**（web→unity RPC）
- **契約の単一ソース**: `webui/src/bridge/transport/protocol.ts` ⇔ C# `WireFixtures/*.json`。`wireContract.test.ts` / `WireContractTest.cs` が両側で型一致を強制
- **状態主権**: 画面遷移は uGUI の `UIStateControl`（`UIStateEnum`）が主権。加えて**第2状態機械 `GameStateType`**（カットシーン等のゲーム全体状態）が存在し、Topic 化は Phase C4 で行う。Web は `ui_state.current` topic で追従し、遷移要求は `ui_state.request` action（許可済み intent 限定）
- **切替**: `WebUiScreenGate`（実効 web モード = Ctrl+I トグル ON かつホスト起動成功）。置換済み uGUI ビューは `SetActive(isActive && !WebUiScreenGate.IsWebUiMode)` で自己抑止

## 4. Phase 構成と依存関係

3トラック並行 + 最終 Phase。**A はいつでも並行可・最優先は A1**。B は小さく即効、C は大物画面、D が締め。

依存は「**着手依存**（実装を始められるか）」と「**完了ゲート依存**（Phase 完了判定に必要か）」を区別する。
例: C1 は着手依存なしだが、完了ゲートには実機検証（A1）とチャレンジ完了イベント規約（A4）が要る。

| Phase | 内容 | 規模 | 着手依存 | 計画書 |
|---|---|---|---|---|
| **A1** | CEF バイナリ恒久統合（INFRA-1） | 中 | なし・**最優先** | `plans/phase-a-infra.md` |
| **A2** | 入力・IME・フォーカス排他（INFRA-2） | 中 | A1（実機検証） | 同上 |
| **A3** | 本番静的配信 + アセット配信 + Windows/Linux（INFRA-9/8/5） | 中 | なし | 同上 |
| **A4** | 接続堅牢性 + Topic 横断規約（INFRA-13/7: revision・再接続復元・死活） | 中 | なし | 同上 |
| **A5** | i18n 基盤 + 要素 ID 規約の策定（INFRA-11/12 前倒し） | 小〜中 | なし（Web 側は WU 完了待ち） | 同上 |
| **B1** | ギア伝達系レジストリ登録 + ElectricToGearGenerator 専用ビュー | 中 | WU 完了 | `plans/phase-b1-gear-blocks.md` |
| **B2** | 列車 PF インベントリ + 電柱ネットワーク情報 | 中 | WU 完了 | `plans/phase-b2-train-pf-electric-pole.md` |
| **B3** | 細部パリティ（ドラッグ配分・クラフト表示・品質フォロー） | 小粒×10 | WU 完了・1件ずつ独立 | `plans/phase-b3-detail-parity.md` |
| **C1** | チャレンジ（リスト+ツリー+HUD）— ツリー描画基盤の確立 | 大 | WU 完了・A5（文言/anchor） | `plans/phase-c1-challenge.md` |
| **C2** | ポーズメニュー + モード系 HUD + 共通部品（ツールチップ基盤含む） | 中〜大 | WU 完了・A5 | `plans/phase-c2-pause-mode-common.md` |
| **C3** | 列車乗車 HUD + 列車インベントリ | 大 | A2（乗車入力）・A4（イベント規約）・B2 推奨 | `plans/phase-c3-train-hud.md` |
| **C4** | スキット / チュートリアル / カットシーン（**再設計から**） | 特大 | A2・A4・A5。設計文書は先行可 | `plans/phase-c4-skit-tutorial.md` |
| **D** | カットオーバー完了: 全面ゲート化監査・既存画面の i18n 変換・最終検証 | 中 | A〜C 全部 | `plans/phase-d-cutover.md` |

推奨着手順: **A1 →（WU 完了後）A5 → B1〜B3（並行で A2/A3/A4）→ C1 → C2 → C3 → C4 → D**。
クラフトツリー削除（別設計書）と C4 の再設計文書はいつでも先行可。
大物画面（C1〜C4）は着手時に本計画書を元へ writing-plans 形式の詳細計画（完全コード付き）を切ってから実装する。

### 並行運用ルール（設計負債解消 WU1〜9 実行中の間）

- `moorestech_web/webui/src` を触る Phase（B/C 全部と A5 の Web 側）は **WU1〜9 完了まで凍結**
  （WU5 が契約層を zod へ全面書き換え、WU6/8 がスロット部品と App.tsx を再編するため）
- 並行可能: **A1**（Unity Packages 側）、A3/A4 の C# 側、調査・再設計文書・詳細計画の作成
- WU5 完了時点で、本書と各 plans の契約手順（`protocol.ts`/`payloadTypes.ts` 追記）を
  zod 方式の記述へ更新すること（WU 側 PR に含めるか、直後に本書を改訂）

## 5. 共通手順: 1画面を Web 化する定型フロー

順序規約: **実装漏れ確定（uGUI 実コード確認）→ Topic/Action 拡充 → ビュー実装**。

1. **C# Topic**: `Client.WebUiHost/Game/Topics/` に `ITopicHandler` 実装（`GetSnapshotJsonAsync` + 変化時 `hub.Publish`）。データ源は client の `_blockStateMessagePack` republish かサーバ `*EventPacket` 中継（イベント + 初期 snapshot + 購読の3点セット）。連続変動値（トルク/RPM 等）は固定間隔サンプリングで publish。**A4 の Topic 横断規約に従う**（状態 Topic は単調増加 revision を持ち、snapshot と event の競合で古い値が新しい値を上書きしないこと・再接続時に snapshot から復元できること）
2. **C# Action**（操作があれば）: `Game/Actions/` に `IActionHandler` 実装。進行を伴う操作（会話送り・選択肢等）は**冪等化**する（対象 revision/id を payload に含め、Unity 側で現在状態と照合して古い要求を破棄）
3. **登録**: `Game/WebUiGameBinder.Bind()` に `RegisterTopic/RegisterAction` を追加
4. **契約**: `protocol.ts` の Topics/ActionPayloads/ACTION_TYPES + `payloadTypes.ts` + `WireFixtures/*.json` + 両側契約テスト（※WU5 の zod 移行後は zod スキーマ単一定義 + `z.infer` に読み替え。移行後に本節を更新する）
5. **Web 実装**: `src/features/<画面>/` に component/css/logic/vitest。`App.tsx` に `ui_state.current` 由来のルーティング追加。可視文字列は **i18n 経由**（A5 基盤・ハードコード禁止）。チュートリアル対象になり得る要素へ **`data-tutorial-anchor`**（A5 規約）を実装時に付与（`data-testid` とは分離）
6. **uGUI 抑止**: 対応 uGUI ビューに `&& !WebUiScreenGate.IsWebUiMode` を追加
7. **検証**: `pnpm vitest run` + Playwright e2e（mock-host, workers:1）+ `.cs` 変更は `uloop compile`

## 6. 横断の検証ゲート

- `.cs` 変更時: `uloop compile --project-path ./moorestech_client` → 関連テストを `--filter-type regex` で
- Web 変更時: tsc / vitest / 該当 e2e
- **ゲート漏れ決定論チェック**（最初の移行 PR から導入。Phase D まで待たない）:
  「webモード中に表示され得る uGUI 平面ビューにゲートが入っているか」「新規スクリーンスペース
  uGUI の追加禁止」「除外 allowlist（ワールド空間/メインメニュー/デバッグ）」を機械判定するスクリプト
- 各 Phase 完了ごと: コミット + `TODO.md` チェック + `disposition.md` へ完了日追記 +
  PlayMode 実機スモーク（`unity-playmode-recorded-playtest`）
- 実装単位は **1 タスク = 1 PR 相当**に切る（Phase はマイルストーンであり PR 単位ではない。
  各 plans 内のタスク分割に従う）
- 品質バックログ: `design-debt-audit-2026-07-17.md`（CSS/設計負債。Phase 作業のついでに該当箇所を消化）

## 7. ドキュメントマップ

| ファイル | 役割 |
|---|---|
| `MIGRATION.md`（本書） | 移行の入口。方針・完了定義・Phase 構成 |
| `disposition.md` | 旧台帳 全59項目の処遇追跡表（済/吸収先/除外。Phase 完了時に更新） |
| `TODO.md` | 進捗チェックリスト（今やることだけ） |
| `plans/phase-*.md` | Phase ごとの実行計画書 |
| `design-debt-audit-2026-07-17.md` | Web 側 CSS/設計負債の監査結果（現役バックログ） |
| `subagent-execution-plan-2026-07-18.md` | 設計負債解消の WU 実行計画（現役・上記監査の処方箋） |
| `archive/` | 旧方針時代の台帳・計画・申し送り（履歴。根拠参照用） |

旧台帳 `archive/cef-webui-migration-todo.md` は FEAT-* の受け入れ条件の根拠アーカイブ。
各 Phase 計画書に必要分は転記済みだが、疑義があれば原典として参照する。
