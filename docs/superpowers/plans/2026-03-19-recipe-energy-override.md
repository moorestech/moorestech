# レシピごとのエネルギー消費オーバーライド 実装計画

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** レシピごとにエネルギー消費量をオーバーライドできるようにし、ブロックデフォルトと異なる電力/歯車要件をレシピ単位で設定可能にする

**Architecture:** YAMLスキーマに`energyOverrideType`(enum) + `energyOverride`(switch)を追加し、SourceGeneratorで型安全なモデルを自動生成。`VanillaMachineProcessorComponent.RequestPower`を可変化してレシピ切り替え時にエネルギー値を動的更新。C#バリデーションでブロックタイプとオーバーライドタイプの整合性を保証。

**Tech Stack:** C# / Unity / YAMLスキーマ + SourceGenerator / NUnit

**Design Spec:** `docs/superpowers/specs/2026-03-19-recipe-energy-override-design.md`

---

## ファイル構成

### 変更するファイル
- `VanillaSchema/machineRecipes.yml` — スキーマにenergyOverride追加
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` — RequestPower可変化、レシピ切り替え時の更新
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs` — オーバーライド対応のRPM/Torque参照
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs` — Electricオーバーライド適用
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs` — Gearオーバーライド適用
- `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs` — ロード時のオーバーライド復元
- `moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs` — バリデーション追加
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json` — テスト用レシピデータにオーバーライド追加
- `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` — テスト用ブロックデータ（必要に応じて）

### 新規作成するファイル
- `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeEnergyOverrideResolver.cs` — レシピからオーバーライドされたElectricPowerを計算するヘルパー
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs` — オーバーライド機能のテスト

---

## Chunk 1: スキーマ変更とSourceGenerator再実行

### Task 1: YAMLスキーマ変更

**Files:**
- Modify: `VanillaSchema/machineRecipes.yml:14` (propertiesの末尾に追加)

- [ ] **Step 1: machineRecipes.ymlにenergyOverrideフィールドを追加**

`machineRecipes.yml`の`items.properties`の末尾（`outputFluids`の後）に以下を追加する:

```yaml
    - key: energyOverrideType
      type: enum
      options:
        - None
        - Electric
        - Gear
      default: None
    - key: energyOverride
      switch: ./energyOverrideType
      cases:
        - when: None
          type: object
          optional: true
          properties: []
        - when: Electric
          type: object
          properties:
            - key: requiredPower
              type: number
        - when: Gear
          type: object
          properties:
            - key: requireTorque
              type: number
              optional: true
            - key: requiredRpm
              type: number
              optional: true
```

- [ ] **Step 2: SandBox側スキーマも同期**

`mooresmaster/mooresmaster.SandBox/schema/machineRecipes.yml`にも同じ変更を適用（git subtree同期）。

- [ ] **Step 3: SourceGeneratorを実行して自動生成コードを更新**

Run: `dotnet build mooresmaster/mooresmaster.Generator/ -c release && dotnet test mooresmaster/`

既存テストがパスし、`MachineRecipeMasterElement`に`EnergyOverrideType`と`EnergyOverride`プロパティが生成されることを確認。

- [ ] **Step 4: コミット**

```bash
git add VanillaSchema/machineRecipes.yml mooresmaster/
git commit -m "feat: machineRecipesスキーマにenergyOverrideフィールドを追加"
```

---

### Task 2: テスト用JSONデータにenergyOverride追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json`

- [ ] **Step 1: 既存GearMachineレシピ(index 3)にGearオーバーライドを追加**

テストデータのindex 3のレシピ（blockGuid: `00000000-0000-0000-0000-00000000000f`、GearMachine）に以下を追加:

```json
"energyOverrideType": "Gear",
"energyOverride": {
  "requireTorque": 3,
  "requiredRpm": 10
}
```

- [ ] **Step 2: 既存ElectricMachineレシピ(index 0)にElectricオーバーライドを追加**

テストデータのindex 0のレシピ（blockGuid: `00000000-0000-0000-0000-000000000001`、ElectricMachine）に以下を追加:

```json
"energyOverrideType": "Electric",
"energyOverride": {
  "requiredPower": 50
}
```

他のレシピはデフォルト（`energyOverrideType`フィールドなし = None）のままで、既存互換性を確認。

- [ ] **Step 3: サーバーコンパイル確認**

Run (worktree): `./tools/unity-test.sh moorestech_server "^$"`

コンパイルが通ることを確認。SourceGeneratorで生成されたモデルに新フィールドが含まれ、JSONからロード可能であること。

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/
git commit -m "feat: テスト用レシピデータにenergyOverride設定を追加"
```

---

## Chunk 2: コア実装 — RequestPower可変化とRecipeEnergyOverrideResolver

### Task 3: RecipeEnergyOverrideResolverの作成

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeEnergyOverrideResolver.cs`

- [ ] **Step 1: RecipeEnergyOverrideResolverクラスを作成**

レシピのオーバーライド設定からElectricPowerを計算するstaticユーティリティ。

```csharp
using Game.EnergySystem;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     レシピのエネルギーオーバーライド設定からElectricPowerを解決するヘルパー
    ///     Helper to resolve ElectricPower from recipe energy override settings
    /// </summary>
    public static class RecipeEnergyOverrideResolver
    {
        /// <summary>
        ///     ElectricMachine用: レシピのオーバーライドからrequiredPowerを解決する
        ///     For ElectricMachine: resolve requiredPower from recipe override
        /// </summary>
        public static ElectricPower ResolveElectricPower(MachineRecipeMasterElement recipe, ElectricPower blockDefaultPower)
        {
            if (recipe == null) return blockDefaultPower;
            if (recipe.EnergyOverrideType != MachineRecipeMasterElement.EnergyOverrideTypeConst.Electric) return blockDefaultPower;

            // Electricケースの型にキャストしてrequiredPowerを取得
            // Cast to Electric case type and get requiredPower
            var electricOverride = (ElectricEnergyOverride)recipe.EnergyOverride;
            return new ElectricPower(electricOverride.RequiredPower);
        }

        /// <summary>
        ///     GearMachine用: レシピのオーバーライドからrequiredPowerを解決する（RPM * Torque）
        ///     For GearMachine: resolve requiredPower from recipe override (RPM * Torque)
        /// </summary>
        public static ElectricPower ResolveGearPower(MachineRecipeMasterElement recipe, float blockDefaultTorque, float blockDefaultRpm)
        {
            if (recipe == null) return new ElectricPower(blockDefaultTorque * blockDefaultRpm);

            var torque = blockDefaultTorque;
            var rpm = blockDefaultRpm;
            ResolveGearParams(recipe, ref torque, ref rpm);
            return new ElectricPower(torque * rpm);
        }

        /// <summary>
        ///     GearMachine用: レシピのオーバーライドからTorqueとRPMを個別に解決する
        ///     For GearMachine: resolve individual torque and RPM from recipe override
        /// </summary>
        public static void ResolveGearParams(MachineRecipeMasterElement recipe, ref float torque, ref float rpm)
        {
            if (recipe == null) return;
            if (recipe.EnergyOverrideType != MachineRecipeMasterElement.EnergyOverrideTypeConst.Gear) return;

            // Gearケースの型にキャストしてTorque/RPMを取得
            // Cast to Gear case type and get Torque/RPM
            var gearOverride = (GearEnergyOverride)recipe.EnergyOverride;
            if (gearOverride.RequireTorque.HasValue) torque = gearOverride.RequireTorque.Value;
            if (gearOverride.RequiredRpm.HasValue) rpm = gearOverride.RequiredRpm.Value;
        }
    }
}
```

> **IMPORTANT**: SourceGeneratorはenumを文字列定数として生成する（C# enumではない）。`EnergyOverrideType`は`string`プロパティで、比較には`MachineRecipeMasterElement.EnergyOverrideTypeConst.Electric`等のネストされた定数クラスを使用する。switchケースの型名（`ElectricEnergyOverride`, `GearEnergyOverride`）はSourceGenerator実行後に`MachineRecipesModule`名前空間で確認すること。optionalプロパティは`float?`（Nullable）として生成される想定。

