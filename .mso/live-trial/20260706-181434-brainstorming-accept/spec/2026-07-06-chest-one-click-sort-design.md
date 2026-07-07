# チェスト専用ワンクリックソート機能 設計書

作成日: 2026-07-06

## 1. 目的

チェスト（Chest）UIに専用のソートボタンを追加し、**そのチェストの中身だけ**をワンクリックで
種類ごとに自動整列できるようにする。メインインベントリには一切影響を与えない。

## 2. 前提（調査で確定した事実）

本機能はサーバー側の整列機構が既に完成しているため、追加実装はクライアントUIに限定される。

- **整列ロジックは実装済み**: `Server.Protocol/PacketResponse/Util/InventoryService/InventorySortService.cs` の
  `Sort()` が「同種スタック結合 → `ItemId` 昇順で詰め直し」を行う。マスタの `ItemId` は `SortPriority` 順で
  採番されるため、`ItemId` 昇順 = 種類順の整列になる。
- **サーバープロトコルも実装済み**: `SortInventoryProtocol`（タグ `va:sortInventory`）が対象インベントリを
  指定して整列できる。チェストは `InventoryType.Block` として対応済みで、`SortInventoryProtocolTest` の
  BlockInventory ケースで検証済み。**サーバー変更は不要**。
- **整列結果の反映も既存同期で完結**: `VanillaChestComponent` の更新イベント → `SubscribeInventoryProtocol`
  経由でクライアントUIに自動反映される。**新プロトコル・新イベントは不要**。
- **クライアント送信APIも既存**: `ClientContext.VanillaApi.SendOnly.SortInventory(InventoryIdentifierMessagePack)`。
- **既存のグローバル整理ボタン**（`PlayerInventoryViewController.sortInventoryButton`）は「メイン＋開いている
  サブインベントリ」を両方整列する。本機能はそれとは別に、チェスト枠に「そのチェストのみ」を整列する
  専用ボタンを設ける（発見性の向上とメイン非破壊が狙い）。

## 3. スコープ

- 対象: チェスト（`ChestBlockInventoryView` / `IChestParam` を持つブロック）のみ。
- 非対象: 機械など他のインベントリ持ちブロックへの横展開は行わない（YAGNI）。将来必要になった場合は
  配線を共通基底 `CommonBlockInventoryViewBase` へ昇格させるリファクタで対応する。

## 4. データフロー

```
[チェストUIのソートボタン] クリック
  → ChestBlockInventoryView が ISubInventoryIdentifier.ToMessagePack() で自分の識別子を生成
  → ClientContext.VanillaApi.SendOnly.SortInventory(識別子)   … 既存API・送信のみ
  → サーバー: SortInventoryProtocol(va:sortInventory) が Target(=Block) を解決
  → InventorySortService.Sort() が スタック結合 + ItemId昇順で詰め直し
  → VanillaChestComponent の更新イベント → SubscribeInventoryProtocol でクライアントUIに自動反映
```

チェストの識別子のみを渡すため、メインインベントリは一切ソートされない。

## 5. 変更点

### 5.1 コード変更（唯一）: `ChestBlockInventoryView.cs`

`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/ChestBlockInventoryView.cs`

- `[SerializeField] private Button sortButton;` を追加（`[SerializeField]` はアンダースコア無し小文字キャメルケース）。
- `Initialize()` 内で `base.Initialize()`（＝ `ISubInventoryIdentifier` の確定）の**後**に、ボタンの
  クリックリスナーを配線する:

  ```csharp
  // ソートボタン押下でこのチェストのみを整列する
  // Clicking the sort button tidies only this chest.
  sortButton.onClick.AddListener(() =>
      ClientContext.VanillaApi.SendOnly.SortInventory(ISubInventoryIdentifier.ToMessagePack()));
  ```

  `ToMessagePack()` は `ISubInventoryIdentifier` の拡張メソッド（`ISubInventoryIdentifierExtension.cs`）で、
  `SortInventory` が要求する `InventoryIdentifierMessagePack` を返す。
- クリック時点で `ISubInventoryIdentifier` プロパティを読むため、識別子未確定時の参照問題は起きない。
- 日英2行セットコメントを付与。ファイルは200行以内に収まる（現状42行）。

### 5.2 Prefab変更（Unity Editor 経由でのみ実施）: `ChestBlockInventory.prefab`

`moorestech_client/Assets/AddressableResources/UI/Block/ChestBlockInventory.prefab`

- チェストパネルにボタンを1個追加し、`ChestBlockInventoryView.sortButton` に割り当てる。
- 見た目・アイコンは既存グローバル整理ボタン（`sortInventoryButton`）に合わせる。
  配置はチェストスロット群の上部（ヘッダ右）を既定とする。
- **prefab はテキスト直接編集禁止**のため、`uloop execute-dynamic-code` で Unity にシリアライズさせる
  正規ルートで変更する。

### 5.3 サーバー変更

なし。既存 `SortInventoryProtocol` がチェストに対応済み。

## 6. エッジケース（すべて既存ロジックが処理済み）

- 空チェスト / 一部だけ埋まったチェスト → `InventorySortService` が正常に詰め直す。
- 同種アイテムのスタック結合・上限超過分の繰り越し → `MergeItem` で処理
  （`SortInventoryProtocolTest` の StackOverflowMerge で検証済み）。
- チェストには除外スロット（`ISortExcludedSlots`）が無いため全スロットが整列対象で問題なし。

## 7. テスト方針

- サーバー整列ロジックは既存テストで担保済み。新規サーバーテストは追加しない。
- クライアントのUI配線は Unity Editor 依存が強いため、自動テストは追加せず、**PlayMode で実チェストを
  開いてボタン押下 → 整列を目視確認**することを検証手段とする（YAGNI）。

## 8. 実装手順（概要）

1. `ChestBlockInventoryView.cs` に `sortButton` フィールドとクリック配線を追加。
2. `uloop compile --project-path ./moorestech_client` でコンパイル確認。
3. `uloop execute-dynamic-code` で `ChestBlockInventory.prefab` にボタンを追加し `sortButton` に割り当て。
4. PlayMode でチェストを開き、ボタン押下で中身のみが種類順に整列することを目視確認。
