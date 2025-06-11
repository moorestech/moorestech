# GameStateManager設計書

## 概要

moorestechクライアントにおける、サーバーから送信されるゲームデータを一元管理するシングルトンの設計案です。この設計は、既存のインベントリ開閉システムの分析を基に、効率的なメモリ管理とネットワーク通信を実現します。

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
    
    // 常時保持データ（Core Data）
    public ICoreGameState Core { get; private set; }
    
    // 条件付きデータ（Active Data）
    public IActiveDataManager Active { get; private set; }
    
    // データ購読管理
    public IDataSubscriptionManager Subscriptions { get; private set; }
}
```

### 2. データの階層化

#### 常時保持データ（メモリ常駐）
```csharp
public interface ICoreGameState
{
    IReadOnlyPlayerState Player { get; }           // プレイヤー基本情報
    IReadOnlyBlockRegistry Blocks { get; }         // ブロック配置情報（座標のみ）
    IReadOnlyUnlockState UnlockState { get; }      // アンロック状態
    IReadOnlyWorldInfo World { get; }              // ワールド基本情報
}
```

指摘：上記の具体的なデータについては適切ではないので、 /Users/katsumi.sato/moorestech/moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse と /Users/katsumi.sato/moorestech/moorestech_server/Assets/Scripts/Server.Event/EventReceive以下の全てのCSファイルを確認し、どのデータが有るのか網羅的に全てチェックし、このinterfaceの構造を再検討してください。

#### アクティブデータ（必要時のみ保持）
```csharp
public interface IActiveDataManager
{
    // ブロックの詳細状態（開いているインベントリ等）
    Task<IBlockDetailedState> ActivateBlockDetails(Vector3Int position);
    void DeactivateBlockDetails(Vector3Int position);
    
    // 範囲内のエンティティ（視界内のみ）
    Task<IEntityCollection> ActivateEntitiesInRange(Vector3 center, float radius);
    void DeactivateEntitiesInRange();
    
    // 特定のマップオブジェクト詳細
    Task<IMapObjectDetails> ActivateMapObjectDetails(int objectId);
    void DeactivateMapObjectDetails(int objectId);
}
```
指摘：思想として、IActiveDataManager、ICoreGameStateというのは持たせず、「ブロック」の概念の中に全てが内包されているイメージにしたいです。そちらのほうが直感的なので。ただし、ここでアクティブデータとしてカテゴライズされているデータは「鮮度」が大事です。なので、一定フレーム後、データを破棄し、再度アクセスして取得できるようにしたいです。
このとき、データの取得はプロパティ方式（GameStateManager.Instance.Blocks.GetBlock(new Vector3Int(0,0,0)).Inventory)ではなく、メソッド方式（async UniTask GetBlockInventory() ）のようにしたいです。これは、インベントリがほしい側はとりあえずasyncでただけばいい感じにデータが送られてきて、データが有効期限内である場合は即座にオンメモリからデータを返せばいいからです。あ、でもそうなると、Subscribeの概念はもはや不要ですね。そもそも、BlockInventoryOpenStateDataStoreという概念もやめて、純粋にクライアントからブロックインベントリのリクエストがあったときにそのデータを返すようにしましょう。送信データが増える懸念はありますが、それは最適化のときに再度検討することにします。このBlockInventoryOpenStateDataStoreの内容も計画書に書いておいてください。また、この変更は計画の一番最初にやりたいです。

### 3. 購読管理システム

```csharp
public interface IDataSubscriptionManager
{
    // 条件付き購読
    IDisposable SubscribeWhen<T>(
        Func<bool> condition,           // 購読条件
        Action<T> onData,              // データ受信時の処理
        string subscriptionTag          // 購読識別子
    );
    
    // 範囲ベースの購読
    IDisposable SubscribeInRange(
        Vector3 center,
        float radius,
        Action<IEnumerable<EntityUpdate>> onUpdate
    );
    
