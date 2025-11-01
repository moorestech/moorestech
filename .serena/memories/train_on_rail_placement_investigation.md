# レールの上に列車を乗せる処理の実装調査

## 1. 列車（TrainUnit）の生成・初期化処理

### TrainUnitコンストラクタ
```csharp
public TrainUnit(
    RailPosition initialPosition,
    List<TrainCar> cars
)
{
    _railPosition = initialPosition;
    _trainId = Guid.NewGuid();
    _cars = cars;
    _currentSpeed = 0.0;
    _isAutoRun = false;
    _previousEntryGuid = Guid.Empty;
    trainUnitStationDocking = new TrainUnitStationDocking(this);
    trainDiagram = new TrainDiagram();
    TrainUpdateService.Instance.RegisterTrain(this);
}
```

### 重要なフィールド
- `_railPosition`: RailPosition型 - レール上の列車位置管理
- `_trainId`: Guid型 - 列車の一意識別子
- `_cars`: List<TrainCar> - 列車を構成する車両リスト
- `_currentSpeed`: double型 - 現在の速度
- `trainUnitStationDocking`: TrainUnitStationDocking型 - 駅ドッキング管理
- `trainDiagram`: TrainDiagram型 - 自動運転の経路ダイアグラム

### TrainUpdateServiceへの登録
- TrainUnitのコンストラクタ内で自動的にTrainUpdateService.Instance.RegisterTrain(this)が呼ばれる
- TrainUpdateServiceはゲームループからUpdateTrainsが呼ばれ、全登録列車のUpdate()メソッドを実行

---

## 2. RailPositionの初期化と設定

### RailPositionコンストラクタ
```csharp
public RailPosition(
    List<RailNode> railNodes,
    int trainLength,
    int initialDistanceToNextNode
)
{
    if (railNodes == null || railNodes.Count < 1)
    {
        throw new ArgumentException("RailNodeリストには1つ以上の要素が必要です。");
    }
    
    if (trainLength < 0)
    {
        throw new ArgumentException("列車の長さは0以上である必要があります。");
    }
    
    _railNodes = railNodes;
    _trainLength = trainLength;
    _distanceToNextNode = initialDistanceToNextNode;
    ValidatePosition();
    TrainRailPositionManager.Instance.RegisterRailPosition(this);
}
```

### RailPositionの構成
- `_railNodes`: List<RailNode> - 列車が占有するレールノードのリスト
- `_trainLength`: int - 列車の全体的な長さ
- `_distanceToNextNode`: int - 現在位置から次のノードまでの距離

### RailPositionの役割
- レール上の列車の物理的位置を管理
- MoveForward()で列車を前後に移動
- GetNodesAtDistance()で指定距離のノードを取得
- RemoveUnnecessaryNodes()で通過済みノードを削除

---

## 3. レールグラフ上での列車の位置決定方法

### GetNodesAtDistanceメソッド
```csharp
public List<RailNode> GetNodesAtDistance(int distance)
{
    List<RailNode> nodesAtDistance = new List<RailNode>();
    int totalDistance = _distanceToNextNode + distance;
    for (int i = 0; i < _railNodes.Count; i++)
    {
        if (totalDistance == 0) 
        {
            nodesAtDistance.Add(_railNodes[i]);
        }
        if (i == _railNodes.Count - 1) break;
        int segmentDistance = _railNodes[i + 1].GetDistanceToNode(_railNodes[i]);
        totalDistance -= segmentDistance;
        if (totalDistance < 0) break;
    }
    return nodesAtDistance;
}
```

### 位置決定ロジック
1. 現在位置(_distanceToNextNode)から指定距離分進んだ位置を計算
2. _railNodesリストを走査しながら各ノード間の距離を逐次減算
3. totalDistanceがちょうど0になるノードを収集
4. 複数のノードがマッチする可能性がある（分岐点など）

### RailNodeの構造
- `OppositeNode`: RailNode型 - 反対方向のノード
- `StationRef`: StationReference型 - 駅情報（nullの場合もある）
- `FrontControlPoint`: Vector3型 - 前側の制御点
- `BackControlPoint`: Vector3型 - 後ろ側の制御点
- `ConnectedNodes`: List<RailNode> - 接続先ノード
- `GetDistanceToNode(RailNode other)`: int型 - 指定ノードまでの距離

