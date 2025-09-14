# 設計ドキュメント

## 概要
本ドキュメントは、Moorestechゲームにおける研究システムの技術設計を定義します。既存のChallengeDatastore、GameUnlockStateDataController、MasterHolderなどの実装パターンに従い、一貫性のあるアーキテクチャで設計します。

## 要件マッピング

### 設計コンポーネントと要件の対応
- **ResearchDataStore** → 要件1: 研究完了状態の管理（ワールド単位）
- **ResearchMaster** → 要件2: 前提条件の検証
- **アイテム消費処理** → 要件3: アイテム消費の処理
- **プロトコル実装** → 要件4: 研究完了プロトコル
- **アクション実行** → 要件5: アクション実行システム（ChallengeDatastoreと共通化）

## アーキテクチャ概要

### システム構成
研究システムは以下のコンポーネントで構成され、既存システムと統合されます：

1. **マスターデータ層**: ResearchMasterによる研究定義の管理
2. **データ層**: ワールド単位での研究進捗データの管理と永続化
3. **ビジネスロジック層**: 研究完了条件の検証とアクション実行（既存システムと共通化）
4. **プロトコル層**: クライアント・サーバー間の通信
5. **イベント層**: 状態変更の通知

## 詳細設計

### 1. マスターデータ実装

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
            // JSONからマスターデータを読み込み
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

### 2. データストア実装（ワールド単位）

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
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly ResearchEvent _researchEvent;

        public ResearchDataStore(
            IPlayerInventoryDataStore inventoryDataStore,
            IGameUnlockStateDataController gameUnlockStateDataController,
            ResearchEvent researchEvent)
        {
            _inventoryDataStore = inventoryDataStore;
            _gameUnlockStateDataController = gameUnlockStateDataController;
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

            // アクション実行（ChallengeDatastoreと共通パターン）
            ExecuteResearchActions(researchElement.ClearedActions);

            // イベント発行
            _researchEvent.OnResearchCompleted(playerId, researchGuid);

            return new ResearchCompletionResult
            {
                Success = true,
                CompletedResearchGuid = researchGuid
            };
        }

        private void ExecuteResearchActions(ResearchActionElement[] actions)
        {
            // ChallengeDatastoreのExecuteChallengeActionと同様のパターン
            foreach (var action in actions)
            {
                ExecuteResearchAction(action);
            }
        }

        private void ExecuteResearchAction(ResearchActionElement action)
        {
            // ChallengeDatastoreのパターンを踏襲
            switch (action.ActionType)
            {
                case ResearchActionType.UnlockCraftRecipe:
                    var unlockRecipeGuids = action.UnlockRecipeGuids;
                    foreach (var guid in unlockRecipeGuids)
                    {
                        _gameUnlockStateDataController.UnlockCraftRecipe(guid);
                    }
                    break;

                case ResearchActionType.UnlockItem:
                    var itemGuids = action.UnlockItemGuids;
                    foreach (var itemGuid in itemGuids)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                        _gameUnlockStateDataController.UnlockItem(itemId);
                    }
                    break;

                case ResearchActionType.UnlockBlock:
                    var blockGuids = action.UnlockBlockGuids;
                    foreach (var blockGuid in blockGuids)
                    {
                        var blockId = MasterHolder.BlockMaster.GetBlockId(blockGuid);
                        _gameUnlockStateDataController.UnlockBlock(blockId);
                    }
                    break;
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

        private void ExecuteUnlockActions(ResearchActionElement[] actions)
        {
            // ロード時はアンロック系アクションのみ実行（ChallengeDatastoreと同様）
            foreach (var action in actions)
            {
                switch (action.ActionType)
                {
                    case ResearchActionType.UnlockCraftRecipe:
                    case ResearchActionType.UnlockItem:
                    case ResearchActionType.UnlockBlock:
                        ExecuteResearchAction(action);
                        break;
                }
            }
        }

        #endregion
    }
}
```

### 3. プロトコル実装

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

### 4. 初期化と統合

#### DIコンテナへの登録（MoorestechServerDIContainerGenerator.cs）

```csharp
public (PacketResponseCreator, ServiceProvider) Create(MoorestechServerDIContainerOptions options)
{
    // 既存のサービス登録...

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

### 5. イベント通知

```csharp
// 研究完了イベント
public class ResearchCompletedEventPacket
{
    private readonly EventProtocolProvider _eventProtocolProvider;

    public ResearchCompletedEventPacket(
        EventProtocolProvider eventProtocolProvider,
        ResearchEvent researchEvent)
    {
        _eventProtocolProvider = eventProtocolProvider;
        researchEvent.OnResearchCompleted.Subscribe(OnResearchCompleted);
    }

    private void OnResearchCompleted(int playerId, Guid researchGuid)
    {
        var packet = new ResearchCompletedEventMessagePack
        {
            ResearchGuid = researchGuid.ToString(),
            CompletedResearchGuids = // 全完了済み研究のリスト
        };

        // 全プレイヤーに通知（ワールド単位のため）
        _eventProtocolProvider.SendEventToAllPlayers(packet);
    }
}
```

## 既存システムとの統合

### アクション実行の共通化
ChallengeDatastoreで使用されているアクション実行パターンを再利用：
- `IGameUnlockStateDataController`を通じたアンロック処理
- レシピ、アイテム、ブロックのアンロック
- ロード時のアンロック専用処理

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
   - ResearchDataStoreの各メソッドのテスト
   - 前提条件検証ロジックのテスト
   - アイテム消費処理のテスト

2. **統合テスト**
   - プロトコル通信の動作確認
   - セーブ/ロードの正確性検証
   - アクション実行の確認

## 今後の拡張性

1. **研究ツリーのビジュアライズ**
   - graphViewSettings を活用したUI表現
   - 研究の依存関係の視覚化

2. **研究カテゴリ機能**
   - ChallengeDatastoreのカテゴリシステムを参考に実装
   - カテゴリごとのアンロック管理