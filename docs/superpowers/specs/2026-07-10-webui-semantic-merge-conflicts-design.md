# Web UI Semantic Merge Conflicts Design

## Goal

Web UI が旧ゲーム仕様へ依存している5件の意味的競合を、新しい動的インベントリ、動的スタック上限、BlockIdベースの設置仕様へ合わせて解消する。

## Inventory contract

`InventoryAreaMapper` は固定値を所有せず、呼び出し時点の `LocalPlayerInventory.MainSlotCount` を受け取る。メイン領域は `mainSlotCount - PlayerInventoryConst.HotBarSlotCount`、ホットバーは末尾の `HotBarSlotCount`、ブロック側サブインベントリは `mainSlotCount` 以降として変換する。トピック配信とaction入力が同じ値を使うため、研究による拡張後も表示座標と送信先が一致する。

## Item master contract

`/api/master/items` は名前とItemIdを `MasterHolder.ItemMaster` から読み、MaxStackを読み取り専用の `IItemStackLevelLookup.GetMaxStack(ItemId)` から取得する。スタックレベルは実行中に変わるため、レスポンスJSONはキャッシュせず毎回構築する。HTTPの `Cache-Control: no-store` は維持する。

ブラウザ側も初回成功後に取得を停止せず、既存の3秒間隔で再取得を続ける。これにより新しい同期プロトコルや状態複製を作らず、現在値のHTTP APIを単一の情報源としてゲーム中の解放へ追随する。

## Machine recipe and block icon contract

`crafting.machine_recipes` の機械識別子を `blockItemId` から `blockId` に置き換える。C# DTO、TypeScript型、validator、グループ化、タブ状態、fixture/testを同じ契約へ移行する。機械アイコンは新規 `/api/block-icons/{blockId}.png` から取得し、`ClientContext.BlockImageContainer.GetBlockView(new BlockId(id))` のテクスチャをUnityメインスレッドでPNG化する。

Reactには表示専用 `BlockIcon` を追加する。機械画像とタブ画像はBlockIconを使い、機械画像クリックによるItemId選択は削除する。レシピ入出力のItemSlotクリックは維持する。

## Error handling and cache behavior

不正なblockIdパスは404、ゲーム初期化前は503、画像未登録は404とする。ブロックPNGはItemIconと同じETag再検証方式でキャッシュし、WebUiHost停止時に破棄する。動的MaxStack JSONはキャッシュしない。

## Placement and precedent

| Item | Placement | Precedent |
|---|---|---|
| 動的インベントリ座標変換 | `Client.WebUiHost/Game/Actions/Inventory` | `LocalPlayerInventory.MainSlotCount` と `ILocalPlayerInventoryExtension` |
| 動的MaxStack読取 | `Client.WebUiHost/Game/ItemMasterEndpoint` から `Core.Item` の読み取り専用Instanceを使用 | `Client.DebugSystem/ItemGetDebugSheet.cs` |
| ブロック画像HTTP配信 | `Client.WebUiHost/Game/BlockIconEndpoint.cs` | `ItemIconEndpoint.cs` |
| ブロック画像の実体 | `ClientContext.BlockImageContainer` | `BuildMenuEntryCatalog.cs`, uGUI `MachineRecipeView.cs` |
| Web契約と表示 | `moorestech_web/webui/src/bridge/contract`, `features/recipe` | 既存ItemIcon、MachineRecipeView |

`Core.Master` と自動生成された `Mooresmaster.Model.*` は変更しない。新しい状態、通信プロトコル、BlockとItemの対応表は追加しない。

## Verification

- Unity compileで5件のエラーが0件になること。
- `InventoryAreaMapperTest` が拡張後のmain/hotbar境界とblock先頭を検証すること。
- Web UI unit testがBlockIdグループ化とタブ生成を検証すること。
- itemMasterStoreのテストが成功後にも再取得し、更新されたMaxStackを反映すること。
- Web UI build/typecheckがC#契約変更に追随して通ること。
- 残存検索で `blockItemId` とWeb UI内の `PlayerInventoryConst.MainInventorySize` と `ItemMasterElement.MaxStack` が0件になること。
