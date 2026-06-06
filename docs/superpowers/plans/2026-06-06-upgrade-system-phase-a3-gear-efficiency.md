# アップグレードシステム フェーズA3（GearMachine 省エネ適用）実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **API整合の前提:** API名（`MachineModuleEffect.PowerMultiplier` / `VanillaMachineProcessorComponent` の効果スナップショット）は Phase A 計画（`2026-06-05-upgrade-system-phase-a.md`、特に A3-1/A3-2/A3-3）に追従する。Phase A 実装でシグネチャが変わったら本プランも同期。

**Goal:** 省エネ（Efficiency）モジュール効果を、歯車機械が動力網へ要求する消費トルクの削減にも適用する。Phase A の A3-3 は電力機械の要求電力にしか省エネを適用しておらず、設計仕様§5.1「全機械共通」が歯車機械で成立していないのを解消する。

**Architecture:** 歯車機械の消費は `GearEnergyTransformer.GetRequiredTorque(rpm, isClockwise)` が決める（`VanillaGearMachineTemplate` で生成、`VanillaGearMachineComponent` が保持）。`GetRequiredTorque` は `virtual`。機械専用サブクラス `MachineGearEnergyTransformer` を作り、`VanillaMachineProcessorComponent` が処理開始時にスナップショットした消費倍率（`MachineModuleEffect.PowerMultiplier`）を要求トルクに乗じる。要求トルクの削減＝動力網への負荷減＝省エネ（処理スループットは別経路 `GetCurrentSuppliedPower` なので不変）。

**Tech Stack:** Unity / C# / NUnit / uloop CLI

**設計仕様:** `docs/superpowers/specs/2026-06-05-upgrade-system-design.md`（§5.1 効果軸）
**前提プラン:** `2026-06-05-upgrade-system-phase-a.md`（A3-3 まで完了していること）

---

## ⚠ 設計改訂（2026-06-06）— サブクラスを作らない（ユーザーレビュー反映）

**当初の `MachineGearEnergyTransformer : GearEnergyTransformer` サブクラス案は不採用。** 既存の歯車関連コンポーネントから要求トルクを変えれば足りる。以降のタスク群 A3g-2-2（サブクラス実装）と A3g-2-3（生成順入れ替え）は下記で置き換える。

**改訂後の実装:**
1. 既存 `GearEnergyTransformer`（実ファイル名 `GearEnergyTransformerComponent.cs`）に**消費倍率プロバイダ**を足す。デフォルトは中立 1.0 なので他の歯車部品（発電機・コンベア等）に無影響:
   ```csharp
   private System.Func<float> _consumptionMultiplier = () => 1f;
   public void SetConsumptionMultiplier(System.Func<float> provider) => _consumptionMultiplier = provider;
   // GetRequiredTorque 内で baseTorque に _consumptionMultiplier() を乗じる
   ```
2. `VanillaGearMachineComponent`（既に processor と transformer を保持し仲介している）が配線する:
   ```csharp
   _gearEnergyTransformer.SetConsumptionMultiplier(() => _vanillaMachineProcessorComponent.CurrentPowerMultiplier);
   ```
   倍率は**トルク照会時にスナップショット値を読む**（`OnGearUpdate` での push は処理サイクルに対して古くなりうるため、プロバイダ経由で都度読む）。
3. `VanillaMachineProcessorComponent.CurrentPowerMultiplier`（処理中はスナップショット倍率、Idle 時 1.0）を公開する点は当初どおり（タスク群 A3g-1）。

**改訂後のファイル変更（A3）:**
- 修正: `Game.Block/Blocks/Gear/GearEnergyTransformerComponent.cs`（消費倍率プロバイダ・既定1.0）
- 修正: `Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs`（プロバイダ配線）
- 修正: `Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`（`CurrentPowerMultiplier` 公開）
- 新規: なし（`MachineGearEnergyTransformer.cs` は作らない）
- `VanillaGearMachineTemplate.cs` の生成順入れ替えも不要（サブクラスを作らないため）

**テストは当初どおり**「省エネ装着の歯車機械が未装着より小さい要求トルクを出す／未装着は中立／下限clamp」をサーバー統合テストで検証（タスク群 A3g-2-1・A3g-3 はそのまま有効）。

> 以降の「調査済みの実パターン」「ファイル構成」とタスク群 A3g-2-2/A3g-2-3 は、上の改訂で置き換わる箇所を含む歴史的記録。実装は本改訂ブロックに従うこと。

