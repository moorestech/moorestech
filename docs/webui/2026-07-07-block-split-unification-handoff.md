# 申し送り: ブロック側「半分掴み」のホスト計算統一（block_inventory.split 追加）

**日付**: 2026-07-07 / **出典**: webuiクリーンアップ実行計画（`../superpowers/plans/2026-07-07-webui-cleanup.md`「意思決定の記録」§1）のスコープ外 follow-up

## 何が分裂しているか

スロット右クリック（空手）の「半分掴み」の計算場所が、プレイヤー側とブロック側で異なる。

| 対象 | 送る action | 半分の計算者 |
|---|---|---|
| プレイヤースロット | `inventory.split`（payload に count なし） | **ホスト**（C# `SplitGrabActionHandler` が `item.Count / 2`） |
| ブロックスロット | `block_inventory.move_item`（count=クライアント計算の半分） | **クライアント**（`Math.floor(slot.count / 2)`） |

**現時点で挙動差はゼロ**（両者とも床関数半分。C# 側を 2026-07-07 に実測確認済み）。
問題は挙動ではなく構造: ブロック側はクライアントの表示中 count（stale になり得る）に依存して数量を確定させており、
ホスト側の最新値で計算するプレイヤー側と信頼モデルが揃っていない。

## 現在の防衛線（クリーンアップで実施済み）

- 両プランナを `moorestech_web/webui/src/shared/itemMove/` の同一モジュール群に集約済み
  - プレイヤー側: `playerSlotPlan.ts` の `planPlayerRightClick`（`inventory.split` を送るだけ）
  - ブロック側: `blockSlotPlan.ts` の `planBlockRightClick`（**唯一の分岐点**。「ホストに block_inventory.split が無いためここだけ client 計算」のコメント付き）
- 差異はこの1関数に閉じ込め済み。unit テスト（`blockSlotPlan.test.ts`）と e2e（`blockInventoryGestures.spec.ts` / `machineGestures.spec.ts`）が現行挙動を固定している

## 統一する場合の作業手順（webui 単独では不可能・C# 変更が必要）

1. **C#**: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/BlockInventoryActions.cs` に
   `block_inventory.split` ハンドラを追加（`BlockAreaSlotParser` と `InventoryActions.cs` の `SplitGrabActionHandler` を参照。
   登録は `WebUiGameBinder.cs`）。half 計算は既存 `SplitGrabActionHandler` と同じ床関数に揃える
2. **プロトコル**: `webui/src/bridge/transport/protocol.ts` の `ActionPayloads` にエントリ追加。
   `ACTION_TYPES` は網羅チェック（`ActionTypesExhaustive`）があるため、追記漏れはコンパイルエラーで検出される
3. **mock-host**: `webui/e2e/mock-host/` に split 適用を**新規実装**（`inventoryModel.ts` / `wsHandler.ts`）。
   注意: mock-host に `inventory.split` の適用実装は**存在しない**（プレイヤー側 e2e は送信 payload の検証のみで状態適用していない）ため、手本になる split 分岐は無い。
   構造の参考は `applyBlockMove`。この実装は省略不可——`blockInventoryGestures.spec.ts` が右クリ後の grab-overlay 表示（＝状態変化）を検証しているため、適用しないと手順4のテストが通らない
4. **プランナ切替**: `blockSlotPlan.ts` の `planBlockRightClick` を「count 計算せず `block_inventory.split` を送る」に変更し、
   分岐点コメントを削除。`blockSlotPlan.test.ts` の期待値と、count を全等値照合している e2e
   （`blockInventoryGestures.spec.ts` の右クリ半分ケース、`machineGestures.spec.ts` の入力スロット右クリ）を追随させる
5. **C# ⇔ TS ワイヤ契約**: エラーコード正準セットの追随のみ。
   `WireFixtures/` の契約テストが扱うのはホスト→クライアント payload と `error_codes.json` であり、action（クライアント→ホスト）のフィクスチャ機構は**存在しない**。
   新ハンドラが新しい `ActionResult.Fail` コードを導入する場合に限り、`WireContractTest.cs` の正準セット（手維持 grep 由来）と `error_codes.json` を更新する。
   既存コードの再利用で済むなら本手順の作業は無し。action フィクスチャの新設は本件のスコープ外（やるなら別判断）

## 注意

- uGUI 側の半分掴み実装（`SplitGrabActionHandler` が委譲する先）と数量セマンティクスを変えないこと。狙いは計算場所の統一のみ
- 台帳エントリ: `TODO.md` 2a「品質フォロー」の該当行。着手時はこのドキュメントを正とする