### StationReferenceの構造
```csharp
public class StationReference
{
    public IBlock StationBlock { get; private set; }
    public StationNodeRole NodeRole { get; private set; }  // Entry or Exit
    public StationNodeSide NodeSide { get; private set; }   // Front or Back
    
    public bool IsPairWith(StationReference other)
    {
        // 同じ駅ブロック + Entry-Exitペアであることを確認
        if (other == null) return false;
        if (StationBlock == null || other.StationBlock == null) return false;
        if (StationBlock.BlockInstanceId != other.StationBlock.BlockInstanceId) return false;
        if (NodeSide != other.NodeSide) return false;
        return NodeRole != other.NodeRole;  // Entry-Exitの組み合わせ
    }
}
```

### StationNodeRole / StationNodeSide
- StationNodeRole: Entry（列車進入）/ Exit（列車出出）
- StationNodeSide: Front（正面）/ Back（背面）
- 駅には常に4つのノードが存在: EntryFront, EntryBack, ExitFront, ExitBack

---

## 4. 列車をレール上に配置する具体的な処理

### ドッキング判定フロー（TryDockWhenStopped）

```
TrainUnit.Update()
  ↓
UpdateTrainByDistance(distance)で列車を前進
  ↓
IsArrivedDestination() && _isAutoRun == true
  ↓
trainUnitStationDocking.TryDockWhenStopped()
  ├─ 各TrainCarについて反復:
  │  ├─ carposition = 累積車両長（前の車両の合計長）
  │  ├─ frontNodelist = RailPosition.GetNodesAtDistance(carposition)
  │  ├─ carposition += car.Length
  │  ├─ rearNodelist = RailPosition.GetNodesAtDistance(carposition)
  │  │
  │  └─ frontNodelistとrearNodelistの各ペアについて:
  │     ├─ IsSameStation(frontNode, rearNode)をチェック
  │     │  └─ frontNode.StationRef.IsPairWith(rearNode.StationRef)
  │     │  └─ frontNode.StationRef.NodeRole == StationNodeRole.Exit
  │     │
  │     └─ RegisterDockingBlock(dockingBlock, car, carIndex)
  │        ├─ ITrainDockingReceiverコンポーネントを取得
  │        ├─ receiver.CanDock(handle)をチェック
  │        └─ receiver.OnTrainDocked(handle)を呼び出し
  │
  └─ car.dockingblock = dockingBlock（ドッキング成功時）
```

### TryDockWhenStoppedメソッドの要点
1. **車両毎の処理**: 列車の各車両について独立してドッキング判定
2. **位置計算**: GetNodesAtDistanceで車両の前端・後端に対応するノードを取得
3. **駅判定**: IsSameStation()で同じ駅かつEntry-Exitペアを確認
4. **ドッキング登録**: RegisterDockingBlockで駅ブロックにドッキング

### IsSameStation判定ロジック
```csharp
private bool IsSameStation(RailNode frontNode, RailNode rearNode)
{
    bool isPair = frontNode.StationRef.IsPairWith(rearNode.StationRef);
    if (!isPair)
    {
        return false;  // 同じ駅でない場合はfalse
    }
    return frontNode.StationRef.NodeRole == StationNodeRole.Exit;
}
```

### RegisterDockingBlockの詳細
```csharp
private bool RegisterDockingBlock(IBlock block, TrainCar car, int carIndex)
{
    if (block == null)
    {
        return false;
    }
    
    // 既にドッキング済みの場合、同じ車両ならOK
    if (_dockedReceivers.TryGetValue(block, out var existing))
    {
        return existing.Handle.CarId == car.CarId;
    }
    
    // ITrainDockingReceiverコンポーネントを取得
    if (block.ComponentManager.TryGetComponent<ITrainDockingReceiver>(out var receiver))
    {
        var handle = new TrainDockHandle(_trainUnit, car, carIndex);
        if (!receiver.CanDock(handle))
        {
            return false;
        }
        
        // ドッキング登録
        _dockedReceivers[block] = new DockedReceiver(receiver, handle);
        receiver.OnTrainDocked(handle);
        return true;
    }
    
    return false;
}
```

### TrainCarの構造
```csharp
public class TrainCar
{
    public Guid CarId { get; }
    public int Length { get; }
    public int InventorySlots { get; }
    public int TractionForce { get; }
    public int FuelSlots { get; }
    public bool IsDocked => dockingblock != null;
    public IBlock dockingblock { get; set; }
    public bool IsFacingForward { get; private set; }
}
```

