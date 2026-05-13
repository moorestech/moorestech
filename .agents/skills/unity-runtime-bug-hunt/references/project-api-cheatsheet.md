# Project API Cheatsheet

Step 3 で動的コードを書くときに参照する、プロジェクト固有のエントリーポイント集。対象プロジェクトで検証済みの API・名前・典型パターンを追記していく。未登録のプロジェクトで作業するときは最初にここへエントリを追加する。

## moorestech (client-server統合版)

### プロセス構成: PlayMode では同一プロセス

**重要:** moorestech は client/server が別 Unity プロジェクトだが、**PlayMode 実行中は同一プロセス上で両方動く**。client の Unity Editor に uloop を繋いだ状態で `Game.Context.ServerContext.*` を普通に参照できる。

- `VanillaApi` の field に `_localServerProcess (Process)` があるが、これは製品ビルド時の外部サーバー起動用の仕組みで、**PlayMode では未使用**。名前に惑わされない
- サーバー側の状態（`WorldBlockDatastore`, `GearNetworkDatastore` 等）を client の uloop から直接ダンプできる。別 Unity 起動やサーバーコード改変は不要
- 疑わしいときは probe: `return Game.Context.ServerContext.WorldBlockDatastore.BlockMasterDictionary.Count;` を 1 コール打つ。返れば in-process

### エントリーポイント（ここから World を辿る）

| 用途 | コード |
|---|---|
| ワールド内の全ブロック | `Game.Context.ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values` |
| ブロック→コンポーネント | `block.ComponentManager.TryGetComponent<T>(out var c)`（**`block.TryGetComponent` は存在しない**, Unity の GameObject 混同注意） |
| ブロック座標 | `bwp.BlockPositionInfo.OriginalPos`（**`OriginPos` ではない**, `CS1061` になる） |
| マスターデータ | `Core.Master.MasterHolder.ItemMaster` / `BlockMaster` / `CraftRecipeMaster` 等 |
| サービスプロバイダ | `ServerContext.MainServiceProvider` → `.GetService<IWorldSaveDataLoader>()` 等 |
| プレイヤーインベントリ | `serviceProvider.GetService<IPlayerInventoryDataStore>()` |

### よく使うコンポーネント（ブロック系）

| コンポーネント | namespace | 主フィールド |
|---|---|---|
| `VanillaChestComponent` | `Game.Block.Blocks.Chest` | `InventoryItems`（= `_itemDataStoreService._inventory`） |
| `VanillaBeltConveyorComponent` | `Game.Block.Blocks.BeltConveyor` | `BeltConveyorItems`, `_inventoryItems` |
| `BlockConnectorComponent<IBlockInventory>` | `Game.Block.Component` | `ConnectedTargets` (`IReadOnlyDictionary<IBlockInventory, ConnectedInfo>`) |

### インベントリ中身のダンプパターン

```csharp
var nonEmpty = chest.InventoryItems
    .Select((it, idx) => new { idx, it })
    .Where(x => x.it.Count > 0)
    .Select(x => $"slot={x.idx} id={x.it.Id.AsPrimitive()} cnt={x.it.Count}");
```

`IItemStack.Id` は `ItemId` 構造体。文字列比較には `.AsPrimitive()` を使う。

### 接続先の存在チェック

```csharp
if (mgr.TryGetComponent<BlockConnectorComponent<IBlockInventory>>(out var conn))
{
    sb.AppendLine($"ConnectedTargets.Count={conn.ConnectedTargets.Count}");
    foreach (var t in conn.ConnectedTargets)
        sb.AppendLine($"  -> {t.Key.GetType().Name} self={t.Value.SelfConnector != null} target={t.Value.TargetConnector != null}");
}
```

`ConnectedTargets.Count == 0` は **「このブロックは誰にも接続されていない」** という決定的証拠。アイテム搬出系の不具合ではまずこれを見る。

### バックグラウンドスレッドの存在確認

moorestech では `ServerGameUpdater.StartUpdate` が別スレッドで `GameUpdater.Update()` を 50ms ごとに叩く。このスレッドが死ぬと `BlockSystem.Update()` が一切呼ばれなくなる。

`mcp__rider-debugger__list_threads` → 返却される threads[] 内に `"[moorestech]ゲームアップデートスレッド"` が存在するか確認。無ければ initialization pipeline が更新ループを起動していない。

### よくある名前ミス

| 間違い | 正しい | 理由 |
|---|---|---|
| `BlockPositionInfo.OriginPos` | `OriginalPos` | プロパティ名 |
| `block.TryGetComponent<T>` | `block.ComponentManager.TryGetComponent<T>` | IBlock は Unity の GameObject ではない |
| `itemStack is ItemStack` (BP条件内) | `is Core.Item.Implementation.ItemStack` | internal クラスは FQN 必須 |
| `chest.InventoryItems.Count`（debugger内） | `_itemDataStoreService._inventory._size` | property getter は debugger で評価不可 |

### PlayMode 起動を伴うテストユーティリティ

`Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil` にはPlayModeでのブロック配置・アイテム投入のヘルパーがある。動的コード相当のことを再現テストで書きたい時に参考になる。
