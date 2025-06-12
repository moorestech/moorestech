# GameStateManager設計書

## 概要

moorestechクライアントにおける、サーバーから送信されるゲームデータを一元管理するシングルトンの設計案です。この設計は、データの性質に応じて適切なアクセス方式を提供し、効率的なメモリ管理とネットワーク通信を実現します。

## 現状分析

### サーバー側のデータ送信パターン

1. **ポーリング型（クライアント主導）**
   - EventProtocol: 100ms間隔でポーリング
   - プレイヤーごとにイベントキューを管理

2. **イベント駆動型（サーバー主導）**
   - ブロック状態変更、配置、削除
   - インベントリ更新（メイン、グラブ、開いているブロック）

3. **選択的データ送信**
   - BlockInventoryOpenStateDataStoreが開閉状態を管理
   - 開いているプレイヤーのみに詳細データを送信

### クライアント側の現在の実装

- ServerCommunicator: 低レベルソケット通信
- PacketExchangeManager: パケット送受信管理
- VanillaApi: 高レベルAPI（Event、Response、SendOnly）
- 各種DataStore: データ保持・管理

## 新設計：GameStateManager

### 1. 基本アーキテクチャ

```csharp
public sealed class GameStateManager : MonoBehaviour
{
    private static GameStateManager _instance;
    public static GameStateManager Instance => _instance;
    
    // リアルタイムデータへのプロパティアクセス
    public IBlockRegistry Blocks { get; private set; }
    public IPlayerState Player { get; private set; }
    public IEntityRegistry Entities { get; private set; }
    public IGameProgressState GameProgress { get; private set; }
    public IMapObjectRegistry MapObjects { get; private set; }
}
```

### 2. データアクセス方式の設計

#### プロパティ方式（リアルタイムデータ）
常にサーバーからリアルタイムで更新され、常時保持すべきデータ：

```csharp
public interface IBlockRegistry
{
    IReadOnlyBlock GetBlock(Vector3Int position);
    IReadOnlyDictionary<Vector3Int, IReadOnlyBlock> AllBlocks { get; }
}

public interface IReadOnlyBlock
{
    int BlockId { get; }
    Vector3Int Position { get; }
    BlockDirection Direction { get; }
    
    // ブロックタイプ固有のステートを型安全に取得
    T GetState<T>(string stateKey) where T : class;
    
    // 鮮度管理が必要なデータへの非同期アクセス
    UniTask<IBlockInventory> GetInventoryAsync();
}

public interface IPlayerState
{
    Vector3 Position { get; }
    IReadOnlyList<ItemStack> MainInventory { get; }    // MainInventoryUpdateEvent
    ItemStack GrabItem { get; }                         // GrabInventoryUpdateEvent
}

public interface IEntityRegistry
{
    IReadOnlyList<IClientEntity> GetEntities();
    IClientEntity GetEntity(long instanceId);
}

public interface IClientEntity
{
    long InstanceId { get; }
    string EntityType { get; }
    Vector3 Position { get; }
    string State { get; }  // エンティティ固有のステート文字列
}

public interface IGameProgressState
{
    IReadOnlyUnlockState Unlocks { get; }              // ResponseGameUnlockState
    IReadOnlyChallengeState Challenges { get; }         // ResponseChallengeInfo
    IReadOnlyCraftTreeState CraftTree { get; }          // ResponseGetCraftTree
}

public interface IMapObjectRegistry
{
    IReadOnlyMapObject GetMapObject(int instanceId);
    IReadOnlyDictionary<int, IReadOnlyMapObject> AllMapObjects { get; }
}
```

#### メソッド方式（鮮度管理が必要なデータ）
リクエスト時に取得し、一定期間後に破棄されるデータ：

```csharp
public interface IBlockInventory
{
    IReadOnlyList<ItemStack> Items { get; }
    DateTime LastUpdated { get; }                      // 鮮度情報
}

// ブロックステートの型安全な定義例
public class CommonMachineState
{
    public float CurrentPower { get; set; }
    public float RequestPower { get; set; }
    public float ProcessingRate { get; set; }
    public string CurrentStateType { get; set; }
}

public class FluidPipeState
{
    public int FluidId { get; set; }
    public int Amount { get; set; }
    public int Capacity { get; set; }
}
```