- [ ] **Step 2: コンパイル確認**

Run (worktree): `./tools/unity-test.sh moorestech_server "^$"`

- [ ] **Step 3: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeEnergyOverrideResolver.cs
git commit -m "feat: RecipeEnergyOverrideResolverを追加"
```

---

### Task 4: VanillaMachineProcessorComponentのRequestPower可変化

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs:28,35,47,61,125-139`

- [ ] **Step 1: RequestPowerをreadonly解除し、ブロックデフォルト値を保持するフィールドを追加**

```csharp
// line 28: readonlyを外す
public ElectricPower RequestPower { get; private set; }

// line 28付近: ブロックデフォルト値を保持
private readonly ElectricPower _blockDefaultPower;
```

- [ ] **Step 2: コンストラクタでブロックデフォルト値も保持**

両方のコンストラクタで`_blockDefaultPower`を初期化:

```csharp
// デフォルトコンストラクタ (line 39-48)
public VanillaMachineProcessorComponent(
    VanillaMachineInputInventory vanillaMachineInputInventory,
    VanillaMachineOutputInventory vanillaMachineOutputInventory,
    MachineRecipeMasterElement machineRecipe, ElectricPower requestPower)
{
    _vanillaMachineInputInventory = vanillaMachineInputInventory;
    _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
    _processingRecipe = machineRecipe;
    _blockDefaultPower = requestPower;
    RequestPower = requestPower;
}

// ロードコンストラクタ (line 50-65)
public VanillaMachineProcessorComponent(
    VanillaMachineInputInventory vanillaMachineInputInventory,
    VanillaMachineOutputInventory vanillaMachineOutputInventory,
    ProcessState currentState, uint remainingTicks, MachineRecipeMasterElement processingRecipe,
    ElectricPower requestPower)
{
    _vanillaMachineInputInventory = vanillaMachineInputInventory;
    _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
    _processingRecipe = processingRecipe;
    _processingRecipeTicks = processingRecipe != null ? GameUpdater.SecondsToTicks(processingRecipe.Time) : 0;
    _blockDefaultPower = requestPower;
    RequestPower = requestPower;
    RemainingTicks = remainingTicks;
    CurrentState = currentState;
}
```

- [ ] **Step 3: Idle()でレシピ開始時にRequestPowerを更新する仕組みを追加**

`Idle()`メソッド内(line 125-139)、`_processingRecipe = recipe;`の後にRequestPower更新を追加:

```csharp
private void Idle()
{
    var isGetRecipe = _vanillaMachineInputInventory.TryGetRecipeElement(out var recipe);
    var isStartProcess = CurrentState == ProcessState.Idle && isGetRecipe &&
           _vanillaMachineInputInventory.IsAllowedToStartProcess() &&
           _vanillaMachineOutputInventory.IsAllowedToOutputItem(recipe);

    if (isStartProcess)
    {
        CurrentState = ProcessState.Processing;
        _processingRecipe = recipe;
        _processingRecipeTicks = GameUpdater.SecondsToTicks(_processingRecipe.Time);
        _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipe);
        RemainingTicks = _processingRecipeTicks;

        // レシピのエネルギーオーバーライドを適用
        // Apply recipe energy override
        RequestPower = _resolveOverridePower?.Invoke(_processingRecipe) ?? _blockDefaultPower;
    }
}
```

- [ ] **Step 4: レシピ完了時にブロックデフォルトに戻す**

`Processing()`メソッド内(line 142-159)、処理完了時にRequestPowerを戻す:

```csharp
private void Processing()
{
    var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, RequestPower);
    if (subTicks >= RemainingTicks)
    {
        RemainingTicks = 0;
        CurrentState = ProcessState.Idle;
        _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipe);

        // レシピ完了時にブロックデフォルトに戻す
        // Reset to block default when recipe completes
        RequestPower = _blockDefaultPower;
    }
    else
    {
        RemainingTicks -= subTicks;
    }

    _usedPower = true;
}
```

- [ ] **Step 5: オーバーライド解決用デリゲートフィールドと設定メソッドを追加**

