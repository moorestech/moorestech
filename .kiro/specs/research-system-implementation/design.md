# 設計ドキュメント

## 概要
本ドキュメントは、Moorestechゲームにおける研究システムの技術設計を定義します。既存のPlayerInventoryシステムやプロトコル実装パターンを参考に、拡張性と保守性を重視した設計を行います。

## アーキテクチャ概要

### システム構成
研究システムは以下のコンポーネントで構成されます：

1. **データ層**: 研究進捗データの管理と永続化
2. **ビジネスロジック層**: 研究完了条件の検証とアクション実行
3. **プロトコル層**: クライアント・サーバー間の通信
4. **イベント層**: 状態変更の通知

## 詳細設計

### 1. データモデル

#### ResearchData
プレイヤーごとの研究進捗を管理するデータクラス

```csharp
namespace Game.Research
{
    public class ResearchData
    {
        // 完了済み研究ノードのGUID集合（高速検索用）
        public HashSet<Guid> CompletedResearchGuids { get; private set; }

        // プレイヤーID
        public int PlayerId { get; private set; }

        // 最後に研究を完了した時刻（デバッグ・分析用）
        public DateTime LastResearchCompletedAt { get; set; }

        public ResearchData(int playerId)
        {
            PlayerId = playerId;
            CompletedResearchGuids = new HashSet<Guid>();
        }
    }
}
```

#### ResearchSaveJsonObject
永続化用のJSON構造

```csharp
public class ResearchSaveJsonObject
{
    public Dictionary<int, PlayerResearchSaveData> PlayerResearchData { get; set; }
}

public class PlayerResearchSaveData
{
    public List<string> CompletedResearchGuids { get; set; }
    public string LastResearchCompletedAt { get; set; }
}
```

### 2. データストア実装

#### IResearchDataStore インターフェース

```csharp
namespace Game.Research.Interface
{
    public interface IResearchDataStore
    {
        // 研究完了チェック
        bool IsResearchCompleted(int playerId, Guid researchGuid);
        bool CanCompleteResearch(int playerId, Guid researchGuid);

        // 研究完了処理
        ResearchCompletionResult CompleteResearch(int playerId, Guid researchGuid);

        // データ取得
        ResearchData GetResearchData(int playerId);
        HashSet<Guid> GetCompletedResearchGuids(int playerId);

        // 永続化
        ResearchSaveJsonObject GetSaveJsonObject();
        void LoadResearchData(ResearchSaveJsonObject saveData);
    }
}
```

#### ResearchDataStore 実装

```csharp
namespace Game.Research
{
    public class ResearchDataStore : IResearchDataStore
    {
        private readonly Dictionary<int, ResearchData> _playerResearchData;
        private readonly IResearchNodeRepository _researchNodeRepository;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IResearchCompletedEvent _researchCompletedEvent;
        private readonly IActionDispatcher _actionDispatcher;

        public ResearchDataStore(
            IResearchNodeRepository researchNodeRepository,
            IPlayerInventoryDataStore inventoryDataStore,
            IResearchCompletedEvent researchCompletedEvent,
            IActionDispatcher actionDispatcher)
        {
            _playerResearchData = new Dictionary<int, ResearchData>();
            _researchNodeRepository = researchNodeRepository;
            _inventoryDataStore = inventoryDataStore;
            _researchCompletedEvent = researchCompletedEvent;
            _actionDispatcher = actionDispatcher;
        }

        public ResearchData GetResearchData(int playerId)
        {
            if (!_playerResearchData.ContainsKey(playerId))
            {
                _playerResearchData[playerId] = new ResearchData(playerId);
            }
            return _playerResearchData[playerId];
        }

        public bool CanCompleteResearch(int playerId, Guid researchGuid)
        {
            var researchData = GetResearchData(playerId);
            var researchNode = _researchNodeRepository.GetNode(researchGuid);

            if (researchNode == null) return false;

            // 既に完了済みチェック
            if (researchData.CompletedResearchGuids.Contains(researchGuid))
                return false;

            // 前提研究チェック
            if (researchNode.PrevResearchNodeGuid != Guid.Empty &&
                !researchData.CompletedResearchGuids.Contains(researchNode.PrevResearchNodeGuid))
                return false;

            // アイテム所持チェック
            if (!CheckRequiredItems(playerId, researchNode.ConsumeItems))
                return false;

            return true;
        }

        public ResearchCompletionResult CompleteResearch(int playerId, Guid researchGuid)
        {
            // トランザクション的な処理
            if (!CanCompleteResearch(playerId, researchGuid))
            {
                return new ResearchCompletionResult
                {
                    Success = false,
                    Reason = "Research cannot be completed"
                };
            }

            var researchNode = _researchNodeRepository.GetNode(researchGuid);

            // アイテム消費
            if (!ConsumeRequiredItems(playerId, researchNode.ConsumeItems))
            {
                return new ResearchCompletionResult
                {
                    Success = false,
                    Reason = "Failed to consume required items"
                };
            }

            // 研究完了記録
            var researchData = GetResearchData(playerId);
            researchData.CompletedResearchGuids.Add(researchGuid);
            researchData.LastResearchCompletedAt = DateTime.UtcNow;

            // アクション実行
            ExecuteClearedActions(playerId, researchNode.ClearedActions);

            // イベント発行
            _researchCompletedEvent.OnResearchCompleted(playerId, researchGuid);

            return new ResearchCompletionResult
            {
                Success = true,
                CompletedResearchGuid = researchGuid
            };
        }
    }
}
```