### TrainDockHandle
```csharp
public sealed class TrainDockHandle : ITrainDockHandle
{
    public Guid TrainId => _trainUnit.TrainId;
    public Guid CarId => _trainCar.CarId;
    public int CarIndex => _carIndex;
    public TrainUnit TrainUnit => _trainUnit;
    public TrainCar TrainCar => _trainCar;
}
```

---

## 5. ITrainDockingReceiverインターフェース

```csharp
public interface ITrainDockingReceiver : IBlockComponent
{
    bool CanDock(ITrainDockHandle handle);
    void ForceUndock();
    void OnTrainDocked(ITrainDockHandle handle);
    void OnTrainDockedTick(ITrainDockHandle handle);
    void OnTrainUndocked(ITrainDockHandle handle);
}
```

### 実装例：StationComponent
- `CanDock()`: 駅がドッキング可能か判定
- `OnTrainDocked()`: ドッキング時の処理（アイテム転送開始など）
- `OnTrainDockedTick()`: 毎フレームのドッキング中処理
- `OnTrainUndocked()`: ドッキング解除時の処理

---

## 6. クライアント側での列車の表示処理

### TrainRailObjectManager
- レール接続情報からスプラインメッシュを生成
- RailGraph更新プロトコルを受信して表示を更新
- OnRailDataReceivedでレール情報を受信
- UpdateRailComponentSplineでスプラインを更新

### プロトコル
- GetRailConnectionsProtocol: レール接続情報をクライアントに送信
- RailGraphDatastore.GetAllRailConnections(): 全レール接続情報を取得

---

## 7. 列車の移動と停止のフロー

### TrainUnit.Update()の主要フロー

1. **自動運転チェック**
   - trainDiagram.GetCurrentNode()が存在するか確認
   - 存在しなければ自動運転停止

2. **ダイアグラム変更チェック**
   - _previousEntryGuidと現在のエントリーGuidを比較
   - 変更されていたらドッキング解除

3. **移動と速度計算**
   - UpdateTrainSpeedで速度更新
   - UpdateTrainByDistance(distance)で距離移動

4. **到着判定とドッキング**
   - IsArrivedDestination()で目的地到着確認
   - TryDockWhenStopped()でドッキング試行

### UpdateTrainByDistanceの要点
1. MoveForward(distance)でレール上を移動
2. IsArrivedDestination()で目的地確認
3. 到着時にTryDockWhenStopped()を呼び出し
4. ドッキング成功時にtrainDiagram.ResetCurrentEntryDepartureConditions()

---

## 8. テスト例：TrainStationDockingScenario

テスト用のシナリオクラスが標準的なドッキング処理を提供：

```csharp
private TrainUnit CreateTrain(List<RailNode> nodes, List<TrainCar> cars, int initialDistanceToNextNode)
{
    var trainLength = cars.Sum(trainCar => trainCar.Length);
    var railPosition = new RailPosition(nodes, trainLength, initialDistanceToNextNode);
    var train = new TrainUnit(railPosition, cars);
    return train;
}
```

---

## 9. 重要な設計原則

### ドッキング判定の複雑性
- 単一の車両が複数の駅ノードにまたがる可能性を考慮
- GetNodesAtDistance()は複数のマッチ結果を返す可能性がある
- 全ペアの組み合わせをチェックして最初に成功したペアを使用

### 駅の構造
- 駅は常に「入口-出口」のペアを持つ
- 各ペアは「正面」と「背面」の2つの側面を持つ
- 列車はEntry側から進入し、Exit側から出出

### ドッキング状態の管理
- TrainCar.dockingblock: 車両がドッキングしているブロック
- TrainUnitStationDocking._dockedReceivers: ドッキング中のレシーバー管理
- StationComponent: 駅側でドッキング状態を追跡

---

## 10. パフォーマンス考慮

### GetNodesAtDistanceのパフォーマンス
- O(n)の線形探索（nはレールノード数）
- 複数の距離値をチェック（車両数分）

### TrainUpdateServiceの定期実行
- 毎フレームではなく、固定タイムステップで実行
- TickSeconds = 0.2秒（デフォルト）で複数ティック/フレーム

### メモリ使用
- RailPosition._railNodes: 列車が占有するノードのみ保持
- RemoveUnnecessaryNodes()で過去のノードを削除
- _dockedReceivers: アクティブなドッキング情報のみ保持
