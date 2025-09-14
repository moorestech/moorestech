# 設計ドキュメント

## 概要
本ドキュメントは、Moorestechゲームにおける研究システムの技術設計を定義します。既存のChallengeDatastore、GameUnlockStateDataController、MasterHolderなどの実装パターンに従い、一貫性のあるアーキテクチャで設計します。

### 重要な設計方針
- **アクション実行の共通化**: 新規にGameActionExecutorクラスを作成し、ChallengeDatastoreと研究システムの両方が利用
- **データ構造の共通化**: ChallengeActionElement型を共通のアクション定義として使用
- **ワールド単位の管理**: 研究進捗はプレイヤーごとではなくワールド単位で管理

## 要件マッピング

### 設計コンポーネントと要件の対応
- **ResearchDataStore** → 要件1: 研究完了状態の管理（ワールド単位）
- **ResearchMaster** → 要件2: 前提条件の検証
- **GameActionExecutor** → 要件5: アクション実行システム（共通化）
- **アイテム消費処理** → 要件3: アイテム消費の処理
- **プロトコル実装** → 要件4: 研究完了プロトコル

## アーキテクチャ概要

### システム構成
研究システムは以下のコンポーネントで構成され、既存システムと統合されます：

1. **マスターデータ層**: ResearchMasterによる研究定義の管理
2. **データ層**: ワールド単位での研究進捗データの管理と永続化
3. **ビジネスロジック層**: 研究完了条件の検証とアクション実行
4. **アクション実行層**: GameActionExecutorによる共通アクション処理
5. **プロトコル層**: クライアント・サーバー間の通信
6. **イベント層**: 状態変更の通知

## 詳細設計

### 1. アクション実行専用クラス（新規作成）

#### GameActionExecutor
ChallengeDatastoreのExecuteChallengeActionメソッドのロジックを切り出し、共通化

```csharp
namespace Game.Action
{
    public interface IGameActionExecutor
    {
        void ExecuteAction(ChallengeActionElement action);
    }

    public class GameActionExecutor : IGameActionExecutor
    {
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public GameActionExecutor(IGameUnlockStateDataController gameUnlockStateDataController)
        {
            _gameUnlockStateDataController = gameUnlockStateDataController;
        }

        public void ExecuteAction(ChallengeActionElement action)
        {
            // ChallengeDatastoreのExecuteChallengeActionメソッドから移動
            switch (action.ChallengeActionType)
            {
                case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                    var unlockRecipeGuids = ((UnlockCraftRecipeChallengeActionParam) action.ChallengeActionParam).UnlockRecipeGuids;
                    foreach (var guid in unlockRecipeGuids)
                    {
                        _gameUnlockStateDataController.UnlockCraftRecipe(guid);
                    }
                    break;

                case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                    var itemGuids = ((UnlockItemRecipeViewChallengeActionParam) action.ChallengeActionParam).UnlockItemGuids;
                    foreach (var itemGuid in itemGuids)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                        _gameUnlockStateDataController.UnlockItem(itemId);
                    }
                    break;

                case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                    var challenges = ((UnlockChallengeCategoryChallengeActionParam) action.ChallengeActionParam).UnlockChallengeCategoryGuids;
                    foreach (var guid in challenges)
                    {
                        _gameUnlockStateDataController.UnlockChallenge(guid);
                    }
                    break;
            }
        }
    }
}
```

### 2. ChallengeDatastoreの修正

既存のChallengeDatastoreをGameActionExecutorを使用するように変更

```csharp
public class ChallengeDatastore
{
    private readonly IGameActionExecutor _gameActionExecutor;

    public ChallengeDatastore(
        IGameUnlockStateDataController gameUnlockStateDataController,
        ChallengeEvent challengeEvent,
        IGameActionExecutor gameActionExecutor)  // 新規追加
    {
        _gameUnlockStateDataController = gameUnlockStateDataController;
        _challengeEvent = challengeEvent;
        _gameActionExecutor = gameActionExecutor;
        // ...
    }

    private void ExecuteChallengeAction(ChallengeActionElement action)
    {
        // GameActionExecutorに委譲
        _gameActionExecutor.ExecuteAction(action);
    }
}
```

### 3. マスターデータ実装

#### ResearchMaster
MasterHolder.csパターンに従い、研究マスターデータを管理

