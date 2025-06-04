# System Patterns

## 全体アーキテクチャ

moorestechは、クライアント・サーバーアーキテクチャを採用しています。サーバーがゲームロジックを処理し、クライアントがユーザーインターフェースとプレイヤー操作を担当します。

```
+-------------------+        +-------------------+
|                   |        |                   |
|  moorestech_client|<------>|  moorestech_server|
|  (Unity)          |  TCP   |  (Unity)          |
|                   |        |                   |
+-------------------+        +-------------------+
```

## サーバーアーキテクチャ

サーバーは、以下のような階層構造を持っています：

```
+---------------------------------------------------------------+
|                         Server.Boot                           |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|                        Server.Protocol                        |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|                          Game.World                           |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|  Game.Block  |  Game.Entity  |  Game.PlayerInventory  | ...   |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|    Core.Item    |    Core.Inventory    |    Core.Master    |  |
+---------------------------------------------------------------+
```

### 主要コンポーネント

- **Server.Boot**: サーバーの起動処理を担当します。DIコンテナの設定、マップのロード、MODのロードなどを行います。
- **Server.Protocol**: クライアントとの通信プロトコルを担当します。パケットの送受信、シリアライズ/デシリアライズなどを行います。
- **Game.World**: ゲームワールド全体を管理します。ブロックの配置、エンティティの管理などを行います。
- **Game.Block**: ブロック関連の機能を提供します。ブロックの種類、状態、コンポーネントなどを管理します。
- **Game.Entity**: エンティティ関連の機能を提供します。エンティティの種類、状態、動作などを管理します。
- **Game.PlayerInventory**: プレイヤーインベントリ関連の機能を提供します。インベントリの管理、アイテムの操作などを行います。
- **Core.Item**: アイテム関連の基本機能を提供します。アイテムの種類、スタック、メタデータなどを管理します。
- **Core.Inventory**: インベントリ関連の基本機能を提供します。アイテムの保存、取得、操作などを行います。
- **Core.Master**: マスターデータ関連の機能を提供します。アイテム、ブロック、レシピなどのマスターデータを管理します。

## クライアントアーキテクチャ

クライアントは、以下のような階層構造を持っています：

```
+---------------------------------------------------------------+
|                       Client.Starter                          |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|                       Client.Network                          |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|                         Client.Game                           |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|  Client.Game.InGame.Block  |  Client.Game.InGame.UI  |  ...   |
+---------------------------------------------------------------+
```

### 主要コンポーネント

- **Client.Starter**: クライアントの起動処理を担当します。DIコンテナの設定、シーンのロード、初期化などを行います。
- **Client.Network**: サーバーとの通信を担当します。パケットの送受信、シリアライズ/デシリアライズなどを行います。
- **Client.Game**: ゲーム全体を管理します。ゲームの状態、シーケンス、イベントなどを管理します。
- **Client.Game.InGame.Block**: ブロック関連のUI機能を提供します。ブロックの表示、操作などを行います。
- **Client.Game.InGame.UI**: UI関連の機能を提供します。インベントリUI、メニューUI、ステータスUIなどを管理します。

## 通信アーキテクチャ

クライアントとサーバー間の通信は、MessagePackを使用して行われます。通信は以下のように行われます：

1. クライアントがサーバーに接続します（`ServerCommunicator.CreateConnectedInstance`）。
2. クライアントがサーバーにリクエストを送信します（`PacketSender.Send`）。
3. サーバーがリクエストを処理し、レスポンスを返します。
4. クライアントがレスポンスを受信し、処理します（`PacketExchangeManager.ExchangeReceivedPacket`）。

## デザインパターン

### 依存性注入（DI）パターン

moorestechでは、依存性注入（DI）パターンを採用しています。サーバー側では`Microsoft.Extensions.DependencyInjection`を、クライアント側では`VContainer`を使用しています。

### コンポーネントパターン

ブロックやエンティティなどのゲームオブジェクトは、コンポーネントの集合として実装されています。コンポーネントは単一の機能を提供し、他のコンポーネントと協調して動作します。