### 3. 鮮度管理システム

```csharp
internal class FreshDataCache<TKey, TData>
{
    private readonly Dictionary<TKey, CachedData> _cache;
    private readonly TimeSpan _expiration;
    
    internal class CachedData
    {
        public TData Data { get; set; }
        public DateTime CachedAt { get; set; }
        
        public bool IsFresh => DateTime.UtcNow - CachedAt < _expiration;
    }
    
    public async UniTask<TData> GetOrFetchAsync(
        TKey key, 
        Func<TKey, UniTask<TData>> fetchFunc)
    {
        if (_cache.TryGetValue(key, out var cached) && cached.IsFresh)
        {
            return cached.Data;
        }
        
        var data = await fetchFunc(key);
        _cache[key] = new CachedData { Data = data, CachedAt = DateTime.UtcNow };
        return data;
    }
}
```

### 4. 実装例：インベントリシステムとの統合

```csharp
public class BlockInventoryUI
{
    public async UniTask ShowInventory(Vector3Int blockPos)
    {
        // ブロックを取得（プロパティアクセス）
        var block = GameStateManager.Instance.Blocks.GetBlock(blockPos);
        
        // インベントリデータを非同期で取得（メソッドアクセス）
        var inventory = await block.GetInventoryAsync();
        
        // UIに反映
        UpdateUI(inventory.Items);
        
        // イベント購読（リアルタイム更新のみ）
        VanillaApi.Event.Subscribe<OpenableBlockInventoryUpdateEvent>(
            tag: "va:event:openableBlockInventoryUpdate",
            onEvent: (e) => {
                if (e.Position == blockPos)
                {
                    UpdateSlot(e.SlotIndex, e.Item);
                }
            }
        );
    }
}
```

### 5. BlockInventoryOpenStateDataStore廃止について

現在のサーバー実装では、`BlockInventoryOpenStateDataStore`がプレイヤーごとにどのブロックのインベントリを開いているかを管理し、開いているプレイヤーにのみ更新を送信する仕組みになっています。

新設計では、この仕組みを廃止し、以下のように変更します：

1. **クライアントからのリクエストベース**：インベントリデータが必要な時にクライアントから明示的にリクエスト
2. **キャッシュによる最適化**：クライアント側で鮮度管理を行い、頻繁なリクエストを抑制
3. **プロトコルの統一**：`OpenableBlockInventoryUpdateEvent`を廃止し、`BlockInventoryUpdateProtocol`として通常のPacketResponseパターンに統一

この変更により、サーバー側の実装がシンプルになり、クライアント側で柔軟なデータ管理が可能になります。

## 移行計画

### フェーズ0: サーバー側の準備（最優先）
1. `BlockInventoryOpenStateDataStore`の廃止
2. `BlockInventoryOpenCloseProtocol`の廃止  
3. `OpenableBlockInventoryUpdateEventPacket`を`BlockInventoryUpdateProtocol`に置き換え
4. ブロックインベントリのリクエストベースAPI実装

### フェーズ1: GameStateManager基盤構築
1. GameStateManagerのシングルトン実装
2. プロパティアクセス用インターフェース定義
3. 既存DataStoreのラップ実装

### フェーズ2: VanillaApiとの統合
1. **VanillaApiの位置づけ**
   - GameStateManagerの内部で低レベル通信層として使用
   - 外部からの直接アクセスを段階的に廃止
   - `ClientContext.VanillaApi`への参照をGameStateManager経由に移行

2. **データフローの統合**
   - **初期化フロー**:
     - GameStateManagerは汎用的なデータ管理基盤を提供
     - 各DataStoreが自身の責任でGameStateManagerからデータを取得
     - DataStore側でゲーム固有のロジックを実装
   
   - **イベント購読の一元化**:
     - GameStateManagerが汎用的なイベント配信メカニズムを提供
     - 各DataStoreがGameStateManagerのイベントストリームを購読
     - データの解釈と処理は各DataStore側で実装
   
   - **送信処理**:
     - `VanillaApiSendOnly`は現状のまま維持
     - 各コンポーネントが必要に応じて直接使用