```csharp
namespace Core.Master
{
    public class ResearchMaster
    {
        private readonly Dictionary<Guid, ResearchMasterElement> _researchElements;

        public ResearchMaster(JToken researchJson)
        {
            _researchElements = new Dictionary<Guid, ResearchMasterElement>();
            LoadFromJson(researchJson);
        }

        public ResearchMasterElement GetResearch(Guid researchGuid)
        {
            return _researchElements.TryGetValue(researchGuid, out var element)
                ? element : null;
        }

        public List<ResearchMasterElement> GetAllResearches()
        {
            return _researchElements.Values.ToList();
        }

        private void LoadFromJson(JToken json)
        {
            // research.jsonからデータを読み込み
            // VanillaSchema/research.ymlから生成されたJSONを解析
        }
    }
}

// 研究マスターエレメントのデータ構造
public class ResearchMasterElement
{
    public Guid ResearchNodeGuid { get; set; }
    public string ResearchNodeName { get; set; }
    public string ResearchNodeDescription { get; set; }
    public Guid PrevResearchNodeGuid { get; set; }

    // ChallengeActionElementを使用してアクション定義を共通化
    public ChallengeActionElement[] ClearedActions { get; set; }

    public ConsumeItem[] ConsumeItems { get; set; }
    public GraphViewSettings GraphViewSettings { get; set; }
}
```

#### MasterHolder.csへの追加

```csharp
public class MasterHolder
{
    // 既存のプロパティ...
    public static ResearchMaster ResearchMaster { get; private set; }

    public static void Load(MasterJsonFileContainer masterJsonFileContainer)
    {
        // 既存のロード処理...
        ResearchMaster = new ResearchMaster(GetJson(masterJsonFileContainer, new JsonFileName("research")));
    }
}
```

### 4. データストア実装（ワールド単位）

#### IResearchDataStore インターフェース

```csharp
namespace Game.Research.Interface
{
    public interface IResearchDataStore
    {
        // 研究完了チェック（ワールド単位）
        bool IsResearchCompleted(Guid researchGuid);
        bool CanCompleteResearch(Guid researchGuid, int playerId);

        // 研究完了処理
        ResearchCompletionResult CompleteResearch(Guid researchGuid, int playerId);

        // データ取得
        HashSet<Guid> GetCompletedResearchGuids();

        // 永続化
        ResearchSaveJsonObject GetSaveJsonObject();
        void LoadResearchData(ResearchSaveJsonObject saveData);
    }
}
```

#### ResearchDataStore 実装（ワールド単位）

```csharp
namespace Game.Research
{
    public class ResearchDataStore : IResearchDataStore
    {
        // ワールド全体で共有される完了済み研究のセット
        private readonly HashSet<Guid> _completedResearchGuids = new();

        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IGameActionExecutor _gameActionExecutor;
        private readonly ResearchEvent _researchEvent;

        public ResearchDataStore(
            IPlayerInventoryDataStore inventoryDataStore,
            IGameActionExecutor gameActionExecutor,
            ResearchEvent researchEvent)
        {
            _inventoryDataStore = inventoryDataStore;
            _gameActionExecutor = gameActionExecutor;
            _researchEvent = researchEvent;
        }

        public bool IsResearchCompleted(Guid researchGuid)
        {
            return _completedResearchGuids.Contains(researchGuid);
        }

        public bool CanCompleteResearch(Guid researchGuid, int playerId)
        {
            var researchElement = MasterHolder.ResearchMaster.GetResearch(researchGuid);
            if (researchElement == null) return false;

            // 既に完了済みチェック
            if (_completedResearchGuids.Contains(researchGuid))
                return false;

            // 前提研究チェック
            if (researchElement.PrevResearchNodeGuid != Guid.Empty &&
                !_completedResearchGuids.Contains(researchElement.PrevResearchNodeGuid))
                return false;

            // アイテム所持チェック（プレイヤーのインベントリから）
            if (!CheckRequiredItems(playerId, researchElement.ConsumeItems))
                return false;

            return true;
        }

        public ResearchCompletionResult CompleteResearch(Guid researchGuid, int playerId)
        {
            if (!CanCompleteResearch(researchGuid, playerId))
            {
                return new ResearchCompletionResult
                {
                    Success = false,
                    Reason = "Research cannot be completed"
                };
            }

            var researchElement = MasterHolder.ResearchMaster.GetResearch(researchGuid);

            // プレイヤーのインベントリからアイテムを消費
            if (!ConsumeRequiredItems(playerId, researchElement.ConsumeItems))
            {
                return new ResearchCompletionResult
                {
                    Success = false,
                    Reason = "Failed to consume required items"
                };
            }

            // 研究完了記録（ワールドレベル）
            _completedResearchGuids.Add(researchGuid);

            // アクション実行（GameActionExecutorを使用）
            ExecuteResearchActions(researchElement.ClearedActions);

            // イベント発行
            _researchEvent.OnResearchCompleted(playerId, researchGuid);

            return new ResearchCompletionResult
            {
                Success = true,
                CompletedResearchGuid = researchGuid
            };
        }

        private void ExecuteResearchActions(ChallengeActionElement[] actions)
        {
            // GameActionExecutorを使用してアクションを実行
            foreach (var action in actions)
            {
                _gameActionExecutor.ExecuteAction(action);
            }
        }

        #region SaveLoad

        public ResearchSaveJsonObject GetSaveJsonObject()
        {
            return new ResearchSaveJsonObject
            {
                CompletedResearchGuids = _completedResearchGuids
                    .Select(g => g.ToString())
                    .ToList()
            };
        }

        public void LoadResearchData(ResearchSaveJsonObject saveData)
        {
            _completedResearchGuids.Clear();

            if (saveData?.CompletedResearchGuids != null)
            {
                foreach (var guidString in saveData.CompletedResearchGuids)
                {
                    if (Guid.TryParse(guidString, out var guid))
                    {
                        _completedResearchGuids.Add(guid);

                        // 新規追加された要素のアンロックアクションを実行
                        var researchElement = MasterHolder.ResearchMaster.GetResearch(guid);
                        if (researchElement != null)
                        {
                            ExecuteUnlockActions(researchElement.ClearedActions);
                        }
                    }
                }
            }
        }

        private void ExecuteUnlockActions(ChallengeActionElement[] actions)
        {
            // ロード時はアンロック系アクションのみ実行
            foreach (var action in actions)
            {
                switch (action.ChallengeActionType)
                {
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe:
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockItemRecipeView:
                    case ChallengeActionElement.ChallengeActionTypeConst.unlockChallengeCategory:
                        _gameActionExecutor.ExecuteAction(action);
                        break;
                }
            }
        }

        #endregion

        private bool CheckRequiredItems(int playerId, ConsumeItem[] consumeItems)
        {
            // プレイヤーインベントリからアイテムチェック
            var inventory = _inventoryDataStore.GetInventoryData(playerId);
            // ... アイテムチェックロジック
            return true;
        }

        private bool ConsumeRequiredItems(int playerId, ConsumeItem[] consumeItems)
        {
            // プレイヤーインベントリからアイテム消費
            var inventory = _inventoryDataStore.GetInventoryData(playerId);
            // ... アイテム消費ロジック
            return true;
        }
    }
}
```