### 3. プロトコル実装

#### リクエスト/レスポンス

```csharp
// クライアント → サーバー
[MessagePackObject]
public class CompleteResearchRequestMessagePack : IByteArraySerializable
{
    public const string ProtocolTag = "CompleteResearch";

    [Key(0)] public int PlayerId { get; set; }
    [Key(1)] public string ResearchGuid { get; set; }
}

// サーバー → クライアント
[MessagePackObject]
public class CompleteResearchResponseMessagePack
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public string ResearchGuid { get; set; }
    [Key(2)] public string FailureReason { get; set; }
    [Key(3)] public List<string> MissingItems { get; set; }
}
```

#### イベント通知

```csharp
// 研究完了イベント（サーバー → クライアント）
[MessagePackObject]
public class ResearchCompletedEventPacket : IByteArraySerializable
{
    public const string EventTag = "ResearchCompleted";

    [Key(0)] public int PlayerId { get; set; }
    [Key(1)] public string ResearchGuid { get; set; }
    [Key(2)] public List<string> UnlockedItems { get; set; }
}

// プレイヤーログイン時の同期
[MessagePackObject]
public class SyncPlayerResearchDataPacket : IByteArraySerializable
{
    public const string EventTag = "SyncResearchData";

    [Key(0)] public List<string> CompletedResearchGuids { get; set; }
}
```

#### プロトコルハンドラー

```csharp
namespace Server.Protocol.PacketResponse
{
    public class CompleteResearchProtocol : IPacketResponse
    {
        private readonly IResearchDataStore _researchDataStore;

        public CompleteResearchProtocol(IResearchDataStore researchDataStore)
        {
            _researchDataStore = researchDataStore;
        }

        public List<ResponsePacket> GetResponsePackets(RequestPacket request)
        {
            var requestData = MessagePackSerializer.Deserialize<CompleteResearchRequestMessagePack>(
                request.Payload);

            var researchGuid = Guid.Parse(requestData.ResearchGuid);
            var result = _researchDataStore.CompleteResearch(
                requestData.PlayerId, researchGuid);

            var response = new CompleteResearchResponseMessagePack
            {
                Success = result.Success,
                ResearchGuid = requestData.ResearchGuid,
                FailureReason = result.Reason,
                MissingItems = result.MissingItems
            };

            return new List<ResponsePacket>
            {
                new ResponsePacket(
                    requestData.PlayerId,
                    MessagePackSerializer.Serialize(response))
            };
        }
    }
}
```

### 4. 研究ノードリポジトリ

```csharp
namespace Game.Research
{
    public interface IResearchNodeRepository
    {
        ResearchNode GetNode(Guid researchGuid);
        List<ResearchNode> GetAllNodes();
        void LoadFromSchema(string schemaPath);
    }

    public class ResearchNodeRepository : IResearchNodeRepository
    {
        private readonly Dictionary<Guid, ResearchNode> _researchNodes;

        public ResearchNodeRepository()
        {
            _researchNodes = new Dictionary<Guid, ResearchNode>();
        }

        public void LoadFromSchema(string schemaPath)
        {
            // VanillaSchema/research.ymlからデータを読み込み
            // Addressableまたは直接ファイル読み込みで実装
        }

        public ResearchNode GetNode(Guid researchGuid)
        {
            return _researchNodes.TryGetValue(researchGuid, out var node)
                ? node : null;
        }
    }
}
```