```csharp
// フィールド追加（line 35付近）
private Func<MachineRecipeMasterElement, ElectricPower> _resolveOverridePower;

// 設定メソッド追加
public void SetResolveOverridePower(Func<MachineRecipeMasterElement, ElectricPower> resolver)
{
    _resolveOverridePower = resolver;
}
```

- [ ] **Step 6: コンパイル確認**

Run (worktree): `./tools/unity-test.sh moorestech_server "^$"`

- [ ] **Step 7: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs
git commit -m "feat: VanillaMachineProcessorComponentのRequestPowerを可変化"
```

---

## Chunk 3: テンプレート側のオーバーライド適用

### Task 5: VanillaMachineTemplate（ElectricMachine）のオーバーライド対応

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs:35,75`

- [ ] **Step 1: New()でprocessor作成後にオーバーライドリゾルバーを設定**

`New()`メソッド内、`processor`作成(line 35)の直後:

```csharp
var processor = new VanillaMachineProcessorComponent(input, output, null, new ElectricPower(machineParam.RequiredPower));

// レシピエネルギーオーバーライドのリゾルバーを設定
// Set recipe energy override resolver
var defaultPower = new ElectricPower(machineParam.RequiredPower);
processor.SetResolveOverridePower(recipe => RecipeEnergyOverrideResolver.ResolveElectricPower(recipe, defaultPower));
```

- [ ] **Step 2: Load()でprocessor作成後にオーバーライドリゾルバーを設定**

`Load()`メソッド内、`processor`作成(line 75)の直後に同様に追加:

```csharp
var processor = BlockTemplateUtil.MachineLoadState(componentStates, input, output, new ElectricPower(machineParam.RequiredPower), blockMasterElement);

// レシピエネルギーオーバーライドのリゾルバーを設定
// Set recipe energy override resolver
var defaultPower = new ElectricPower(machineParam.RequiredPower);
processor.SetResolveOverridePower(recipe => RecipeEnergyOverrideResolver.ResolveElectricPower(recipe, defaultPower));
```

- [ ] **Step 3: コンパイル確認**

Run (worktree): `./tools/unity-test.sh moorestech_server "^$"`

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs
git commit -m "feat: VanillaMachineTemplateにElectricオーバーライドリゾルバーを設定"
```

---

### Task 6: VanillaGearMachineTemplate・VanillaGearMachineComponent（GearMachine）のオーバーライド対応

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs:49,53,58`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs:19,21,30-37`

- [ ] **Step 1: VanillaMachineProcessorComponentにCurrentRecipeプロパティを追加（先に追加しないとStep 3でコンパイルエラーになる）**

`VanillaMachineProcessorComponent`に以下を追加:

```csharp
public MachineRecipeMasterElement CurrentRecipe => _processingRecipe;
```

- [ ] **Step 2: VanillaGearMachineTemplate.GetBlock()でprocessor作成後にオーバーライドリゾルバーを設定**

`GetBlock()`メソッド内(line 53付近)、`processor`作成の直後:

```csharp
var processor = componentStates == null ? new VanillaMachineProcessorComponent(input, output, null, requirePower) : BlockTemplateUtil.MachineLoadState(componentStates, input, output, requirePower, blockMasterElement);

// レシピエネルギーオーバーライドのリゾルバーを設定
// Set recipe energy override resolver
var defaultTorque = machineParam.RequireTorque;
var defaultRpm = machineParam.RequiredRpm;
processor.SetResolveOverridePower(recipe => RecipeEnergyOverrideResolver.ResolveGearPower(recipe, defaultTorque, defaultRpm));
```

- [ ] **Step 3: VanillaGearMachineComponentにprocessorからレシピを参照する仕組みを追加**

`VanillaGearMachineComponent`の`OnGearUpdate()`を変更して、アクティブなレシピのオーバーライドを考慮する:

```csharp
public class VanillaGearMachineComponent : IBlockComponent
{
    private readonly GearEnergyTransformer _gearEnergyTransformer;
    private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
    private readonly float _blockDefaultTorque;
    private readonly float _blockDefaultRpm;