### 5. プロトコル実装

#### リクエスト/レスポンス

```csharp
// クライアント → サーバー
[MessagePackObject]
public class CompleteResearchRequestMessagePack : ProtocolMessagePackBase
{
    public const string ProtocolTag = "CompleteResearch";

    [Key(0)] public int PlayerId { get; set; }
    [Key(1)] public string ResearchGuid { get; set; }
}

// サーバー → クライアント
[MessagePackObject]
public class CompleteResearchResponseMessagePack : ProtocolMessagePackBase
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public string ResearchGuid { get; set; }
    [Key(2)] public string FailureReason { get; set; }
    [Key(3)] public List<string> CompletedResearchGuids { get; set; } // ワールド全体の完了済み研究
}
```

#### プロトコルハンドラー

```csharp
namespace Server.Protocol.PacketResponse
{
    public class CompleteResearchProtocol : IPacketResponse
    {
        private readonly IResearchDataStore _researchDataStore;

        public CompleteResearchProtocol(ServiceProvider serviceProvider)
        {
            _researchDataStore = serviceProvider.GetService<IResearchDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<CompleteResearchRequestMessagePack>(payload.ToArray());

            var researchGuid = Guid.Parse(request.ResearchGuid);
            var result = _researchDataStore.CompleteResearch(researchGuid, request.PlayerId);

            return new CompleteResearchResponseMessagePack
            {
                Success = result.Success,
                ResearchGuid = request.ResearchGuid,
                FailureReason = result.Reason,
                CompletedResearchGuids = _researchDataStore.GetCompletedResearchGuids()
                    .Select(g => g.ToString())
                    .ToList()
            };
        }
    }
}
```

### 6. 初期化と統合

#### DIコンテナへの登録（MoorestechServerDIContainerGenerator.cs）

```csharp
public (PacketResponseCreator, ServiceProvider) Create(MoorestechServerDIContainerOptions options)
{
    // 既存のサービス登録...

    // アクション実行サービスの登録（ChallengeDatastoreより前に登録）
    services.AddSingleton<IGameActionExecutor, GameActionExecutor>();

    // 既存のChallengeDatastoreは上記のGameActionExecutorを使用するよう修正される
    services.AddSingleton<ChallengeDatastore, ChallengeDatastore>();

    // 研究システムの登録
    services.AddSingleton<IResearchDataStore, ResearchDataStore>();
    services.AddSingleton<ResearchEvent, ResearchEvent>();

    // 既存の処理...
}
```

