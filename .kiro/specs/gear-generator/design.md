# 設計書

## 概要
歯車発電機は、歯車ネットワークから受け取った機械的エネルギー（RPMとトルク）を電力に変換するブロックコンポーネントです。エネルギー充足率に対して線形な発電特性を持ち、プレイヤーが予測可能で理解しやすい発電システムを提供します。

## アーキテクチャ設計

### クラス構成

```
GearElectricGeneratorComponent
├── IGearEnergyTransformer (インターフェース実装)
├── IElectricGenerator (インターフェース実装)
├── IUpdatableBlockComponent (インターフェース実装)
├── ISaveBlockComponent (インターフェース実装)
└── IBlockStateDetail (インターフェース実装)
```

### 主要インターフェース

#### IGearEnergyTransformer
歯車ネットワークからエネルギーを受け取るためのインターフェース
- `TeethCount`: 歯車の歯数
- `InputRpm`: 入力RPM（プロパティ）
- `InputTorque`: 入力トルク（プロパティ）
- `RequiredTorque`: 要求トルク（プロパティ）

#### IElectricGenerator
電力ネットワークに電力を供給するためのインターフェース
- `GeneratedElectricPower`: 発電量（プロパティ）
- `OnChangeGeneratedPower`: 発電量変更イベント

## 詳細設計

### クラス定義

```csharp
namespace Game.Block.Blocks.GearElectric
{
    public class GearElectricGeneratorComponent :
        IGearEnergyTransformer,
        IElectricGenerator,
        IUpdatableBlockComponent,
        ISaveBlockComponent,
        IBlockStateDetail
    {
        // パラメータ
        private readonly GearElectricGeneratorParam _param;

        // 状態
        private RPM _inputRpm;
        private Torque _inputTorque;
        private float _energyFulfillmentRate;
        private ElectricPower _currentGeneratedPower;

        // イベント
        public event Action<ElectricPower> OnChangeGeneratedPower;

        // コンストラクタ
        public GearElectricGeneratorComponent(
            GearElectricGeneratorParam param,
            BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo,
            Func<MachineIOService> machineIOServiceAccessor)
        {
            // 初期化処理
        }
    }
}
```
### 発電量計算ロジック

```csharp
private void UpdateGeneratedPower()
{
    // エネルギー充足率の計算
    var rpmRate = _inputRpm.Value / _param.RequiredRpm.Value;
    var torqueRate = _inputTorque.Value / _param.RequiredTorque.Value;

    // 充足率は各要素の積
    _energyFulfillmentRate = rpmRate * torqueRate;

    // 100%を超える場合はクリッピング
    if (_energyFulfillmentRate > 1.0f)
    {
        _energyFulfillmentRate = 1.0f;
    }

    // 線形な発電量計算
    var newGeneratedPower = new ElectricPower(
        _param.MaxGeneratedPower.Value * _energyFulfillmentRate
    );

    // 発電量が変化した場合はイベント発火
    if (Math.Abs(_currentGeneratedPower.Value - newGeneratedPower.Value) > 0.01f)
    {
        _currentGeneratedPower = newGeneratedPower;
        OnChangeGeneratedPower?.Invoke(_currentGeneratedPower);
    }
}
```

### ネットワーク接続処理

#### 歯車ネットワークとの接続
```csharp
public TeethCount TeethCount => _param.TeethCount;

public Torque RequiredTorque => new Torque(
    _param.RequiredTorque.Value * _energyFulfillmentRate
);

// 歯車ネットワークから入力を受け取る
public void SetGearInput(RPM rpm, Torque torque)
{
    _inputRpm = rpm;
    _inputTorque = torque;
    UpdateGeneratedPower();
}
```

#### 電力ネットワークとの接続
```csharp
public ElectricPower GeneratedElectricPower => _currentGeneratedPower;

// 電力ネットワークへの登録
private void RegisterToEnergyNetwork()
{
    var energySegment = GetConnectedEnergySegment();
    energySegment?.RegisterGenerator(this);
}
```


## ブロック配置と接続仕様

### 接続面の定義
- **背面（North）**: 歯車ネットワークからの動力入力
- **前面（South）**: 電力ネットワークへの電力出力
- **その他の面**: 接続不可

### 接続検証ロジック

```csharp
private bool ValidateConnections()
{
    // 背面に歯車接続があるか確認
    var gearConnection = GetConnectionAtDirection(BlockDirection.North);
    if (gearConnection == null || !gearConnection.IsGearNetwork)
    {
        return false;
    }

    // 前面に電力接続があるか確認
    var powerConnection = GetConnectionAtDirection(BlockDirection.South);
    if (powerConnection == null || !powerConnection.IsEnergyNetwork)
    {
        return false;
    }

    return true;
}
```

## パフォーマンス最適化

### 更新頻度の制御
```csharp
public void Update()
{
    _updateTimer += Time.deltaTime;

    // 0.1秒ごとに更新（10Hz）
    if (_updateTimer >= UpdateInterval)
    {
        _updateTimer = 0;
        UpdateGeneratedPower();
    }
}

private const float UpdateInterval = 0.1f;
```