    public VanillaGearMachineComponent(VanillaMachineProcessorComponent vanillaMachineProcessorComponent, GearEnergyTransformer gearEnergyTransformer, GearMachineBlockParam gearMachineBlockParam)
    {
        _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
        _gearEnergyTransformer = gearEnergyTransformer;
        _blockDefaultTorque = gearMachineBlockParam.RequireTorque;
        _blockDefaultRpm = gearMachineBlockParam.RequiredRpm;

        _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
    }

    private void OnGearUpdate(GearUpdateType gearUpdateType)
    {
        // 現在のレシピからオーバーライドされたRPM/Torqueを取得
        // Get overridden RPM/Torque from current recipe
        var torque = _blockDefaultTorque;
        var rpm = _blockDefaultRpm;
        RecipeEnergyOverrideResolver.ResolveGearParams(_vanillaMachineProcessorComponent.CurrentRecipe, ref torque, ref rpm);

        var requiredRpm = new RPM(rpm);
        var requireTorque = new Torque(torque);

        var currentElectricPower = _gearEnergyTransformer.CalcMachineSupplyPower(requiredRpm, requireTorque);
        _vanillaMachineProcessorComponent.SupplyPower(currentElectricPower);
    }

    // ... Destroy等は既存のまま
}
```

- [ ] **Step 4: コンパイル確認**

Run (worktree): `./tools/unity-test.sh moorestech_server "^$"`

- [ ] **Step 5: 既存のGearMachineIoTestが通ることを確認**

Run (worktree): `./tools/unity-test.sh moorestech_server "GearMachineIoTest"`

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs
git commit -m "feat: GearMachineのオーバーライド対応を追加"
```

---

### Task 7: BlockTemplateUtil.MachineLoadState()のロード時オーバーライド対応

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs:119-131`

- [ ] **Step 1: MachineLoadState()で処理中レシピがある場合にRequestPowerを更新**

`MachineLoadState()`の末尾(line 125-133)で、processorのRequestPowerをレシピに応じて更新する。これはTemplate側で`SetResolveOverridePower`を設定した後、processorが既に`Processing`状態の場合のみ実行される。

実際にはTemplate側で`SetResolveOverridePower`を設定した後に手動でRequestPowerを初期化する。Template側の実装で対応済み（Task 5, 6でprocessor作成後にリゾルバーを設定するが、ロード時のProcessing状態のrecipeのオーバーライドは`MachineLoadState`が`requestPower`引数でブロックデフォルト値を渡しているので問題ない。ロード後にTemplate側でリゾルバーを設定し、その後のUpdate()で`Processing()`が実行される際に`RequestPower`はリゾルバー経由で解決される）。

ただし、ロードコンストラクタでは`RequestPower`は`requestPower`（ブロックデフォルト）で初期化される。Processing状態でロードされた場合、次のIdle()呼び出しまでRequestPowerがブロックデフォルトのままになる問題がある。

→ ロードコンストラクタの末尾でオーバーライド適用ロジックを呼ぶか、Template側でprocessor作成後に手動で適用する。

**Template側で対応する方法（よりシンプル）:**

`VanillaMachineTemplate.Load()`と`VanillaGearMachineTemplate.GetBlock()`で、processor作成後かつリゾルバー設定後に、Processing中のレシピがあれば即座にRequestPowerを更新:

```csharp
// リゾルバー設定の直後に追加
if (processor.CurrentState == ProcessState.Processing && processor.CurrentRecipe != null)
{
    processor.ForceUpdateRequestPower();
}
```

`VanillaMachineProcessorComponent`に`ForceUpdateRequestPower()`を追加:

```csharp
/// <summary>
///     ロード時に処理中レシピのオーバーライドを強制適用する
///     Force apply override for in-progress recipe on load
/// </summary>
public void ForceUpdateRequestPower()
{
    RequestPower = _resolveOverridePower?.Invoke(_processingRecipe) ?? _blockDefaultPower;
}
```

- [ ] **Step 2: コンパイル確認**

Run (worktree): `./tools/unity-test.sh moorestech_server "^$"`

- [ ] **Step 3: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs
git commit -m "feat: ロード時のProcessing状態でのオーバーライド復元対応"
```

