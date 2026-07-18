# 申し送り: ブロック側「半分掴み」のホスト計算統一（block_inventory.split 追加）

> **✅ 2026-07-07 実装完了**。本ドキュメントは経緯記録として保管。実装内容は末尾「実装完了記録」参照。

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

## 検証記録（2026-07-07 実コード裏取り済み）

本ドキュメントの全主張を実コードと突き合わせて検証済み。行番号は 2026-07-07 時点（ドリフトし得るのでシンボル名で探すこと）。

- 分裂の記述: `blockSlotPlan.ts` `planBlockRightClick`（床計算+`block_inventory.move_item`・分岐点コメント実在）/ `playerSlotPlan.ts` `planPlayerRightClick`（count なし `inventory.split`）で一致
- C# 側: `SplitGrabActionHandler`（`InventoryActions.cs`、ActionType `"inventory.split"`、`item.Count / 2` の整数除算=床）、登録は `WebUiGameBinder.cs` の `hub.RegisterAction(...)`。`BlockInventoryActions.cs` は記載パスに実在し `BlockAreaSlotParser` を使用
- プロトコル: `protocol.ts` に `ActionPayloads` / `ACTION_TYPES`（`satisfies`）/ `ActionTypesExhaustive`（never 制約）が実在、網羅チェックの説明は正確
- テスト: `blockSlotPlan.test.ts` 実在。e2e は `e2e/tests/block/` 配下（`blockInventoryGestures.spec.ts` は Wood×7→count:3 を `toContainEqual` で payload 等値照合し grab-overlay 表示も検証、`machineGestures.spec.ts` に入力スロット右クリケース実在）
- 検証で発見・訂正済みの誤り2件（訂正コミット `dd319bf40`）:
  1. 手順3の旧記述「既存 `inventory.split` 分岐が手本」→ mock-host に split 適用は存在しなかった（現手順3が正）
  2. 手順5の旧記述「契約テストに新 action を追加」→ action フィクスチャ機構は存在しない（現手順5のエラーコード追随のみが正）

## 実装完了記録（2026-07-07）

手順1〜4を実装、手順5は予告どおり作業なしで完了（新エラーコード導入なし。`invalid_payload` / `invalid_slot` / `grab_not_empty` / `empty_slot` は全て既存正準セット内）。

- **C#**: `BlockInventoryActions.cs` に `BlockSplitGrabActionHandler` を追加（block スロット限定・`item.Count / 2` の床関数は `SplitGrabActionHandler` と同一）。`WebUiGameBinder.cs` で登録
- **プロトコル**: `protocol.ts` に `"block_inventory.split": { from: BlockSlotRef }` を追加（`ACTION_TYPES` も追随、網羅チェック通過）
- **mock-host**: `inventoryModel.ts` に `applyBlockSplit`（grab_not_empty / empty_slot / 1個成功no-op をホスト同型で再現）、`wsHandler.ts` に適用分岐を追加
- **プランナ**: `planBlockRightClick` の client 床計算を廃止し count なし `block_inventory.split` 送信へ切替。分岐点コメント削除（player 側との分裂解消）
- **挙動変更1点**: 空手＋1個スロットの右クリックは従来 client 側で無操作だったが、player 側と同じく split を送りホストが no-op 判断する形に統一（unit テストもこの期待に更新）
- **検証**: webui unit 144件 / e2e 44件 / C# `Client.Tests.WebUi` 57件 全パス、Unity コンパイルエラー0
