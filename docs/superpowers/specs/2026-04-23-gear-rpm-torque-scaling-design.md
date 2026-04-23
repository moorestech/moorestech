# Gear系ブロック RPM依存トルク消費 設計仕様

**作成日:** 2026-04-23
**対象:** Gear系全消費ブロックのトルク消費をRPMに応じて非線形スケールさせ、出力（加工速度・搬送速度・発電量等）をRPM比に線形連動させる。低RPMでは省エネ・低速、高RPMでは高消費・高速となるトレードオフを作る。

---

## 1. 背景と目的

### 現状
- `GearBeltConveyor` のみ `Torque = RPM × requireTorquePerRpm` のRPM比例消費
- `Gear`, `Shaft`, `GearChainPole`, `GearMachine`, `GearMiner`, `GearMapObjectMiner`, `GearPump`, `GearElectricGenerator` は **固定** `requireTorque` 消費
- 機械類は `requiredRpm` 未満で完全停止、`requiredRpm` 以上では上限クランプされ、RPMを上げても加工速度が変わらない

### 目的
- 全Gear系消費コンポーネントで、RPMに応じたトルク消費・出力スケールを導入
- 基準RPM（`baseRpm`）からの比率で消費・出力を計算
- `RPM比 = currentRpm / baseRpm`
- 消費: `baseTorque × f(RPM比)`（非線形）
- 出力: `baseOutput × RPM比`（線形）
- 結果として **「2倍速で3倍消費」** のようなトレードオフが成立

### 非対象
- オーバーロード破壊パラメータ（`overloadMaxRpm`, `overloadMaxTorque`）の再バランス
- 生成側コンポーネント（`SimpleGearGenerator`, `FuelGearGenerator`）の変更
- クライアント側の視覚挙動（RPM同期済み、既存プロトコルで対応可）

### 完了条件
全Gear系消費コンポーネントが共通の式でRPM依存トルク消費・出力スケールを行い、以下がマスタで設定可能:
- `baseRpm` / `minimumRpm` / `baseTorque` / `torqueExponentUnder` / `torqueExponentOver`

---

## 2. コア計算式

### パラメータ

| 名前 | 役割 | デフォルト |
|---|---|---|
| `baseRpm` | 基準RPM。ここで消費=`baseTorque`、出力=`baseOutput` | 各ブロック毎 |
| `minimumRpm` | これ未満で完全停止 | 各ブロック毎 |
| `baseTorque` | 基準RPM時のトルク消費量 | 各ブロック毎 |
| `torqueExponentUnder` | RPM比 ≤ 1 のときの指数 (`b`) | `2` |
| `torqueExponentOver` | RPM比 > 1 のときの指数 (`c`) | `1.585` |

### 式

```
rpmRatio = currentRpm / baseRpm

if currentRpm < minimumRpm:
    requiredTorque = 0
    operatingRate  = 0
else:
    f(x) = x^torqueExponentUnder  (x ≤ 1)
    f(x) = x^torqueExponentOver   (x > 1)
    requiredTorque = baseTorque × f(rpmRatio)
    torqueRate     = min(currentTorque / requiredTorque, 1)
    operatingRate  = rpmRatio × torqueRate
```

### 検算

| currentRpm / baseRpm | f(x) (消費倍率) | 出力倍率 |
|---|---|---|
| 0.5 | `0.5^2 = 0.25` | 0.5 |
| 1.0 | 1.0 | 1.0 |
| 2.0 | `2^1.585 ≈ 3.0` | 2.0 |
| `< minimumRpm/baseRpm` | 0 | 0 |

---

## 3. スキーマ変更

### 3-1. 新規共通スキーマ `VanillaSchema/ref/gearConsumption.yml`

```yaml
id: gearConsumption
type: object
properties:
- key: baseRpm
  type: number
  default: 5
- key: minimumRpm
  type: number
  default: 5
- key: baseTorque
  type: number
  default: 1
- key: torqueExponentUnder
  type: number
  default: 2
- key: torqueExponentOver
  type: number
  default: 1.585
```

SourceGeneratorが自動生成する C# 型（命名は既存規約に従う。候補: `GearConsumptionElement` または `GearConsumption`）を基底クラスで保持する。

### 3-2. `VanillaSchema/blocks.yml` の変更

各Gear系消費ブロックに `- key: gearConsumption / ref: gearConsumption` を追加し、既存の個別キーを削除。

| ブロック | 削除するキー | 追加 |
|---|---|---|
| Gear | `requireTorque` | `gearConsumption` ref |
| Shaft | `requireTorque` | `gearConsumption` ref |
| GearChainPole | — | `gearConsumption` ref（`baseTorque=0`で運用） |
| GearMachine | `requireTorque`, `requiredRpm` | `gearConsumption` ref |
| GearMiner | `requireTorque`, `requiredRpm` | `gearConsumption` ref |
| GearMapObjectMiner | `requireTorque`, `requiredRpm` | `gearConsumption` ref |
| GearPump | `requireTorque`, `requiredRpm` | `gearConsumption` ref |
| GearBeltConveyor | `requireTorquePerRpm` | `gearConsumption` ref |
| GearElectricGenerator | `requiredTorque`, `requiredRpm` | `gearConsumption` ref |