---

## Chunk 4: バリデーションとテスト

### Task 8: C#バリデーション追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs:20-97`

- [ ] **Step 1: バリデーションテストを先に書く**

**File:** `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs`

```csharp
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class RecipeEnergyOverrideTest
    {
        /// <summary>
        ///     テストデータの読み込みが成功する（バリデーション通過の確認）
        ///     Test data loads successfully (validates validation passes)
        /// </summary>
        [Test]
        public void ValidationPassesWithCorrectOverrideTest()
        {
            // テストModの読み込みが成功する = バリデーションを通過している
            // Test mod loading succeeds = validation passed
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // energyOverrideType付きのレシピが正常にロードされることを確認
            // Verify recipe with energyOverrideType loads correctly
            var recipes = MasterHolder.MachineRecipesMaster.MachineRecipes.Data;
            Assert.IsTrue(recipes.Length > 0);
        }
    }
}
```

- [ ] **Step 2: テスト実行（パスすることを確認）**

Run (worktree): `./tools/unity-test.sh moorestech_server "RecipeEnergyOverrideTest"`

- [ ] **Step 3: MachineRecipesMasterUtil.Validate()にオーバーライドタイプ整合性チェックを追加**

`RecipeValidation()`ローカル関数内の既存チェック（line 94付近）の後に追加:

```csharp
// エネルギーオーバーライドタイプとブロックタイプの整合性チェック
// Validate energy override type matches block energy type
if (blockId != null)
{
    var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid);
    var blockType = blockMaster.BlockType;
    var overrideType = recipe.EnergyOverrideType;

    // SourceGeneratorはenumを文字列定数として生成する
    // SourceGenerator generates enums as string constants
    if (overrideType == MachineRecipeMasterElement.EnergyOverrideTypeConst.Electric && blockType != BlockMasterElement.BlockTypeConst.ElectricMachine)
    {
        logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] GUID:{recipe.MachineRecipeGuid} has Electric energy override but block '{blockMaster.Name}' is {blockType}, not ElectricMachine\n";
    }
    if (overrideType == MachineRecipeMasterElement.EnergyOverrideTypeConst.Gear && blockType != BlockMasterElement.BlockTypeConst.GearMachine)
    {
        logs += $"[MachineRecipesMaster] Recipe[{recipeIndex}] GUID:{recipe.MachineRecipeGuid} has Gear energy override but block '{blockMaster.Name}' is {blockType}, not GearMachine\n";
    }
}
```

> **NOTE**: SourceGeneratorはenumを文字列定数として生成する。`recipe.EnergyOverrideType`は`string`型で、比較には`MachineRecipeMasterElement.EnergyOverrideTypeConst.*`を使う。`blockMaster.BlockType`も同様に`BlockMasterElement.BlockTypeConst.*`を使う。実際の生成コードを確認して調整すること。`using Mooresmaster.Model.BlocksModule;`のインポートも必要。

- [ ] **Step 4: コンパイル確認**

Run (worktree): `./tools/unity-test.sh moorestech_server "^$"`

- [ ] **Step 5: テスト再実行**

Run (worktree): `./tools/unity-test.sh moorestech_server "RecipeEnergyOverrideTest"`

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs
git commit -m "feat: エネルギーオーバーライドのバリデーションとテストを追加"
```

---

### Task 9: ElectricMachineのオーバーライド動作テスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs`

- [ ] **Step 1: テストを追加**