### イベント最適化
- 発電量の変化が閾値（0.01 EP）以上の場合のみイベント発火
- 不要な再計算を避けるためのキャッシング

## 例外処理とエラーハンドリング

### 入力検証
```csharp
private bool ValidateInput()
{
    // 負の値チェック
    if (_inputRpm.Value < 0 || _inputTorque.Value < 0)
    {
        _currentGeneratedPower = ElectricPower.Zero;
        return false;
    }

    // 過負荷チェック（200%超）
    if (_inputRpm.Value > _param.RequiredRpm.Value * 2 ||
        _inputTorque.Value > _param.RequiredTorque.Value * 2)
    {
        // 警告表示
        ShowOverloadWarning();
    }

    return true;
}
```

## テスト計画

### ユニットテスト対象
1. エネルギー充足率計算の正確性
2. 線形発電特性の検証
3. クリッピング処理の動作
4. イベント発火条件の確認

### 統合テスト対象
1. 歯車ネットワークとの接続
2. 電力ネットワークとの接続
3. セーブ/ロード機能

## 実装優先順位

### Phase 1: 基本実装
1. コンポーネントクラスの基本構造
2. インターフェースの実装
3. エネルギー充足率と発電量計算

### Phase 2: ネットワーク統合
1. 歯車ネットワークとの接続
2. 電力ネットワークとの接続
3. 接続検証ロジック

## 依存関係

### 必要なNamespace
- `Game.Gear.Common` - RPM, Torque, TeethCount
- `Game.EnergySystem` - ElectricPower, IElectricGenerator
- `Game.Block.Interface` - 各種ブロックインターフェース
- `Core.Block` - BlockId, BlockInstanceId
- `MessagePack` - セーブデータのシリアライゼーション

### 参照する既存実装
- `SteamGearGeneratorComponent` - 歯車発電機の基本構造
- `VanillaElectricGeneratorComponent` - 電力生成の実装例
- `GearNetworkConnection` - 歯車ネットワーク接続処理

## スキーマ定義

### VanillaSchema/blocks.yml への追加内容

#### 1. blockTypeへの追加
```yaml
- key: blockType
  type: enum
  options:
  # ... 既存のオプション ...
  - GearElectricGenerator  # 追加
```

#### 2. blockParam内のswitch caseへの追加
```yaml
- when: GearElectricGenerator
  type: object
  implementationInterface:
  - IGearConnectors
  properties:
  - key: teethCount
    type: integer
    default: 10
  - key: requiredRpm
    type: number
    default: 120
  - key: requiredTorque
    type: number
    default: 100
  - key: maxGeneratedPower
    type: number
    default: 100
  - key: gear
    ref: gear
```

### moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json への追加内容

```json
{
  "blockGuid": "00000000-0000-0000-0000-000000000028",
  "itemGuid": "11110000-0000-0000-0000-000000000000",
  "name": "TestGearElectricGenerator",
  "blockType": "GearElectricGenerator",
  "blockSize": [1, 1, 1],
  "blockParam": {
    "teethCount": 10,
    "requiredRpm": 120,
    "requiredTorque": 100,
    "maxGeneratedPower": 100,
    "gear": {
      "gearConnects": [
        {
          "offset": [0, 0, 0],
          "connectType": "Gear",
          "connectOption": {
            "isReverse": false
          },
          "directions": [
            [0, 0, -1],
            [0, 0, 1],
            [1, 0, 0],
            [-1, 0, 0]
          ]
        }
      ]
    }
  },
  "overrideVerticalBlock": {}
}
```

### ForUnitTestModBlockId.cs への追加内容

```csharp
public static class ForUnitTestModBlockId
{
    // ... 既存の定義 ...

    // 歯車発電機のテスト用ID
    public static readonly BlockGuid TestGearElectricGenerator = new("00000000-0000-0000-0000-000000000028");
}
```

## マスターデータ設定

### 製品版向けブロック定義（VanillaMod/master/blocks.json）

```json
{
  "blockGuid": "d4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f9a",
  "itemGuid": "e5f6a7b8-c9d0-1e2f-3a4b-5c6d7e8f9a0b",
  "name": "歯車発電機",
  "blockType": "GearElectricGenerator",
  "blockSize": [1, 1, 1],
  "blockPrefabAddressablesPath": "Assets/Prefabs/Blocks/GearElectricGenerator.prefab",
  "blockUIAddressablesPath": "Assets/UI/Blocks/GearElectricGeneratorUI.prefab",
  "blockParam": {
    "teethCount": 20,
    "requiredRpm": 120,
    "requiredTorque": 100,
    "maxGeneratedPower": 100,
    "gear": {
      "gearConnects": [
        {
          "offset": [0, 0, 0],
          "connectType": "Gear",
          "connectOption": {
            "isReverse": false
          },
          "directions": [
            [0, 0, -1]
          ]
        }
      ]
    }
  },
  "overrideVerticalBlock": {}
}
```

## コンポーネント登録

### BlockFactoryへの登録