---

## 調査済みの実パターン（着手前に必読）

- **消費の算出点:** `Game.Block/Blocks/Gear/GearEnergyTransformer.cs:45` `public virtual Torque GetRequiredTorque(RPM rpm, bool isClockwise)` → `GearConsumptionCalculator.CalcRequiredTorque(_consumption, rpm)`。`_consumption == null`（発電機）は常に 0 を返す。
- **スループット経路は別:** 処理速度に効く `GetCurrentOperatingRate()` / `GetCurrentSuppliedPower()` は実RPM/トルク（`CurrentRpm`/`CurrentTorque`）から計算。`GetRequiredTorque` を下げてもスループット計算式自体は変わらない（=省エネはスループットを変えない、Factorio準拠）。
- **歯車機械の組み立て:** `Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs`
  - L45 `var gearConsumption = machineParam.GearConsumption;`
  - L46 `var gearEnergyTransformer = new GearEnergyTransformer(gearConsumption, blockInstanceId, gearConnector);`
  - L52 `var processor = ... new VanillaMachineProcessorComponent(input, output, null, requirePower) ...`
  - L57 `var machineComponent = new VanillaGearMachineComponent(processor, gearEnergyTransformer);`
  - **生成順の注意:** transformer(L46) が processor(L52) より先に作られている。`MachineGearEnergyTransformer` が processor を参照するには、**生成順を入れ替える**（processor を先に作る）。requirePower は gearConsumption から計算でき先に求まるので入れ替え可能。
- **電力機械側の対応物（A3-3）:** 電力機械は要求電力に `PowerMultiplier` を掛けた `EffectiveRequestPower` を返す。歯車機械では「要求トルクに `PowerMultiplier` を掛ける」が対応物。両者の倍率源は `VanillaMachineProcessorComponent` の効果スナップショット（同一）。

---

## ファイル構成

**新規:**
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineGearEnergyTransformer.cs` — `GearEnergyTransformer` を継承し `GetRequiredTorque` に省エネ倍率を適用

**修正:**
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` — 現在の消費倍率を公開するアクセサ（`CurrentPowerMultiplier`。処理中はスナップショット値、Idle時は 1.0）
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs` — processor を先に生成し `MachineGearEnergyTransformer` を使う

**テスト:**
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearMachineEfficiencyTest.cs`（新規）

---

# タスク群A3g-1: プロセッサに消費倍率アクセサを公開

ゴール: 歯車側が「いま適用すべき消費倍率」を processor から読める。

### Task A3g-1-1: `CurrentPowerMultiplier` を確認/追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`

- [ ] **Step 1: A3-3 の実装を確認**

Read: `VanillaMachineProcessorComponent.cs`。A3-3 で効果スナップショット（`_currentEffect`）と電力用の倍率適用がどう実装されたか確認。`PowerMultiplier` を外から読む手段が既にあるか（`EffectiveRequestPower` は電力値であって倍率ではない点に注意）。

- [ ] **Step 2: 消費倍率アクセサを追加（無ければ）**

単純 getter プロパティは規約上 `SetHoge` 不要の読み取り専用としてOK（値の Set はしない計算プロパティ）。処理中はスナップショット倍率、Idle時は中立 1.0:

```csharp
// 現在適用中の消費倍率。処理中は開始時スナップショット、非処理時は中立(1.0)
// Current power multiplier; snapshot value while processing, neutral (1.0) when idle
public float CurrentPowerMultiplier => _currentEffect != null && CurrentState == ProcessState.Processing
    ? _currentEffect.PowerMultiplier
    : 1f;
```

> `_currentEffect` / `CurrentState` / `ProcessState` の正確な名前は A3-2/A3-3 実装で確認して合わせる。`_currentEffect` がフィールド名でない場合（A3で別名になった場合）は実名に置換。

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 4: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs
git commit -m "feat(block): expose CurrentPowerMultiplier on machine processor"
```

---

# タスク群A3g-2: 機械専用 GearEnergyTransformer

ゴール: 歯車機械の要求トルクに省エネ倍率がかかる。

### Task A3g-2-1: 失敗するテストを書く

**Files:**
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearMachineEfficiencyTest.cs`

- [ ] **Step 1: テストを書く**

省エネモジュールを挿した歯車機械が処理中、未装着より小さい要求トルクを出すことを検証。