```csharp
/// <summary>
///     Electricオーバーライドが適用され、RequestPowerがレシピの値になることを確認
///     Verify Electric override is applied and RequestPower becomes recipe value
/// </summary>
[Test]
public void ElectricMachineEnergyOverrideTest()
{
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
        new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    var itemStackFactory = ServerContext.ItemStackFactory;

    // Electricオーバーライド付きのレシピを取得（index 0, requiredPower: 50）
    // Get recipe with Electric override (index 0, requiredPower: 50)
    var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];

    var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
    ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
    var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
    foreach (var inputItem in recipe.InputItems)
    {
        blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
    }

    var processor = block.GetComponent<VanillaMachineProcessorComponent>();
    var machineComponent = block.GetComponent<VanillaElectricMachineComponent>();

    // ブロックデフォルトのrequiredPower(100)であることを確認
    // Verify block default requiredPower (100)
    Assert.AreEqual(100f, processor.RequestPower.AsPrimitive(), 0.01f);

    // エネルギー供給してレシピ処理を開始させる
    // Supply energy to start recipe processing
    machineComponent.SupplyEnergy(new ElectricPower(10000));
    GameUpdater.UpdateOneTick();

    // レシピのオーバーライド値(50)になっていることを確認
    // Verify overridden to recipe value (50)
    Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
    Assert.AreEqual(50f, processor.RequestPower.AsPrimitive(), 0.01f);
}
```

- [ ] **Step 2: テスト実行**

Run (worktree): `./tools/unity-test.sh moorestech_server "ElectricMachineEnergyOverrideTest"`

- [ ] **Step 3: コミット**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs
git commit -m "test: ElectricMachineのエネルギーオーバーライド動作テストを追加"
```

---

### Task 10: GearMachineのオーバーライド動作テスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs`

- [ ] **Step 1: テストを追加**

```csharp
/// <summary>
///     Gearオーバーライドが適用され、RequestPowerがオーバーライド値のTorque×RPMになることを確認
///     Verify Gear override applies and RequestPower becomes overridden Torque × RPM
/// </summary>
[Test]
public void GearMachineEnergyOverrideTest()
{
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
        new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    var itemStackFactory = ServerContext.ItemStackFactory;

    // Gearオーバーライド付きのレシピを取得（index 3, requireTorque: 3, requiredRpm: 10）
    // Get recipe with Gear override (index 3, requireTorque: 3, requiredRpm: 10)
    var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[3];

    var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
    ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
    var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
    foreach (var inputItem in recipe.InputItems)
    {
        blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
    }

    var processor = block.GetComponent<VanillaMachineProcessorComponent>();
    var gearEnergyTransformer = block.GetComponent<GearEnergyTransformer>();

    // ブロックデフォルトのrequiredPower(Torque * RPM)を確認
    // Verify block default requiredPower (Torque * RPM)
    var gearMachineParam = MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid).BlockParam as GearMachineBlockParam;
    var defaultPower = gearMachineParam.RequireTorque * gearMachineParam.RequiredRpm;
    Assert.AreEqual(defaultPower, processor.RequestPower.AsPrimitive(), 0.01f);

    // 十分なギアパワーを供給してレシピ処理を開始
    // Supply sufficient gear power to start recipe processing
    var rpm = new RPM(gearMachineParam.RequiredRpm);
    var torque = new Torque(gearMachineParam.RequireTorque);
    gearEnergyTransformer.SupplyPower(rpm, torque, true);
    GameUpdater.RunFrames(1);
    processor.Update();

    // レシピのオーバーライド値(3 * 10 = 30)になっていることを確認
    // Verify overridden to recipe value (3 * 10 = 30)
    Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
    Assert.AreEqual(30f, processor.RequestPower.AsPrimitive(), 0.01f);
}
```

- [ ] **Step 2: テスト実行**

Run (worktree): `./tools/unity-test.sh moorestech_server "GearMachineEnergyOverrideTest"`

- [ ] **Step 3: コミット**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs
git commit -m "test: GearMachineのエネルギーオーバーライド動作テストを追加"
```

---

### Task 11: セーブ/ロード時のオーバーライド復元テスト

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs`

- [ ] **Step 1: テストを追加**