```
+---------------------------------------------------------------+
|                           IBlock                              |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|                    BlockComponentManager                      |
+---------------------------------------------------------------+
                               |
                               v
+---------------------------------------------------------------+
|  IBlockInventory  |  IBlockConnector  |  IUpdatableBlock  |   |
+---------------------------------------------------------------+
```

### イベント駆動型アーキテクチャ

コンポーネント間の通信には、イベント駆動型のアプローチを使用しています。イベントの発行と購読には、UniRxの`IObservable`と`IObserver`を使用しています。

```csharp
// イベントの発行
private readonly Subject<BlockState> _blockStateChange = new();
public IObservable<BlockState> BlockStateChange => _blockStateChange;

// イベントの発火
_blockStateChange.OnNext(new BlockState(stateDetails));

// イベントの購読
blockStateChange.Subscribe(OnBlockStateChange);
```

### ファクトリーパターン

アイテムやブロックの作成には、ファクトリーパターンを使用しています。

```csharp
// アイテムの作成
var itemStack = itemStackFactory.Create(itemId, count);

// ブロックの作成
var block = blockFactory.CreateBlock(blockId, position);
```

### シングルトンパターン

マスターデータやデータストアなどのグローバルなサービスには、シングルトンパターンを使用しています。

```csharp
// マスターデータの取得
var itemMaster = MasterHolder.ItemMaster;

// データストアの取得
var worldDataStore = modEntryInterface.GetService<IWorldDataStore>();
```

## セーブ・ロードシステムのパターン

### コンストラクタベースのロードパターン

moorestechでは、セーブデータのロード時に状態変更メソッド（AddLiquid, SetItemなど）を避け、コンストラクタで直接プロパティを設定する設計パターンを採用しています。これにより、意図しないOnNextイベントの発行を防ぎます。

```csharp
// 良い例：コンストラクタでセーブデータを受け取り、直接プロパティを設定
public FluidPipeComponent(BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IFluidInventory> connectorComponent, float capacity, Dictionary<string, string> componentStates)
{
    _fluidContainer = new FluidContainer(capacity);
    
    // セーブデータがある場合は直接プロパティを設定
    if (componentStates != null && componentStates.TryGetValue(typeof(FluidPipeSaveComponent).FullName, out var savedState))
    {
        var jsonObject = JsonConvert.DeserializeObject<FluidPipeSaveJsonObject>(savedState);
        _fluidContainer.FluidId = jsonObject.FluidId;
        _fluidContainer.Amount = jsonObject.Amount;
    }
}

// 悪い例：ロード後にメソッドを呼び出す（OnNextイベントが発生してしまう）
// component.AddLiquid(savedFluidId, savedAmount); // 避けるべき
```

### BlockTemplateのGetBlockパターン

BlockTemplateでは、NewメソッドとLoadメソッドが同じGetBlockメソッドを呼び出し、componentStatesの有無で処理を分岐させるパターンを使用します。

```csharp
public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
{
    return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
}

public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
{
    return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
}

private IBlock GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
{
    // componentStatesの有無で新規作成かロードかを判断
    // コンポーネントの生成時にcomponentStatesを渡す
}
```

### 流体（Fluid）システムのセーブ・ロード

流体データのセーブ・ロードでは、FluidIdとAmountを保存し、ロード時は直接これらのプロパティを設定します。

```csharp
// BlockTemplateUtilでの流体ロード例
if (jsonObject.InputFluidSlot != null)
{
    for (var i = 0; i < jsonObject.InputFluidSlot.Count && i < vanillaMachineInputInventory.FluidInputSlot.Count; i++)
    {
        var fluidData = jsonObject.InputFluidSlot[i];
        // 直接プロパティを設定（AddLiquidメソッドは使用しない）
        vanillaMachineInputInventory.FluidInputSlot[i].FluidId = fluidData.FluidId;
        vanillaMachineInputInventory.FluidInputSlot[i].Amount = fluidData.Amount;
    }
}