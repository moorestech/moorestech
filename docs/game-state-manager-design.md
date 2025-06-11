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
    IReadOnlyDictionary<string, byte[]> State { get; }  // BlockStateMessagePack
    指摘：GameStateManager側でシリアライズまでやりたいですね。クライアント側で使ってるところを調査して、シリアライズ方法を検討してください。
}

public interface IPlayerState
{
    Vector3 Position { get; }
    IReadOnlyList<ItemStack> MainInventory { get; }    // MainInventoryUpdateEvent
    ItemStack GrabItem { get; }                         // GrabInventoryUpdateEvent
}

public interface IEntityRegistry
{
    IReadOnlyList<IEntity> GetEntities();
    指摘：IEntityはサーバー側のEntityを表すインターフェースなので、クライアント側で再定義してください。
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
public interface IBlock : IReadOnlyBlock
{
    // 鮮度管理が必要なデータへの非同期アクセス
    UniTask<IBlockInventory> GetInventoryAsync();      // BlockInventoryResponseProtocol
    指摘：これもIReadOnlyBlockの中に入れておいて
}

public interface IBlockInventory
{
    IReadOnlyList<ItemStack> Items { get; }
    DateTime LastUpdated { get; }                      // 鮮度情報
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
3. **イベントは全プレイヤーに送信**：`OpenableBlockInventoryUpdateEvent`は全プレイヤーに送信し、クライアント側でフィルタリング
指摘：OpenableBlockInventoryUpdateEventを廃止し、通常のPacketResponseと同じように新しくプロトコルを作成してください。

この変更により、サーバー側の実装がシンプルになり、クライアント側で柔軟なデータ管理が可能になります。

## 移行計画

### フェーズ0: サーバー側の準備（最優先）
1. `BlockInventoryOpenStateDataStore`の廃止
2. `BlockInventoryOpenCloseProtocol`の廃止
3. `OpenableBlockInventoryUpdateEventPacket`を全プレイヤーに送信するよう変更
4. ブロックインベントリのリクエストベースAPI実装

### フェーズ1: GameStateManager基盤構築
1. GameStateManagerのシングルトン実装
2. プロパティアクセス用インターフェース定義
3. 既存DataStoreのラップ実装

### フェーズ2: 鮮度管理システム
1. `FreshDataCache`の実装
2. ブロックインベントリの非同期取得実装
3. キャッシュ有効期限の調整

### フェーズ3: 段階的移行
1. BlockInventoryUIをGameStateManager経由に移行
2. その他のUIコンポーネントを順次移行
3. 旧DataStoreの廃止
指摘：クライアント側でVanullaApiに依存していたり、現在のBlockStateEventHandlerに依存している部分を全て調査し、載せ替えが必要な部分を洗い出してください。

## 技術的考慮事項

- **UniRx**: リアルタイムデータ更新の通知にObservableパターンを活用
- **UniTask**: 非同期データ取得メソッドの実装
- **MessagePack**: 既存のシリアライゼーション形式を維持
- **DIコンテナ**: GameStateManagerとDataStoreの依存関係管理

## まとめ

この設計により、以下が実現されます：

1. **直感的なAPI**: データの性質に応じたアクセス方式（プロパティ/メソッド）
2. **効率的なデータ管理**: 鮮度管理による適切なキャッシュ戦略
3. **シンプルな実装**: BlockInventoryOpenStateDataStoreの廃止による簡潔化
4. **段階的移行**: 既存システムからの低リスクな移行パス

指摘：コンパイルが通ることを確認するために、