3. **条件付きデータ管理の実装**
   - インベントリ開閉状態の管理をGameStateManager内で実装
   - アクティブ/非アクティブデータの切り替えロジック
   - メモリ効率を考慮した自動解放機能

### フェーズ3: 鮮度管理システム
1. `FreshDataCache`の実装
2. ブロックインベントリの非同期取得実装
3. キャッシュ有効期限の調整

### フェーズ4: 段階的移行

#### 4.1 VanillaApi依存クラスの移行

**Response依存**:
- InitializeScenePipeline → GameStateManager初期化に統合
- WorldDataHandler → GameStateManagerの内部実装に移行
- BlockInventoryState → IReadOnlyBlock.GetInventoryAsync()を使用
- CraftTreeViewManager → IGameProgressState.CraftTreeを使用
- ChallengeManager → IGameProgressState.Challengesを使用

**SendOnly依存**: （今回のリファクタリング対象外）

**Event依存**:
- BlockStateEventHandler → GameStateManager内部に統合
- NetworkEventInventoryUpdater → GameStateManagerのイベントハンドラに移行
- ClientGameUnlockStateData → IGameProgressStateの内部実装に移行

#### 4.2 BlockStateEventHandler依存の解消
- BlockGameObjectDataStoreへの直接アクセスをGameStateManager経由に変更
- IBlockStateChangeProcessor実装クラスをGameStateManagerのイベントシステムに移行

#### 4.3 旧DataStoreの廃止
- BlockGameObjectDataStore
- EntityObjectDatastore  
- MapObjectGameObjectDatastore
- ClientGameUnlockStateData

## 技術的考慮事項

- **UniRx**: リアルタイムデータ更新の通知にObservableパターンを活用
- **UniTask**: 非同期データ取得メソッドの実装
- **MessagePack**: 既存のシリアライゼーション形式を維持
- **DIコンテナ**: GameStateManagerとDataStoreの依存関係管理

## コンパイル確認

各フェーズの実装後には以下のコマンドでコンパイルを確認します：

- サーバー側：`./unity-test.sh moorestech_server 'BlockInventory|GameState'`
- クライアント側：`./unity-test.sh moorestech_client '^0'`

## まとめ

この設計により、以下が実現されます：

1. **直感的なAPI**: データの性質に応じたアクセス方式（プロパティ/メソッド）
2. **効率的なデータ管理**: 鮮度管理による適切なキャッシュ戦略
3. **シンプルな実装**: BlockInventoryOpenStateDataStoreの廃止による簡潔化
4. **段階的移行**: 既存システムからの低リスクな移行パス

## 実装状況

### 完了したフェーズ
- **フェーズ0**: サーバー側の準備 ✅
- **フェーズ1**: GameStateManager基盤構築 ✅


## 実装時の重要参照先

### ドキュメント
- `/Users/katsumi.sato/moorestech/CLAUDE.md` - プロジェクトの開発規約とコーディング規則
- `/Users/katsumi.sato/moorestech/memory-bank/` - プロジェクトコンテキストとパターン

### サーバー側の主要ファイル
- `/moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/` - プロトコル定義
- `/moorestech_server/Assets/Scripts/Server.Event/EventReceive/` - イベント定義

### クライアント側の主要ファイル
- `/moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs` - 現在のAPI実装
- `/moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/BlockStateEventHandler.cs` - 移行対象
- `/moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/BlockGameObjectDataStore.cs` - 統合対象
- `/moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/EntityObjectDatastore.cs` - 統合対象
- `/moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/View/BlockInventoryState.cs` - インベントリUI実装

### MessagePack関連
- `/moorestech_server/Assets/Scripts/Server.Util/MessagePack/` - シリアライズ定義
- `/moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateDetail/` - ブロック状態の型定義

### テスト実行
- `./unity-test.sh` - テスト実行スクリプト
- `./unity-compile.sh` - コンパイル確認スクリプト