### 5. アクション実行システム

```csharp
namespace Game.Research
{
    public interface IActionDispatcher
    {
        void ExecuteActions(int playerId, List<ResearchAction> actions);
    }

    public class ResearchActionDispatcher : IActionDispatcher
    {
        private readonly Dictionary<string, IActionExecutor> _executors;

        public ResearchActionDispatcher(
            IRecipeUnlockExecutor recipeUnlocker,
            IItemGrantExecutor itemGranter)
        {
            _executors = new Dictionary<string, IActionExecutor>
            {
                ["UnlockRecipe"] = recipeUnlocker,
                ["GrantItem"] = itemGranter
            };
        }

        public void ExecuteActions(int playerId, List<ResearchAction> actions)
        {
            foreach (var action in actions)
            {
                if (_executors.TryGetValue(action.Type, out var executor))
                {
                    executor.Execute(playerId, action.Parameters);
                }
            }
        }
    }
}
```

### 6. 統合ポイント

#### サーバー初期化時

```csharp
// ServerMainSystemBuilder.cs への追加
public class ServerMainSystemBuilder
{
    public void BuildResearchSystem()
    {
        // リポジトリ初期化
        var researchNodeRepository = new ResearchNodeRepository();
        researchNodeRepository.LoadFromSchema("VanillaSchema/research.yml");

        // アクションディスパッチャー初期化
        var actionDispatcher = new ResearchActionDispatcher(
            recipeUnlocker, itemGranter);

        // データストア初期化
        var researchDataStore = new ResearchDataStore(
            researchNodeRepository,
            inventoryDataStore,
            researchCompletedEvent,
            actionDispatcher);

        // プロトコルハンドラー登録
        packetResponseCreator.AddProtocol(
            CompleteResearchRequestMessagePack.ProtocolTag,
            new CompleteResearchProtocol(researchDataStore));
    }
}
```

#### セーブ/ロード統合

```csharp
// AssembleSaveJsonText.cs への追加
public class AssembleSaveJsonText
{
    private readonly IResearchDataStore _researchDataStore;

    public string AssembleSaveText()
    {
        var saveData = new SaveJsonObject
        {
            // 既存のデータ...
            ResearchData = _researchDataStore.GetSaveJsonObject()
        };

        return JsonConvert.SerializeObject(saveData);
    }

    public void LoadSaveData(SaveJsonObject saveData)
    {
        // 既存のロード処理...
        if (saveData.ResearchData != null)
        {
            _researchDataStore.LoadResearchData(saveData.ResearchData);
        }
    }
}
```

## パフォーマンス考慮事項

1. **研究完了チェックの最適化**
   - HashSetを使用してO(1)での検索を実現
   - 頻繁にアクセスされるデータはメモリ上にキャッシュ

2. **並行処理の安全性**
   - 複数プレイヤーが同時に研究を完了する場合のロック機構
   - アトミックなトランザクション処理

3. **メモリ効率**
   - 遅延初期化によるメモリ使用量の最適化
   - 不要なデータの定期的なクリーンアップ

## セキュリティ考慮事項

1. **サーバーサイド検証**
   - すべての研究完了条件をサーバー側で検証
   - クライアントからのデータは一切信頼しない

2. **レート制限**
   - 短時間での大量リクエストを検出・防止
   - 不正なアクセスパターンのログ記録

3. **データ整合性**
   - トランザクション失敗時の完全なロールバック
   - 永続化データの定期的な整合性チェック

## テスト戦略

1. **単体テスト**
   - ResearchDataStoreの各メソッドのテスト
   - 前提条件検証ロジックのテスト
   - アイテム消費処理のテスト

2. **統合テスト**
   - プロトコル通信の動作確認
   - セーブ/ロードの正確性検証
   - 複数プレイヤー環境での動作確認

3. **パフォーマンステスト**
   - 大量の研究ノードでの応答速度
   - 同時接続プレイヤー数の限界値測定

## 今後の拡張性

1. **研究ツリーのビジュアライズ**
   - graphViewSettings を活用したUI表現
   - 研究の依存関係の視覚化

2. **共同研究機能**
   - 複数プレイヤーでの研究進捗共有
   - チーム研究の実装

3. **研究ブースト機能**
   - 研究速度を上げるアイテムやバフ
   - 時間経過による自動研究完了