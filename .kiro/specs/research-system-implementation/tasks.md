# 実装タスク

## 1. 共通アクション実行システムの抽出と実装

### 1.1 GameActionExecutorインターフェースの定義
- [x] `IGameActionExecutor`インターフェースを作成
- [x] `ExecuteAction(ChallengeActionElement action)`メソッドを定義
- **対応要件**: REQ-006

### 1.2 GameActionExecutorクラスの実装
- [x] `ChallengeDatastore.ExecuteChallengeAction`のロジックを抽出
- [x] `GameActionExecutor`クラスに実装を移植
- [x] 必要な依存関係（`IGameUnlockStateDataController`など）を注入
- **対応要件**: REQ-006

### 1.3 ChallengeDatastoreのリファクタリング
- [x] `ExecuteChallengeAction`メソッドを`GameActionExecutor`の呼び出しに置き換え
- [x] 既存のチャレンジ機能が正常に動作することを確認
- **対応要件**: REQ-006

## 2. 研究マスターデータ構造の実装

### 2.1 ResearchNodeマスターデータの定義
- [x] `ResearchMasterElement`データ構造を定義（ID、名前、必要アイテム、アクション）
- [x] ConsumeItemとGraphViewSettings構造を定義
- **対応要件**: REQ-002

### 2.2 ResearchMasterクラスの実装
- [x] `ResearchMaster`クラスを作成
- [x] 研究ノード検索メソッド`GetResearch(Guid id)`を実装
- [x] `MasterHolder`に`ResearchMaster`プロパティを追加
- **対応要件**: REQ-002

## 3. ResearchDataStoreの実装

### 3.1 データ構造の実装
- [x] `ResearchDataStore`クラスを作成
- [x] `HashSet<Guid>`によるワールド研究状態管理を実装
- **対応要件**: REQ-001

### 3.2 基本メソッドの実装
- [x] `IsResearchCompleted(Guid researchId)`メソッドを実装
- [x] `CompleteResearch(Guid researchId, int playerId)`メソッドを実装
- [x] `GetCompletedResearches()`メソッドを実装
- **対応要件**: REQ-001, REQ-003

### 3.3 アクション実行の統合
- [x] `GameActionExecutor`の依存関係注入
- [x] 研究完了時のアクション実行ロジックを実装
- **対応要件**: REQ-006

### 3.4 永続化機能の実装
- [x] `GetSaveJsonObject()`メソッドを実装
- [x] `LoadResearchData(ResearchSaveJsonObject saveData)`メソッドを実装
- [x] ロード時のアンロックアクション実行処理を実装
- **対応要件**: REQ-001

### 3.5 アイテム管理機能の実装
- [x] `CheckRequiredItems`メソッドを実装
- [x] `ConsumeRequiredItems`メソッドを実装
- [x] IOpenableInventoryインターフェースとの連携実装
- **対応要件**: REQ-003

## 4. プロトコル実装

### 4.1 RequestCompleteResearchProtocol
- [ ] MessagePack対応プロトコルクラスを作成
- [ ] `ResearchId`と`PlayerId`フィールドを定義
- **対応要件**: REQ-004

### 4.2 ResearchStateEventProtocol
- [ ] イベントプロトコルクラスを作成（完了状態通知用）
- [ ] `ResearchId`と`IsCompleted`フィールドを定義
- **対応要件**: REQ-005

### 4.3 プロトコルハンドラーの実装
- [ ] `RequestCompleteResearchProtocolHandler`を実装
- [ ] 研究完了要求の検証処理を実装
- [ ] アイテム消費ロジックを実装（`PlayerInventoryDataStore`との連携）
- [ ] 研究完了処理と状態更新
- [ ] イベント通知の送信処理
- **対応要件**: REQ-003, REQ-004, REQ-005

## 5. DI設定とシステム統合

### 5.1 DIコンテナへの登録
- [ ] `MoorestechServerDIContainerGenerator`に`GameActionExecutor`を登録
- [ ] `ResearchDataStore`を登録
- [ ] `ResearchMaster`の初期化処理を追加
- **対応要件**: REQ-001

### 5.2 PacketResponseCreatorの更新
- [ ] `RequestCompleteResearchProtocolHandler`をハンドラーマップに追加
- [ ] プロトコルIDのマッピングを設定
- **対応要件**: REQ-004

## 6. テストの実装

### 6.1 単体テスト
- [x] `GameActionExecutor`のテスト作成
- [ ] `ResearchDataStore`の状態管理テスト作成
- [ ] `ResearchMaster`のデータ読み込みテスト作成
- **対応要件**: REQ-008

### 6.2 統合テスト
- [ ] 研究完了プロトコルの統合テスト作成
- [ ] アイテム消費と研究完了の連携テスト作成
- [ ] アクション実行の統合テスト作成
- **対応要件**: REQ-003, REQ-004, REQ-006

## 7. パフォーマンス最適化

### 7.1 検索性能の確認
- [ ] `HashSet<Guid>`によるO(1)検索性能を確認
- [ ] 大量の研究ノードでのパフォーマンステスト実施
- **対応要件**: REQ-008

## 実装順序の推奨

1. **フェーズ1**: GameActionExecutorの抽出と実装（タスク1）✅ 完了
2. **フェーズ2**: 研究マスターデータとデータストア（タスク2, 3）✅ 完了
3. **フェーズ3**: プロトコル実装（タスク4）⏳ 進行中
4. **フェーズ4**: システム統合とDI設定（タスク5）
5. **フェーズ5**: テストとパフォーマンス確認（タスク6, 7）

## 各タスクの推定作業時間と実績

- GameActionExecutor抽出: 2-3時間 ✅ 完了
- ResearchMaster実装: 2時間 ✅ 完了
- ResearchDataStore実装: 3-4時間 ✅ 完了
- プロトコル実装: 4-5時間 ⏳ 進行中
- DI設定と統合: 2時間
- テスト作成: 4-5時間

**完了済みタスク**: 約7-9時間
**残タスク推定時間**: 10-12時間
**合計推定時間**: 17-21時間