    // 状態変更の追跡
    IObservable<DataActivationEvent> OnDataActivated { get; }
    IObservable<DataDeactivationEvent> OnDataDeactivated { get; }
}
```
指摘：上記指摘の通り、この購読管理システムは不要です。

### 4. 実装例：インベントリシステムとの統合

```csharp
public class BlockInventoryManager
{
    private IDisposable _inventorySubscription;
    private IBlockDetailedState _currentBlockDetails;
    
    public async Task OpenInventory(Vector3Int blockPos)
    {
        // アクティブデータとして詳細情報を要求
        _currentBlockDetails = await GameStateManager.Instance.Active
            .ActivateBlockDetails(blockPos);
        
        // 更新の購読開始
        _inventorySubscription = GameStateManager.Instance.Subscriptions
            .SubscribeWhen<InventoryUpdateEvent>(
                condition: () => IsInventoryOpen,
                onData: (update) => UpdateInventoryUI(update),
                subscriptionTag: $"BlockInventory_{blockPos}"
            );
        
        // サーバーに開いたことを通知
        await VanillaApi.SendOnly.SetOpenCloseBlock(blockPos, true);
    }
    
    public async Task CloseInventory(Vector3Int blockPos)
    {
        // 購読解除
        _inventorySubscription?.Dispose();
        
        // アクティブデータを解放
        GameStateManager.Instance.Active.DeactivateBlockDetails(blockPos);
        
        // サーバーに閉じたことを通知
        await VanillaApi.SendOnly.SetOpenCloseBlock(blockPos, false);
        
        _currentBlockDetails = null;
    }
}
```
指摘：このコードもOpenCloseの概念がなくなるので不要です。

### 5. メモリ管理戦略

```csharp
internal class ActiveDataCache
{
    private readonly Dictionary<string, CachedData> _cache;
    private readonly int _maxCacheSize;
    private readonly TimeSpan _cacheExpiration;
    
    #region Internal
    
    class CachedData
    {
        public object Data { get; set; }
        public DateTime LastAccessed { get; set; }
        public int ReferenceCount { get; set; }
    }
    
    void EvictUnusedData()
    {
        var now = DateTime.UtcNow;
        var toEvict = _cache
            .Where(kvp => kvp.Value.ReferenceCount == 0 && 
                         now - kvp.Value.LastAccessed > _cacheExpiration)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in toEvict)
        {
            _cache.Remove(key);
        }
    }
    
    #endregion
}
```
指摘：これは何目的のものですかね？
いったん不要なので消してください。

## 主な利点

1. **メモリ効率**
   - 必要なデータのみメモリに保持
   - 自動的なキャッシュ管理とガベージコレクション

2. **ネットワーク効率**
   - 条件付き購読により必要な通信のみ実行
   - サーバーと同期した選択的データ受信

3. **拡張性**
   - 新しいアクティブデータタイプの追加が容易
   - 購読条件のカスタマイズが可能

4. **パフォーマンス**
   - 頻繁にアクセスされるデータは高速アクセス可能
   - 不要なデータ処理を削減

指摘：純粋な計画書なのでこの項目は不要

## 移行計画

### フェーズ1: 基盤構築
1. GameStateManagerの基本構造を実装
2. 既存のDataStoreをラップするアダプター作成
3. CoreGameStateの実装

### フェーズ2: アクティブデータ管理
1. ActiveDataManagerの実装
2. メモリキャッシュシステムの構築
3. インベントリシステムでのパイロット実装

### フェーズ3: 購読システム
1. DataSubscriptionManagerの実装
2. 条件付き購読の実装
3. 既存のイベントシステムとの統合

### フェーズ4: 段階的移行
1. 既存コンポーネントを新APIに移行
2. パフォーマンステストと最適化
3. 古いDataStoreの段階的廃止

指摘：計画変更に合わせて再考してください。

## 技術的考慮事項

- UniRxとの統合によるリアクティブプログラミング
- UniTaskによる非同期処理
- MessagePackによる効率的なシリアライゼーション
- DIコンテナとの適切な統合

## まとめ

この設計により、moorestechクライアントは効率的なデータ管理システムを持つことになり、メモリ使用量の削減、ネットワーク帯域の最適化、そして開発者にとって使いやすいAPIを提供します。