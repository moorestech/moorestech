# ユースケース: ランタイム状態を動的コードで観測する（原因調査の API 早見表）

PlayMode 中の「何が存在していて、どうなっているか」を `uloop execute-dynamic-code` で1コール取るときのエントリーポイント集。
黄金律は **推測で直さず、状態の真実を先に取る**。呼ばれているかの確認は一時 `Debug.Log("[hunt] ...")` を入れ `uloop get-logs --search-text "[hunt]"` で見る（`.cs` 編集の前に必ず PlayMode を停止し、調査後は `[hunt]` を全て除去する）。

## 「見えない」は推論でなく probe の結果として宣言する

`_localServerProcess (Process)` のような field 名を見て「サーバーは別OSプロセスだから見えない」と結論し、動的コードを試さずに諦めた実例がある。実際は PlayMode 中は同一プロセスで `ServerContext.*` が普通に引けた。field 名・型名はデプロイモードごとに意味が変わるので、「この状態は見えない」と言う前に該当エントリーポイントを1回叩く。返れば見える、throw / null なら見えない。

```csharp
var ctx = Game.Context.ServerContext.WorldBlockDatastore;
return ctx == null ? "null" : $"OK count={ctx.BlockMasterDictionary.Count}";
```

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

`execute-dynamic-code` で `Core.Update.GameUpdater.CurrentTick` を数百ms空けて2回読み、進んでいるかで判定する。進まなければ initialization pipeline が更新ループを起動していない。

### よくある名前ミス

| 間違い | 正しい | 理由 |
|---|---|---|
| `BlockPositionInfo.OriginPos` | `OriginalPos` | プロパティ名 |
| `block.TryGetComponent<T>` | `block.ComponentManager.TryGetComponent<T>` | IBlock は Unity の GameObject ではない |
| `itemStack is ItemStack`（動的コード内） | `is Core.Item.Implementation.ItemStack` | internal クラスは FQN 必須 |

### PlayMode 起動を伴うテストユーティリティ

`Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil` にはPlayModeでのブロック配置・アイテム投入のヘルパーがある。動的コード相当のことを再現テストで書きたい時に参考になる。

### セーブをロード・保存せず起動するオプション（バグ調査時は基本これ）

バグ調査の再現性のため、**既存セーブをロードせず・オートセーブもしないクリーンな状態**で PlayMode 起動できる。調査中の世界が既存セーブで汚れたり、調査の操作（ブロック破壊・配置等）が本番セーブを破壊するのを防げる。再現条件が「まっさらな世界から」のときは原則これで起動する。

PlayMode 突入前（EditMode 中）に SessionState フラグを立てる:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'UnityEditor.SessionState.SetBool("moorestech_SkipSaveLoadPlayMode", true); return "set";'
# その後に control-play-mode --action Play
```

`InitializeScenePipeline.ApplySkipSaveLoadModeIfNeeded` がこのフラグを読み、サーバを `AutoSave=false` + 一時 `saveFilePath` で起動する（= 既存セーブ非ロード・非保存）。フラグは PlayMode 終了（EditMode 復帰）時に `NoSaveLoadPlayToolbarElement` が自動クリアするので、毎回 Play 前に立て直す。Unity ツールバーの「NoSave Play」再生ボタンと同じ仕組み。