`defineInterface` の `IGearMachineParam`（中身が `requireTorque`/`requiredRpm` のみ）は **削除**。

### 3-3. デフォルト値方針

- `baseRpm`: 既存 `requiredRpm` の値を踏襲（GearMachine=5, GearElectricGenerator=120 等）。`requiredRpm` が無いブロックは以下を初期値とし、ゲームバランス側で後日調整
  - `Gear`, `Shaft`: `5`（一般的な機械と同等）
  - `GearChainPole`: `5`（消費が常に0なので値は意味を持たない）
  - `GearBeltConveyor`: `500`（既存式 `Torque = RPM × 0.002` で定格1となるRPM）
- `minimumRpm`: 基本 `baseRpm` と同値（既存の停止閾値挙動を維持）。低回転運転を許容したければ下げる
- `baseTorque`: 既存 `requireTorque` / `requiredTorque` の値を踏襲。`GearBeltConveyor` は `1`（上記 `baseRpm=500` と対応）、`GearChainPole` は `0`
- `torqueExponentUnder` / `torqueExponentOver`: 共通デフォルト 2 / 1.585

### 3-4. JSONマスタデータ移行

`../moorestech_master/server_v8/blocks/*.json` 内の各Gear系ブロックJSONを以下のように書き換え:

**旧（GearMachine例）:**
```json
{
  "requireTorque": 1,
  "requiredRpm": 5
}
```

**新:**
```json
{
  "gearConsumption": {
    "baseRpm": 5,
    "minimumRpm": 5,
    "baseTorque": 1
  }
}
```

デフォルト値で足りるフィールド（`torqueExponentUnder` 等）は省略可。

---

## 4. 実装戦略

### 4-1. 基底クラス `GearEnergyTransformerComponent` の変更

**配置:** `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearEnergyTransformerComponent.cs`

**変更内容:**
- コンストラクタが `GearConsumptionElement` を1つ受け取る形に変更（固定 `Torque` 引数を廃止）
- 5パラメータを保持するフィールドを追加
- `GetRequiredTorque(currentRpm, isClockwise)` を新式で実装
- `CurrentOperatingRate` プロパティを新設

```csharp
public Torque GetRequiredTorque(RPM currentRpm, bool isClockwise)
{
    if (currentRpm.AsPrimitive() < _consumption.MinimumRpm) return new Torque(0);

    var x = currentRpm.AsPrimitive() / _consumption.BaseRpm;
    var exp = x <= 1f ? _consumption.TorqueExponentUnder : _consumption.TorqueExponentOver;
    return new Torque(_consumption.BaseTorque * Mathf.Pow(x, exp));
}

public float CurrentOperatingRate
{
    get
    {
        if (CurrentRpm.AsPrimitive() < _consumption.MinimumRpm) return 0f;
        var rpmRatio = CurrentRpm.AsPrimitive() / _consumption.BaseRpm;
        var required = GetRequiredTorque(CurrentRpm, CurrentIsClockwise).AsPrimitive();
        var torqueRate = required <= 0f ? 0f : Mathf.Min(CurrentTorque.AsPrimitive() / required, 1f);
        return rpmRatio * torqueRate;
    }
}
```

- 従来 `virtual` だった `GetRequiredTorque` は non-virtual（または sealed）に変更
- `GearBeltConveyorComponent` の `GetRequiredTorque` override は削除

### 4-2. 出力側コンポーネントの変更

各コンポーネントで基底の `CurrentOperatingRate` を参照する形に書き換え。

| コンポーネント | ファイル | 変更内容 |
|---|---|---|
| `VanillaGearMachineComponent` | `Machine/VanillaGearMachineComponent.cs` | 既存は `CalcMachineSupplyPower(requiredRpm, requireTorque)` で得た `currentElectricPower` を `IMachineProcessor` に渡して加工を進める。新方式では `CurrentOperatingRate` をそのまま加工進捗倍率として `IMachineProcessor` の進捗ステップに乗算する。基底クラスの `CalcMachineSupplyPower` は廃止 |
| `VanillaGearMinerComponent` | `Miner/VanillaGearMinerComponent.cs` | 採掘進捗 × `CurrentOperatingRate` |
| `VanillaGearMapObjectMinerComponent` | `MapObjectMiner/VanillaGearMapObjectMinerComponent.cs` | 同上 |
| `GearPumpComponent` | `Gear/GearPumpComponent.cs` | 液体排出量 × `CurrentOperatingRate` |
| `GearBeltConveyorComponent` | `BeltConveyor/GearBeltConveyorComponent.cs` | `SupplyPower()` 内の速度計算を `speed = beltConveyorSpeed × CurrentOperatingRate` に変更 |
| `GearElectricGeneratorComponent` | `GearElectric/GearElectricGeneratorComponent.cs` | `UpdateGeneratedPower()` の `fulfillment = CurrentOperatingRate`（クランプ撤廃）、発電量 = `maxGeneratedPower × CurrentOperatingRate` |

### 4-3. 伝動専用コンポーネント