#### プロトコル登録（PacketResponseCreator.cs）

```csharp
public PacketResponseCreator(ServiceProvider serviceProvider)
{
    // 既存のプロトコル登録...

    // 研究プロトコルの追加
    _packetResponseDictionary.Add(
        CompleteResearchProtocol.ProtocolTag,
        new CompleteResearchProtocol(serviceProvider));
    _packetResponseDictionary.Add(
        GetResearchStateProtocol.ProtocolTag,
        new GetResearchStateProtocol(serviceProvider));
}
```

#### セーブ/ロード統合（AssembleSaveJsonText.cs）

```csharp
public class AssembleSaveJsonText
{
    private readonly IResearchDataStore _researchDataStore;

    public AssembleSaveJsonText(
        // 既存の依存性...
        IResearchDataStore researchDataStore)
    {
        _researchDataStore = researchDataStore;
    }

    public string AssembleSaveText()
    {
        var saveData = new SaveJsonObject
        {
            // 既存のデータ...
            ResearchData = _researchDataStore.GetSaveJsonObject()
        };

        return JsonConvert.SerializeObject(saveData);
    }
}
```

### 7. イベント通知

```csharp
// 研究完了イベント
public class ResearchCompletedEventPacket
{
    private readonly EventProtocolProvider _eventProtocolProvider;
    private readonly IResearchDataStore _researchDataStore;

    public ResearchCompletedEventPacket(
        EventProtocolProvider eventProtocolProvider,
        ResearchEvent researchEvent,
        IResearchDataStore researchDataStore)
    {
        _eventProtocolProvider = eventProtocolProvider;
        _researchDataStore = researchDataStore;
        researchEvent.OnResearchCompleted.Subscribe(OnResearchCompleted);
    }

    private void OnResearchCompleted(int playerId, Guid researchGuid)
    {
        var packet = new ResearchCompletedEventMessagePack
        {
            ResearchGuid = researchGuid.ToString(),
            CompletedResearchGuids = _researchDataStore.GetCompletedResearchGuids()
                .Select(g => g.ToString())
                .ToList()
        };

        // 全プレイヤーに通知（ワールド単位のため）
        _eventProtocolProvider.SendEventToAllPlayers(packet);
    }
}
```

## 既存システムとの統合

### アクション実行の共通化
新規作成したGameActionExecutorクラスによる共通化：
- ChallengeDatastoreの`ExecuteChallengeAction`メソッドのロジックを移動
- ResearchDataStoreとChallengeDatastoreの両方がGameActionExecutorを使用
- `IGameUnlockStateDataController`を通じた共通のアンロック処理
- ロード時のアンロック専用処理も共通化

### マスターデータの統合
MasterHolderパターンに従い、研究マスターデータを静的プロパティとして提供：
- 起動時の一括ロード
- グローバルアクセス可能
- JSONファイルからの読み込み

### ワールドレベルデータ管理
ChallengeDatastoreやGameUnlockStateDataControllerと同様の設計：
- プレイヤーごとではなくワールド単位でのデータ管理
- シングルトンサービスとしてDIコンテナに登録
- セーブ/ロードシステムへの統合

## パフォーマンス考慮事項

1. **研究完了チェックの最適化**
   - HashSetを使用してO(1)での検索を実現
   - メモリ上にキャッシュ

2. **アイテム消費の原子性**
   - プレイヤーインベントリとの整合性保証
   - トランザクション的な処理

## セキュリティ考慮事項

1. **サーバーサイド検証**
   - すべての研究完了条件をサーバー側で検証
   - クライアントからのデータは信頼しない

2. **アイテム消費の検証**
   - インベントリ操作は必ずサーバー側で実行
   - 不正な研究完了を防止

## テスト戦略

1. **単体テスト**
   - GameActionExecutorの各アクションタイプのテスト
   - ResearchDataStoreの各メソッドのテスト
   - 前提条件検証ロジックのテスト
   - アイテム消費処理のテスト

2. **統合テスト**
   - プロトコル通信の動作確認
   - セーブ/ロードの正確性検証
   - アクション実行の確認
   - ChallengeDatastoreとの連携テスト

## 今後の拡張性

1. **研究ツリーのビジュアライズ**
   - graphViewSettings を活用したUI表現
   - 研究の依存関係の視覚化

2. **研究カテゴリ機能**
   - ChallengeDatastoreのカテゴリシステムを参考に実装
   - カテゴリごとのアンロック管理

3. **新しいアクションタイプの追加**
   - GameActionExecutorに新しいケースを追加するだけで拡張可能
   - 研究とチャレンジの両方で利用可能