```csharp
using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Gear.Common;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearMachineEfficiencyTest
    {
        // 省エネモジュール装着の歯車機械が、未装着より小さい要求トルクを出すことを検証
        // Verify a gear machine with an efficiency module requests less torque than one without
        [Test]
        public void EfficiencyModuleReducesRequiredTorqueTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // GearConsumption を持つ歯車機械レシピ/ブロックを使う
            // Use a gear machine block that has GearConsumption
            var blockId = ForUnitTestModBlockId.GearMachineId; // 無ければ A3g-2 Step0 で用意
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new Vector3Int(0,0,0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var plain);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, new Vector3Int(5,0,0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var boosted);

            var efficiency = MasterHolder.ModuleMaster.Modules.Data.First(m => m.EffectAxis == "Efficiency");
            var effItemId = MasterHolder.ItemMaster.GetItemId(efficiency.ItemGuid);
            boosted.GetComponent<IModuleSlotInventoryComponent>().TryInsertModule(0, itemStackFactory.Create(effItemId, 1));

            // 両機械を処理中状態にする（入力投入＋電力相当の歯車供給）。詳細は既存 GearMachine テストを踏襲
            // Drive both into Processing (insert inputs + supply gear power). Follow existing GearMachine tests
            StartProcessing(plain); StartProcessing(boosted);

            var rpm = new RPM(100);
            var plainTorque = plain.GetComponent<IGearEnergyTransformer>().GetRequiredTorque(rpm, true);
            var boostedTorque = boosted.GetComponent<IGearEnergyTransformer>().GetRequiredTorque(rpm, true);

            Assert.Less((float)boostedTorque.AsPrimitive(), (float)plainTorque.AsPrimitive());

            #region Internal
            void StartProcessing(IBlock block)
            {
                // 既存の GearMachine 処理開始テスト（grep "VanillaGearMachineComponent" Tests）の手順を流用
            }
            #endregion
        }
    }
}
```

> `Torque` の数値取り出し（`AsPrimitive()` 等）、`RPM` コンストラクタ、`IGearEnergyTransformer` の `GetComponent` 可否、歯車機械を Processing にする手順は、既存の歯車機械テスト（`grep -rl "VanillaGearMachineComponent\|GetRequiredTorque" moorestech_server/Assets/Scripts/Tests`）を Read して正確に合わせる。テスト用歯車機械ブロックIDが無ければ Step0 で `ForUnitTestModBlockId` とテストmod blocks.json に `GearMachine` + `moduleSlotCount` を追加（Phase A A1-5 と同手順）。

- [ ] **Step 2: 失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearMachineEfficiencyTest"`
Expected: FAIL（省エネ未適用なので両トルク同値、または `MachineGearEnergyTransformer` 未配線）。

### Task A3g-2-2: `MachineGearEnergyTransformer` を実装

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineGearEnergyTransformer.cs`

- [ ] **Step 1: サブクラスを実装**

```csharp
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.GearConsumptionModule;

namespace Game.Block.Blocks.Machine
{
    // 機械専用の歯車消費。省エネモジュール効果を要求トルクに乗じる
    // Machine-specific gear consumer; applies the efficiency module multiplier to required torque
    public class MachineGearEnergyTransformer : GearEnergyTransformer
    {
        private readonly VanillaMachineProcessorComponent _processor;

        public MachineGearEnergyTransformer(
            GearConsumption consumption, BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent,
            VanillaMachineProcessorComponent processor)
            : base(consumption, blockInstanceId, connectorComponent)
        {
            _processor = processor;
        }

        public override Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            // 基準の要求トルクに、処理中の省エネ倍率を乗じる
            // Multiply base required torque by the current efficiency multiplier while processing
            var baseTorque = base.GetRequiredTorque(rpm, isClockwise);
            return new Torque((float)baseTorque.AsPrimitive() * _processor.CurrentPowerMultiplier);
        }
    }
}
```

> `GearEnergyTransformer` のコンストラクタ引数（`GearConsumption consumption, BlockInstanceId blockInstanceId, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent`）は実コードと一致（L30 で確認済み）。`Torque` のコンストラクタ／数値取り出しの正確な綴りは `Torque` 型定義を Read して合わせる（`AsPrimitive()` 仮）。`_consumption` が `private` で base から要求トルクを得られるのは `base.GetRequiredTorque` 経由なので問題ない。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

### Task A3g-2-3: `VanillaGearMachineTemplate` で配線（生成順入れ替え）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs`

