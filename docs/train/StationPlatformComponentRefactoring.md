# Station/Platform コンポーネント リファクタリング設計

## 概要

現在の`StationComponent`と`CargoplatformComponent`は複数の責務を持っており、コンポーネント指向として改善の余地がある。本ドキュメントでは、単一責任・組み合わせ可能な構造へのリファクタリング設計をまとめる。

## 現状の問題点

- `StationComponent`と`CargoplatformComponent`が非常に似たコードを持つ
- 1つのコンポーネントが複数の責務を持っている：
  - Diagramでの駅認識
  - 列車とのドッキング処理
  - アーム状態管理
  - アイテム転送

## 命名規則

| 用語 | 意味 |
|---|---|
| Station | 駅としてのマーカー（Diagramが認識） |
| Platform | 貨物プラットフォーム（先頭車両以外の車両も扱う） |

## 新コンポーネント構成

### 1. TrainStationComponent
**役割**: Diagramが駅として認識するためのマーカー

```csharp
public class TrainStationComponent
{
    public string StationName { get; }
}
```

### 2. TrainPlatformDockingComponent
**役割**: 列車とのドッキング状態管理

```csharp
public class TrainPlatformDockingComponent : ITrainDockingReceiver
{
    public bool IsDocked { get; }
    public TrainCar DockedTrainCar { get; }

    public bool CanDock(ITrainDockHandle handle);
    public void OnTrainDocked(ITrainDockHandle handle);
    public void OnTrainUndocked(ITrainDockHandle handle);
}
```

### 3. TrainPlatformContainerComponent
**役割**: 駅側のコンテナ（インベントリ）保持

```csharp
public class TrainPlatformContainerComponent
{
    public IBlockInventory Inventory { get; }
    public int SlotCount { get; }
}
```

### 4. TrainPlatformArmComponent
**役割**: アーム状態管理、転送方向モード保持

```csharp
public class TrainPlatformArmComponent : IUpdatableBlockComponent
{
    public enum TransferDirection
    {
        ToTrain,
        ToPlatform
    }

    public enum ArmState
    {
        Idle,
        Extending,
        Retracting
    }

    public ArmState State { get; }
    public TransferDirection Direction { get; }
    public bool IsFullyExtended { get; }

    public void SetDirection(TransferDirection direction);
}
```

### 5. TrainPlatformItemTransferComponent
**役割**: アイテム転送ロジック

```csharp
public class TrainPlatformItemTransferComponent : IUpdatableBlockComponent
{
    // ArmComponentのIsFullyExtendedを監視し、
    // 伸びきった瞬間にアイテム転送を実行
}
```

### 6. TrainPlatformFluidTransferComponent（将来）
**役割**: 液体転送ロジック

```csharp
public class TrainPlatformFluidTransferComponent : IUpdatableBlockComponent
{
    // ArmComponentのIsFullyExtendedを監視し、
    // 伸びきった瞬間に液体転送を実行
}
```

## コンポーネント依存関係

```
TrainStationComponent（マーカー）

TrainPlatformDockingComponent
         │
         ↓ 依存
TrainPlatformArmComponent（アーム状態 + 転送方向モード）
         │
         ↓ 参照
┌────────┴────────┬────────────────────────┐
│                 │                        │
TrainPlatform-    TrainPlatform-           TrainPlatform-
ContainerComponent ItemTransferComponent   FluidTransferComponent
```

## 転送の仕組み

### 転送タイミング
- **一括転送方式**: アーム1往復ごとにまとめて転送
- アームが完全に伸びきった瞬間（1サイクルに1回）に転送実行

### アーム状態遷移
```
Idle → Extending → (転送) → Retracting → Idle
```

### 責務分離

**TrainPlatformArmComponent.Update()**
- DockingComponentを見てドッキング中か確認
- Idle → Extending 開始判定
- _armProgressTicks++ / --
- Extending → Retracting 遷移
- Retracting → Idle 遷移
- **アーム自身の状態遷移を自己管理**

**TrainPlatformItemTransferComponent.Update()**
- ArmComponentのIsFullyExtendedを毎フレーム確認
- 伸びきった瞬間を検出したら転送実行
- **転送ロジックのみに集中**

### 伸びきった瞬間の検出

```csharp
public class TrainPlatformItemTransferComponent : IUpdatableBlockComponent
{
    private readonly TrainPlatformArmComponent _armComponent;
    private bool _wasFullyExtended;

    public void Update()
    {
        var isFullyExtended = _armComponent.IsFullyExtended;

        // 伸びきった瞬間を検出
        if (isFullyExtended && !_wasFullyExtended)
        {
            ExecuteTransfer();
        }
        _wasFullyExtended = isFullyExtended;
    }

    private void ExecuteTransfer()
    {
        if (_armComponent.Direction == TransferDirection.ToTrain)
        {
            TransferToTrainCar();
        }
        else
        {
            TransferToPlatform();
        }
    }
}
```

## ArmComponent実装イメージ

```csharp
public class TrainPlatformArmComponent : IUpdatableBlockComponent
{
    private readonly TrainPlatformDockingComponent _dockingComponent;
    private ArmState _armState = ArmState.Idle;
    private int _armProgressTicks;
    private readonly int _armAnimationTicks;

    public TransferDirection Direction { get; private set; }
    public bool IsFullyExtended => _armProgressTicks >= _armAnimationTicks
                                   && _armState == ArmState.Extending;

    public void Update()
    {
        var isDocked = _dockingComponent.IsDocked;

        switch (_armState)
        {
            case ArmState.Idle:
                if (isDocked) StartExtending();
                break;

            case ArmState.Extending:
                if (!isDocked) { StartRetractingFromCurrent(); break; }
                if (_armProgressTicks < _armAnimationTicks) { _armProgressTicks++; break; }
                StartRetractingFromFull();
                break;

            case ArmState.Retracting:
                if (_armProgressTicks > 0) { _armProgressTicks--; break; }
                _armState = ArmState.Idle;
                break;
        }
    }

    public void SetDirection(TransferDirection direction)
    {
        Direction = direction;
    }
}
```

## ブロック構成例

### 旅客駅
```
TrainStationComponent + TrainPlatformDockingComponent
```

### 貨物プラットフォーム（アイテムのみ）
```
TrainPlatformDockingComponent
+ TrainPlatformContainerComponent
+ TrainPlatformArmComponent
+ TrainPlatformItemTransferComponent
```

### 貨物プラットフォーム（アイテム + 液体）
```
TrainPlatformDockingComponent
+ TrainPlatformContainerComponent
+ TrainPlatformArmComponent
+ TrainPlatformItemTransferComponent
+ TrainPlatformFluidTransferComponent
```

## メリット

1. **単一責任**: 各コンポーネントが1つの責務のみ持つ
2. **組み合わせ可能**: ブロックの種類に応じてコンポーネントを組み合わせ
3. **拡張性**: 新しいTransferComponent（液体、乗客など）を追加しやすい
4. **テスト容易**: 各コンポーネントを独立してテスト可能
5. **依存が一方向**: TransferComponent → ArmComponent → DockingComponent
