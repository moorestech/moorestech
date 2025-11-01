# 駅の上に列車を乗せる処理の実装調査

## 概要
モーアテックにおいて、列車を駅（ステーション）のレール上に配置する処理は、複数のコンポーネントが連携して実現されています。

## 1. コアコンポーネント

### サーバー側
1. **TrainUnit** (`moorestech_server/Assets/Scripts/Game.Train/Train/TrainUnit.cs`)
   - 列車全体を管理するクラス
   - 複数のTrainCarを保持
   - RailPositionを保持（レール上の位置情報）
   - TrainUnitStationDockingコンポーネントを保持

2. **TrainUnitStationDocking** (`moorestech_server/Assets/Scripts/Game.Train/Train/TrainUnitStationDocking.cs`)
   - 列車と駅のドッキング（接続）を管理
   - `TryDockWhenStopped()` - 停止時にドッキング可能か確認し、ドッキングを試みる
   - `RegisterDockingBlock()` - ドッキングブロックを登録
   - `_dockedReceivers` - ドッキング済みレシーバーのディクショナリ

3. **StationComponent** (`moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/StationComponent.cs`)
   - 駅ブロック上に配置されたコンポーネント
   - `OnTrainDocked()` - 列車がドッキングした時に呼び出される
   - `OnTrainDockedTick()` - ドッキング中の毎フレーム処理（アイテム転送など）
   - ドッキング中の列車ID、車両ID、インベントリを管理

4. **RailPosition** (`moorestech_server/Assets/Scripts/Game.Train/RailGraph/RailPosition.cs`)
   - 列車のレール上の位置を管理
   - RailNodeのリストで構成
   - `GetNodesAtDistance(distance)` - 指定距離のノードリストを取得

5. **RailNode** (`moorestech_server/Assets/Scripts/Game.Train/RailGraph/RailNode.cs`)
   - レールグラフの一つのノード
   - `StationRef` - StationReferenceを保持（駅への参照）
   - 他のノードとの接続情報を管理

6. **StationReference** (`moorestech_server/Assets/Scripts/Game.Train/RailGraph/StationReference.cs`)
   - RailNode上に駅情報を附属させる
   - `StationBlock` - StationComponentを持つブロックへの参照
   - `NodeRole` - 駅における役割（Entry/Exit）
   - `NodeSide` - ノードの側面（Front/Back）

### クライアント側
1. **TrainRailObjectManager** (`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/TrainRailObjectManager.cs`)
   - レール表示の管理（3Dメッシュの生成・更新）

## 2. 列車の駅への配置フロー

```
TrainUnit.Update()
  ↓
列車移動ロジック(UpdateTrainByDistance)
  ↓
列車が停止した状態になる（CurrentSpeed == 0）
  ↓
TrainUnitStationDocking.TryDockWhenStopped()
  ├─ 各TrainCarについて反復:
  │  ├─ carposition = 累積車両長
  │  ├─ frontNodelist = RailPosition.GetNodesAtDistance(carposition)
  │  ├─ rearNodelist = RailPosition.GetNodesAtDistance(carposition + carLength)
  │  ├─ frontNodeとrearNodeが同じ駅に属するか確認（IsSameStation）
  │  └─ RegisterDockingBlock(dockingBlock, car, carIndex)
  │     ├─ ドッキングレシーバーのチェック
  │     ├─ receiver.CanDock(handle) で可能性確認
  │     └─ receiver.OnTrainDocked(handle) を呼び出し
  │
  └─ StationComponent.OnTrainDocked()で駅が列車到着を認識

  car.dockingblock = dockingBlock （車両が駅ブロックへの参照を保持）
```

## 3. 列車配置判定ロジック

### TryDockWhenStopped()の判定ルール

```csharp
// 1. 車両がレール上に正確に配置されているか
frontNodelist = GetNodesAtDistance(carposition)
rearNodelist = GetNodesAtDistance(carposition + carLength)

// 2. 前後のノードが同じ駅に属しているか
IsSameStation(frontNode, rearNode)
  → frontNode.StationRef != null && rearNode.StationRef != null
  → frontNode.StationRef.StationBlock == rearNode.StationRef.StationBlock
  → frontNodeのNodeRole != rearNodeのNodeRole（Entryと異なる）

// 3. ドッキングブロックが受け入れ可能か
dockingBlock = frontNode.StationRef.StationBlock
receiver.CanDock(handle) が true
```

### 重要なチェック

1. **同じ駅確認** (`IsSameStation`)
   - 同じStationBlockを指す必要がある
   - NodeRoleが異なる必要がある（EntryとExitのペア）

2. **ドッキング可能性** (`RegisterDockingBlock`)
   - ITrainDockingReceiverコンポーネントが必須
   - receiver.CanDock()がtrueであること
   - 既にドッキング済みでないこと

## 4. データ構造の関係図

```
TrainUnit
  ├─ RailPosition
  │  └─ List<RailNode>
  │     └─ RailNode
  │        └─ StationReference
  │           └─ StationComponent (StationBlock)
  │
  ├─ List<TrainCar>
  │  └─ TrainCar
  │     └─ dockingblock (IBlock型)
  │
  └─ TrainUnitStationDocking
     └─ Dictionary<IBlock, DockedReceiver>
        └─ ITrainDockingReceiver

StationComponent
  ├─ _dockedTrainId (ドッキング中の列車ID)
  ├─ _dockedCarId (ドッキング中の車両ID)
  ├─ _dockedTrainCar (TrainCarへの参照)
  └─ _dockedStationInventory (ドッキング中のインベントリ)
```

## 5. ドッキング後の処理

ドッキング成功後:
1. `car.dockingblock = dockingBlock` で車両が駅への参照を保持
2. `StationComponent.OnTrainDockedTick()` が毎フレーム呼び出される
   - アイテム転送処理
   - 駅インベントリの更新

## 6. テスト例

`TrainStationDockingScenario` クラスが統合テスト用の標準シナリオを提供:
- 駅の配置
- レール接続
- 列車の配置と駅への接近
- ドッキング検証

テスト用ブロック: `ForUnitTestModBlockId.TestTrainCargoPlatform`

## 7. クライアント側のビジュアル表現

`TrainRailObjectManager` がレール上の列車の位置に対応する3Dメッシュを更新し、ゲーム画面に列車を表示する役割を担当。