- [ ] **Step 1: processor を先に生成し、transformer を機械専用に差し替え**

現状（L44-57）の順序を、processor 生成 → transformer 生成へ入れ替える:

```csharp
// 動力網コネクタと消費定義
// Gear network connector and consumption definition
var gearConnector = new BlockConnectorComponent<IGearEnergyTransformer>(connectSetting, connectSetting, blockPositionInfo);
var gearConsumption = machineParam.GearConsumption;
var requirePower = (float)(gearConsumption.BaseTorque * gearConsumption.BaseRpm);

// プロセッサを先に生成（効果スナップショットの保有者）
// Create the processor first (owner of the effect snapshot)
var processor = componentStates == null
    ? new VanillaMachineProcessorComponent(input, output, null, requirePower)
    : BlockTemplateUtil.MachineLoadState(componentStates, input, output, requirePower, blockMasterElement);

// 機械専用の歯車消費（省エネ倍率を適用）
// Machine-specific gear consumer (applies efficiency multiplier)
var gearEnergyTransformer = new MachineGearEnergyTransformer(gearConsumption, blockInstanceId, gearConnector, processor);
```

`using Game.Block.Blocks.Machine;` は既存（L5）。後続の `VanillaGearMachineComponent(processor, gearEnergyTransformer)` 等はそのまま。`components` への追加に `gearConnector` / `gearEnergyTransformer` / モジュールスロット（Phase A A2-2 Step4で追加済み）が含まれることを確認。

- [ ] **Step 2: Unity再起動 → コンパイル**

Run: Unity再起動後 `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 3: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearMachineEfficiencyTest"`
Expected: PASS（boosted < plain）。

- [ ] **Step 4: 回帰確認（既存歯車機械テスト）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearMachine|FuelGearGenerator"`
Expected: 既存PASS維持（省エネ未装着時は倍率1.0で従来と同値）。

- [ ] **Step 5: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineGearEnergyTransformer.cs \
  moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearMachineEfficiencyTest.cs
git commit -m "feat(block): apply efficiency module to gear machine torque consumption"
```

---

# タスク群A3g-3: clamp と中立性の確認

ゴール: 極端な省エネ構成でも要求トルクが下限を割らず、未装着時は完全中立。

### Task A3g-3-1: 下限clampと中立テスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearMachineEfficiencyTest.cs`

- [ ] **Step 1: テストを追加**

`MachineModuleEffect.PowerMultiplier` は A3-1 で下限 `MinPowerMultiplier`（0.1）にclamp済み。要求トルクがそれ以下に潰れないこと、未装着時は base と完全一致（倍率1.0）することを検証。

```csharp
        // 省エネ未装着時は要求トルクが基準値と一致（中立）することを検証
        // Verify required torque equals the base value when no efficiency module is equipped (neutral)
        [Test]
        public void NoModuleIsNeutralForGearTorqueTest()
        {
            // ... plain 機械を Processing にし、GetRequiredTorque が base 計算と一致することを確認 ...
        }
```

- [ ] **Step 2: 実行 → PASS 確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GearMachineEfficiencyTest"`
Expected: 全PASS。

- [ ] **Step 3: コミット**

```bash
cd ~/moorestech
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearMachineEfficiencyTest.cs
git commit -m "test(block): verify gear efficiency clamp and neutrality"
```

---

## フェーズA3完了条件

- [ ] 省エネモジュールが歯車機械の要求トルクを削減する（処理中スナップショット倍率を適用）
- [ ] 未装着時は完全中立（倍率1.0、既存挙動と一致）
- [ ] 下限clamp（`MinPowerMultiplier`）が要求トルクにも効く
- [ ] 既存歯車機械テストの回帰なし
- [ ] 設計仕様§5.1「全機械共通」が電力・歯車の両機械で成立

## 補足（バランス確認の留意点）

要求トルク削減がスループット（`GetCurrentOperatingRate`/`GetCurrentSuppliedPower`）に与える間接影響は、動力網の供給配分次第。本プランは「要求トルクが下がる」ことをテスト不変条件とし、処理速度の体感はバランス調整（仕様§9）として実プレイで確認する。処理時間効果（速度モジュール）は Phase A で既に両機械に効く。