```csharp
/// <summary>
///     セーブ/ロード後にオーバーライドされたRequestPowerが正しく復元されることを確認
///     Verify overridden RequestPower is correctly restored after save/load
/// </summary>
[Test]
public void SaveLoadEnergyOverrideTest()
{
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(
        new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    var itemStackFactory = ServerContext.ItemStackFactory;

    // Electricオーバーライド付きのレシピを取得（index 0, requiredPower: 50）
    // Get recipe with Electric override (index 0, requiredPower: 50)
    var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];

    var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
    ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
    var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
    foreach (var inputItem in recipe.InputItems)
    {
        blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
    }

    var machineComponent = block.GetComponent<VanillaElectricMachineComponent>();

    // レシピ処理を開始
    // Start recipe processing
    machineComponent.SupplyEnergy(new ElectricPower(10000));
    GameUpdater.UpdateOneTick();

    var processor = block.GetComponent<VanillaMachineProcessorComponent>();
    Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
    Assert.AreEqual(50f, processor.RequestPower.AsPrimitive(), 0.01f);

    // セーブ＆ロード実行
    // Execute save & load
    var saveServiceProvider = ServerContext.WorldBlockDatastore;
    // ここでは既存のセーブ/ロードテストパターンに従う
    // Follow existing save/load test patterns here
    // （MachineSaveLoadTest等のパターンを参照して実装）

    // ロード後のProcessorのRequestPowerがオーバーライド値(50)であることを確認
    // Verify loaded processor's RequestPower is overridden value (50), not block default (100)
    // Assert.AreEqual(50f, loadedProcessor.RequestPower.AsPrimitive(), 0.01f);
}
```

> **NOTE**: セーブ/ロードテストの具体的な実装は`MachineSaveLoadTest.cs`のパターンに従って調整すること。上記はテンプレートであり、実際のセーブ/ロードAPI呼び出しは既存テストを参照して実装する。

- [ ] **Step 2: テスト実行**

Run (worktree): `./tools/unity-test.sh moorestech_server "SaveLoadEnergyOverrideTest"`

- [ ] **Step 3: コミット**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/RecipeEnergyOverrideTest.cs
git commit -m "test: セーブ/ロード時のエネルギーオーバーライド復元テストを追加"
```

---

### Task 12: 既存テスト全体のリグレッション確認

**Files:** なし（テスト実行のみ）

- [ ] **Step 1: MachineIOTest実行**

Run (worktree): `./tools/unity-test.sh moorestech_server "MachineIOTest"`

既存のElectricMachineテストが壊れていないこと。

- [ ] **Step 2: GearMachineIoTest実行**

Run (worktree): `./tools/unity-test.sh moorestech_server "GearMachineIoTest"`

既存のGearMachineテストが壊れていないこと。

- [ ] **Step 3: MachineSaveLoadTest・GearMachineSaveLoadTest実行**

Run (worktree): `./tools/unity-test.sh moorestech_server "MachineSaveLoad|GearMachineSaveLoad"`

セーブ/ロードテストが壊れていないこと。

- [ ] **Step 4: 全テスト実行**

Run (worktree): `./tools/unity-test.sh moorestech_server ".*"`

全テストがパスすることを確認。

---

## 実装上の注意事項

### SourceGenerator生成コードの型名について
SourceGeneratorはenumをC# enumではなく**文字列定数**として生成する。スキーマに`energyOverrideType`(enum)と`energyOverride`(switch)を追加すると、以下が自動生成される:

- `MachineRecipeMasterElement.EnergyOverrideType` — `string`型プロパティ
- `MachineRecipeMasterElement.EnergyOverrideTypeConst` — ネストされた定数クラス（`.None`, `.Electric`, `.Gear`）
- `MachineRecipeMasterElement.EnergyOverride` — switchケースのポリモーフィック基底型
- `ElectricEnergyOverride` class — switchの`Electric`ケース
- `GearEnergyOverride` class — switchの`Gear`ケース

**比較は常に定数クラス経由で行う**: `recipe.EnergyOverrideType == MachineRecipeMasterElement.EnergyOverrideTypeConst.Electric`

実際の生成コード（`MachineRecipesModule`名前空間）をSourceGenerator実行後に確認し、計画中の型名と異なる場合は適宜調整すること。特にoptionalプロパティの`float?`型の扱いを確認。

### @edit-schema スキルの利用
Task 1のYAMLスキーマ編集時は`edit-schema`スキルを参照すること（`.claude/rules/edit-schema.md`の指示に従う）。

### ワークツリー環境
すべてのテスト・コンパイルは`unity-test.sh`を使用すること（uloop禁止）。`pwd`でworktreeパスを確認。