| コンポーネント | 変更 |
|---|---|
| `GearComponent` | `GearConsumptionElement` を基底に渡すのみ |
| `VanillaShaftTemplate` 経由の `GearEnergyTransformer` | 同上 |
| `GearChainPoleComponent` | 同上。`baseTorque=0` 設定で実質消費ゼロ維持 |

### 4-4. Template クラス群の変更

以下のTemplateで `BlockParam.GearConsumption` を取得し、コンポーネントコンストラクタへ渡すように修正:
- `VanillaGearMachineTemplate`
- `VanillaGearMinerTemplate`
- `VanillaGearMapObjectMinerTemplate`
- `VanillaGearPumpTemplate`
- `VanillaGearBeltConveyorTemplate`
- `VanillaGearElectricGeneratorTemplate`
- `VanillaGearTemplate`
- `VanillaShaftTemplate`
- `VanillaGearChainPoleTemplate`

### 4-5. オーバーロード破壊との整合

`GearOverloadBreakageComponent` は `IGearOverloadParam` の `overloadMaxRpm` / `overloadMaxTorque` をそのまま利用（上限超過で破壊判定）。本タスクでは変更しない。高RPM運転時の消費トルク増大でオーバーロード閾値に到達しやすくなる可能性はあるが、再バランスは後続タスクに委譲。

### 4-6. クライアント側

- RPM・トルクの同期プロトコルは既存のまま
- 機械の加工速度視覚表現（もし存在すれば）は、サーバー側の `operatingRate` をUI表示する必要があるかもしれない。テスト実装時に調査し、必要なら追加タスク化

---

## 5. テスト戦略

### 5-1. ユニットテスト（新規）

**配置:** `moorestech_server/Assets/Scripts/Tests.UnitTest.Game/GearNetwork/GearConsumptionFormulaTest.cs`

テストケース:

| ケース | 条件 | 期待値 |
|---|---|---|
| 定格 | currentRpm=baseRpm=100, baseTorque=1 | requiredTorque=1.0 |
| 半速 | currentRpm=50, baseRpm=100, b=2 | requiredTorque=0.25 |
| 倍速 | currentRpm=200, baseRpm=100, c=1.585 | requiredTorque≈3.0（誤差 < 0.01）|
| 下限未満 | currentRpm=10, minimumRpm=20 | requiredTorque=0 |
| 下限ぴったり | currentRpm=20, minimumRpm=20, baseRpm=100, b=2 | requiredTorque=0.04 |
| operatingRate 倍速満タン | currentRpm=200, 供給=required | 2.0 |
| operatingRate 倍速トルク半分 | 供給=required×0.5 | 1.0 |
| operatingRate 下限未満 | — | 0 |
| 指数カスタム | b=3, c=2 等 | 式に従う |

### 5-2. 統合テスト

**既存テスト更新:**
- `GearBeltConveyor` 搬送速度テスト: 定格/半速/倍速の3点で新式に従うことを検証
- `GearMachine` 加工時間テスト: RPM比に線形で加工時間が縮むことを検証
- `GearElectricGenerator` 発電量テスト: 1超クランプ撤廃後の挙動を検証
- `GearNetworkTest` 等のネットワーク収支テスト: 新パラメータでの期待値に更新

**新規テスト:**
- 高RPM運転でネットワーク要求トルクが `f(x)` に従って増える
- 供給不足時に出力が `torqueRate` で減衰
- `minimumRpm` 未満で停止 → ネットワーク要求から外れ、他機械の供給が増える

### 5-3. マスタデータ整合検証

- `uloop compile --project-path ./moorestech_server` でSourceGenerator再生成成功
- `../moorestech_master/server_v8/blocks/*.json` 書き換え後、マスタロード系テストでバリデーションエラーなし

### 5-4. 手動確認（Unity）

`uloop-launch` でクライアント起動し、サンプルシーンで:
- 発電機出力を下げて低RPM → 機械ゆっくり動作、トルク消費減
- 発電機を強化して高RPM → 機械高速化、消費急増
- 容量限界まで追加 → `minimumRpm` 未満で複数機械が一斉停止

---

## 6. 作業範囲サマリ

### ファイル追加
- `VanillaSchema/ref/gearConsumption.yml`
- `moorestech_server/Assets/Scripts/Tests.UnitTest.Game/GearNetwork/GearConsumptionFormulaTest.cs`

### ファイル編集
- `VanillaSchema/blocks.yml`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearEnergyTransformerComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Gear/GearPumpComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/BeltConveyor/GearBeltConveyorComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Miner/VanillaGearMinerComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/MapObjectMiner/VanillaGearMapObjectMinerComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearChainPole/GearChainPoleComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/GearElectric/GearElectricGeneratorComponent.cs`
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGear*Template.cs`（9ファイル）
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaShaftTemplate.cs`
- `../moorestech_master/server_v8/blocks/*.json`（Gear系ブロック分）
- 既存のGear系統合テスト各種

### 削除
- `IGearMachineParam` インターフェース定義（`blocks.yml` の `defineInterface` から）
- `GearBeltConveyorComponent.GetRequiredTorque` の override
