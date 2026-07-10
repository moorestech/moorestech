# 機械レシピ明示選択 実装計画

> **エージェント実装者向け:** 必須サブスキルとして `superpowers:subagent-driven-development`（推奨）または `superpowers:executing-plans` を使用し、この計画をタスク単位で実装する。進捗管理にはチェックボックス（`- [ ]`）を使用する。

**目標:** 投入物による機械レシピの自動判定を廃止し、設置済み機械ごとに1つのレシピを明示選択する方式へ変更する。加工キャンセルと返却のトランザクション、永続化、同期、UI移行を妨げない最小UIまで実装する。

**アーキテクチャ:** `Game.Block` が機械ごとに1つの共有 `MachineRecipeSelectionState` を所有する。Processorはその状態を参照するが、2つ目のレシピGUIDは保持せず、加工中はジョブスナップショットだけを保持する。機械レシピ選択コンポーネントが選択の検証・保存・キャンセル調停・BlockState通知を担当し、Request-Responseプロトコルは `PacketResponseContext` から操作プレイヤーを特定する。

**技術要素:** Unity 6、C#/.NET、NUnit、UniRx、通信用MessagePack、保存用Newtonsoft JSON、VContainer、UniTask、uloop。

## 全体制約

- 機械が実行時に持つレシピGUIDは、選択中レシピGUIDの1つだけとする。
- 未選択機械は `Guid.Empty` を持ち、加工を開始しない。
- 加工状態オブジェクトは `MachineRecipeGuid` や `MachineRecipeMasterElement` を保持しない。
- キャンセル可否は、消費済みアイテムを機械入力、次に操作プレイヤーのメインインベントリへ全量返却できるかだけで判定する。
- 消費済み液体はキャンセル成功時に消失し、レシピ変更を妨げない。
- レシピ・アイテム・液体の識別子はMachineRecipeGuid・ItemGuid・FluidGuidのJSON文字列で保存し、ItemId・FluidId・BlockId・容量・最大スタック数・スロット数は保存しない。
- スキーマ項目を追加せず、自動生成された `Mooresmaster.Model.*` を編集しない。
- `partial`、新規通知でのC# `Action`/`event`、`try-catch`、デフォルト引数を使用しない。
- 新規・変更するコードファイルはすべて200行以下とし、1ディレクトリがコード10ファイルを超える前にサブディレクトリへ分割する。
- 複雑なメソッドはメソッド内の `#region Internal` にローカル関数をまとめ、クラス直下では使用しない。
- 主要処理には約3〜10行ごとに簡潔な日本語・英語の2行コメントを付ける。
- `.meta` ファイルを手動作成せず、Unityに生成させる。
- PrefabのYAMLを直接編集せず、`uloop execute-dynamic-code` またはUnity Editorのシリアライズ経由で変更する。
- `.cs` を変更した各タスクの完了時に `uloop compile --project-path ./moorestech_client` を実行する。
- テストは `--filter-type regex` で限定実行し、ドメインリロードエラー時は45秒待って再実行する。
- ユーザーの無関係な `.moorestech-external-revisions.json` 変更には触れない。

---

### タスク1: 自動判定用識別を一意GUID参照へ置換する

**対象ファイル:**

- 変更: `moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs`
- 変更: `moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json`
- 変更: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestMachineRecipeId.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/MachineRecipeConfigTest.cs`

**インターフェース:**

- 提供: `MachineRecipesMaster.GetRecipeElement(Guid machineRecipeGuid) : MachineRecipeMasterElement`
- 提供: テストマスタ全体で一意な `MachineRecipeGuid`。
- 削除: `MachineRecipesMaster.TryGetRecipeElement(BlockId, List<ItemId>, List<FluidId>, out MachineRecipeMasterElement)` と投入物キー生成処理。

- [ ] **手順1: 自動逆引きテストをGUID不変条件テストへ置換する**

`MachineRecipeConfigTest` を200行以下に保ち、投入物からレシピを引くテストを次の主要検証へ置換する。

```csharp
[Test]
public void MachineRecipeGuidsAreUniqueTest()
{
    new MoorestechServerDIContainerGenerator().Create(
        new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    var recipes = MasterHolder.MachineRecipesMaster.MachineRecipes.Data;
    var uniqueGuids = recipes.Select(recipe => recipe.MachineRecipeGuid).Distinct().Count();
    Assert.AreEqual(recipes.Length, uniqueGuids);
}

[Test]
public void RecipesWithSameInputsAreResolvedIndependentlyByGuidTest()
{
    new MoorestechServerDIContainerGenerator().Create(
        new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

    var first = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.ElectricMachineRecipe);
    var second = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.ElectricMachineAlternateRecipe);

    Assert.AreNotEqual(first.MachineRecipeGuid, second.MachineRecipeGuid);
    CollectionAssert.AreEqual(first.InputItems.Select(item => item.ItemGuid), second.InputItems.Select(item => item.ItemGuid));
}
```

- [ ] **手順2: 対象テストを実行し、共有GUIDデータによる失敗を確認する**

実行:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeConfigTest"
```

期待結果: 現在の先頭7レシピが `bd3d4d7d-9c3b-4ae1-875b-950327eedd9d` を共有し、代替レシピの定数・データも未作成なのでFAILする。

- [ ] **手順3: 安定した一意テストGUIDを割り当て、明示定数を追加する**

現在のJSON順に、共有されていた各エントリへ次のGUIDを正確に割り当てる。

```text
MachineId             10000000-0000-0000-0000-000000000001
BlockId               10000000-0000-0000-0000-000000000002
BeltConveyorId        10000000-0000-0000-0000-000000000003
GearMachine           10000000-0000-0000-0000-00000000000f
MachineRecipeTest1    10000000-0000-0000-0000-000000000019
MachineRecipeTest2    10000000-0000-0000-0000-00000000001a
MachineRecipeTest3    10000000-0000-0000-0000-00000000001b
```

同一入力の選択テストとロック済み選択テスト用に、次の2レシピを正確に追加する。

```json
{
  "time": 1.5,
  "blockGuid": "00000000-0000-0000-0000-000000000001",
  "initialUnlocked": true,
  "inputItems": [
    { "count": 3, "itemGuid": "00000000-0000-0000-1234-000000000001" },
    { "count": 1, "itemGuid": "00000000-0000-0000-1234-000000000002" }
  ],
  "inputFluids": [],
  "outputItems": [
    { "percent": 1, "count": 1, "itemGuid": "00000000-0000-0000-1234-000000000005" }
  ],
  "outputFluids": [],
  "machineRecipeGuid": "10000000-0000-0000-0000-000000000101"
},
{
  "time": 1,
  "blockGuid": "00000000-0000-0000-0000-000000000001",
  "initialUnlocked": false,
  "inputItems": [
    { "count": 1, "itemGuid": "00000000-0000-0000-1234-000000000001" }
  ],
  "inputFluids": [],
  "outputItems": [
    { "percent": 1, "count": 1, "itemGuid": "00000000-0000-0000-1234-000000000002" }
  ],
  "outputFluids": [],
  "machineRecipeGuid": "10000000-0000-0000-0000-000000000102"
}
```

次の定数を追加する。

```csharp
public static readonly Guid ElectricMachineRecipe = new("10000000-0000-0000-0000-000000000001");
public static readonly Guid GearMachineRecipe = new("10000000-0000-0000-0000-00000000000f");
public static readonly Guid MachineRecipeTest1Basic = new("10000000-0000-0000-0000-000000000019");
public static readonly Guid ElectricMachineAlternateRecipe = new("10000000-0000-0000-0000-000000000101");
public static readonly Guid LockedElectricMachineRecipe = new("10000000-0000-0000-0000-000000000102");
```

- [ ] **手順4: 投入物キー辞書をGUID辞書へ置換する**

マスタを次の形で実装する。

```csharp
private Dictionary<Guid, MachineRecipeMasterElement> _machineRecipesByGuid;

public void Initialize()
{
    MachineRecipesMasterUtil.Initialize(MachineRecipes, out _machineRecipesByGuid);
}

public MachineRecipeMasterElement GetRecipeElement(Guid machineRecipeGuid)
{
    return _machineRecipesByGuid.GetValueOrDefault(machineRecipeGuid);
}
```

`MachineRecipesMasterUtil.Initialize` は `Dictionary<Guid, MachineRecipeMasterElement>` を構築するよう変更する。`Validate` では `HashSet<Guid>` を使い、GUID重複ごとに検証エラーを追加する。`GetRecipeElementKey`、投入物のソート、同一入力レシピ拒否を削除し、同じ入力を持つ複数レシピを許可する。

- [ ] **手順5: コンパイルしてマスタテストを実行する**

実行:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeConfigTest|MasterValidation"
```

期待結果: コンパイル成功。対象テストがすべてPASSする。

- [ ] **手順6: 識別子移行をコミットする**

```bash
git add moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs \
  moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs \
  moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json \
  moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestMachineRecipeId.cs \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/MachineRecipeConfigTest.cs
git commit -m "refactor: 機械レシピを一意GUID参照へ移行"
```

---

### タスク2: 単一レシピ機械ランタイムとトランザクション型キャンセルを実装する

**対象ファイル:**

- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSelectionState.cs`
- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSelectionResult.cs`
- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeInputValidator.cs`
- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/VanillaMachineRecipeSelectionComponent.cs`
- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/Job/MachineProcessingJob.cs`
- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/Job/MachineRecipeRefundTransaction.cs`
- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/ProcessState.cs`
- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorSaveJsonObject.cs`
- 作成: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/MachineRecipe/MachineRecipeSelectionStateDetail.cs`
- 削除: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs`
- 削除: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/MachineBlockStateDetail.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/MachineProcessContext.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/IdleMachineProcessState.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/ProcessingMachineProcessState.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineSaveComponent.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs`
- テスト作成: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineRecipeSelection/MachineRecipeSelectionTestHelper.cs`
- テスト作成: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineRecipeSelection/MachineRecipeSelectionProcessingTest.cs`
- テスト作成: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineRecipeSelection/MachineRecipeCancellationRefundTest.cs`

**インターフェース:**

- 提供: `MachineRecipeSelectionResult VanillaMachineRecipeSelectionComponent.TrySetRecipe(Guid recipeGuid, IOpenableInventory playerMainInventory)`
- 提供: `Guid VanillaMachineRecipeSelectionComponent.GetSelectedRecipeGuid()`
- 提供: `bool VanillaMachineProcessorComponent.TryCancelProcessing(IOpenableInventory playerMainInventory)`
- 提供: `List<IItemStack> VanillaMachineInputInventory.ConsumeInputs(MachineRecipeMasterElement recipe)`
- 利用: タスク1の `MachineRecipesMaster.GetRecipeElement(Guid)`。

- [ ] **手順1: 明示選択と返却の失敗テストを書く**

2つのテストファイルで次のケースを正確に網羅する。

```csharp
[Test]
public void UnselectedMachineDoesNotStartWithValidInputsTest()
{
    var (block, inventory, processor) = MachineRecipeSelectionTestHelper.PlaceElectricMachine();
    MachineRecipeSelectionTestHelper.InsertElectricRecipeInputs(inventory);

    GameUpdater.UpdateOneTick();

    Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
}

[Test]
public void SelectedMachineStartsOnlySelectedRecipeTest()
{
    var (block, inventory, processor) = MachineRecipeSelectionTestHelper.PlaceElectricMachine();
    var result = MachineRecipeSelectionTestHelper.Select(block, ForUnitTestMachineRecipeId.ElectricMachineRecipe, 0);
    MachineRecipeSelectionTestHelper.InsertElectricRecipeInputs(inventory);

    GameUpdater.UpdateOneTick();

    Assert.AreEqual(MachineRecipeSelectionResult.Success, result);
    Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
}

[Test]
public void RefundCapacityFailureKeepsRecipeProgressAndInventoriesTest()
{
    var setup = MachineRecipeSelectionTestHelper.CreateProcessingMachine(0);
    MachineRecipeSelectionTestHelper.FillMachineInput(setup.Inventory);
    MachineRecipeSelectionTestHelper.FillPlayerMainInventory(0);
    var remainingBefore = setup.Processor.GetRemainingTicks();
    var machineBefore = setup.Inventory.CreateCopiedItems();

    var result = setup.Selection.TrySetRecipe(Guid.Empty, setup.PlayerMainInventory);

    Assert.AreEqual(MachineRecipeSelectionResult.RefundCapacityInsufficient, result);
    Assert.AreEqual(ProcessState.Processing, setup.Processor.CurrentState);
    Assert.AreEqual(remainingBefore, setup.Processor.GetRemainingTicks());
    CollectionAssert.AreEqual(machineBefore, setup.Inventory.InventoryItems);
}
```

さらに、機械だけへの返却、機械からプレイヤーへの段階返却、同一GUIDの冪等性、ロック済みレシピ拒否、別機械レシピ拒否、解除成功時に旧出力が生成されないこと、`IsRemain` の除外、消費済み液体の消失をテストする。

- [ ] **手順2: 対象テストを実行し、実装前に失敗することを確認する**

実行:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProcessingTest|MachineRecipeCancellationRefundTest"
```

期待結果: 選択コンポーネント、結果enum、トランザクション型キャンセルが未実装なのでFAILする。

- [ ] **手順3: 単一GUID選択状態と公開選択コンポーネントを実装する**

新しい単純getter/setterプロパティは作らず、メソッドでアクセスする。

```csharp
internal sealed class MachineRecipeSelectionState
{
    private Guid _recipeGuid;

    public MachineRecipeSelectionState(Guid recipeGuid)
    {
        _recipeGuid = recipeGuid;
    }

    public Guid GetRecipeGuid() => _recipeGuid;
    public void SetRecipeGuid(Guid recipeGuid) => _recipeGuid = recipeGuid;
}

public enum MachineRecipeSelectionResult
{
    Success,
    RecipeNotFound,
    RecipeForDifferentBlock,
    RecipeLocked,
    RefundCapacityInsufficient,
}
```

`VanillaMachineRecipeSelectionComponent` は、同一GUID（成功・無操作）、`Guid.Empty` による解除、レシピ存在、BlockGuid一致、アンロック状態、キャンセル・返却の順で検証し、その後だけ状態変更と `_onChangeBlockState.OnNext(Unit.Default)` を実行する。通知はprivateな `Subject<Unit>` とし、`IObservable<Unit>` で公開する。

- [ ] **手順4: 入力検証、正確な消費スナップショット、返却事前確認を実装する**

`RecipeConfirmation` の処理を機械ドメインの `MachineRecipeInputValidator` へ移す。旧自動逆引きユーティリティを削除し、`VanillaMachineInputInventory` から `IGameUnlockStateData` 依存を除去する。

入力消費処理は、実際に消費したスタックを正確に返すよう変更する。

```csharp
public List<IItemStack> ConsumeInputs(MachineRecipeMasterElement recipe)
{
    var consumedItems = new List<IItemStack>();

    // 実際に減らすスタック断片を返却用スナップショットへ残す
    // Preserve the exact removed stack fragments for cancellation refunds
    foreach (var input in recipe.InputItems)
    {
        if (input.IsRemain.HasValue && input.IsRemain.Value) continue;
        var itemId = MasterHolder.ItemMaster.GetItemId(input.ItemGuid);
        for (var slot = 0; slot < InputSlot.Count; slot++)
        {
            var source = InputSlot[slot];
            if (source.Id != itemId || source.Count < input.Count) continue;

            consumedItems.Add(source.SubItem(source.Count - input.Count));
            _itemDataStoreService.SetItem(slot, source.SubItem(input.Count));
            break;
        }
    }

    // 液体は消費するが返却スナップショットには含めない
    // Consume fluids without adding them to the refund snapshot
    foreach (var inputFluid in recipe.InputFluids)
    {
        var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
        for (var tank = 0; tank < _fluidContainers.Length; tank++)
        {
            if (_fluidContainers[tank].FluidId != fluidId || _fluidContainers[tank].Amount < inputFluid.Amount) continue;
            _fluidContainers[tank].Amount -= inputFluid.Amount;
            if (_fluidContainers[tank].Amount == 0) _fluidContainers[tank].FluidId = FluidMaster.EmptyFluidId;
            break;
        }
    }
    return consumedItems;
}
```

アイテムメタデータを維持するため、減算前の元スタックから `source.SubItem(source.Count - input.Count)` で消費断片を作る。

返却は事前確認後に確定する方式で実装する。

```csharp
public static bool TryExecute(
    VanillaMachineInputInventory machineInput,
    IOpenableInventory playerMainInventory,
    List<IItemStack> consumedItems)
{
    var playerRemainders = machineInput.SimulateInsert(consumedItems);
    if (!playerMainInventory.InsertionCheck(playerRemainders)) return false;

    // 事前計算成功後のみ実インベントリへ順番どおり反映する
    // Mutate real inventories only after the complete preflight succeeds
    var actualRemainders = machineInput.InsertItem(consumedItems);
    var unexpectedRemainders = playerMainInventory.InsertItem(actualRemainders);
    Debug.Assert(unexpectedRemainders.Count == 0);
    return true;
}
```

`SimulateInsert` は `AllowMultipleStacksPerItemOnInsert = false` の一時 `OpenableInventoryItemDataStoreService` を作成し、現在の機械入力スロットを `SetItemWithoutEvent` でコピーして、`InsertItem` の余りを返す。

- [ ] **手順5: 加工レシピスナップショットをGUIDなしジョブへ置換する**

`MachineProcessingJob` は総tick、残りtick、生成予定アイテム、生成予定液体、消費済みアイテムを保持する。`ConsumeTicks`、`IsComplete`、`GetPendingItemOutputs`、`GetPendingFluidOutputs`、`GetConsumedItems` などの振る舞いを公開し、レシピやマスタのフィールドは持たない。

このタスク内でProcessor保存DTOを `VanillaMachineProcessorComponent.cs` から分離し、ランタイムのリファクタ後もコンパイルできる状態にする。ジョブは `totalSeconds`、`remainingSeconds`、`pendingOutputs`、`pendingFluidOutputs`、`consumedItems` としてシリアライズし、`BlockTemplateUtil.MachineLoadState` で同じジョブへ復元する。タスク3で選択コンポーネントの永続化を加え、JSON契約全体を検証する。

Idle処理を次のフローへ変更する。

```csharp
var selectedGuid = _context.RecipeSelection.GetRecipeGuid();
if (selectedGuid == Guid.Empty) return ProcessState.Idle;

var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(selectedGuid);
if (!MachineRecipeInputValidator.CanStart(recipe, _context.InputInventory)) return ProcessState.Idle;

var effect = _context.EffectComponent.AggregateCurrent();
var itemOutputs = MachineOutputFactoryUtil.CreateRealizedOutputs(recipe, effect);
var fluidOutputs = MachineOutputFactoryUtil.CreateFluidOutputs(recipe);
if (!_context.OutputInventory.CanStoreOutputs(itemOutputs, fluidOutputs)) return ProcessState.Idle;

var totalTicks = CalculateTotalTicks(recipe, effect);
var consumedItems = _context.InputInventory.ConsumeInputs(recipe);
_processingState.SetJob(new MachineProcessingJob(totalTicks, itemOutputs, fluidOutputs, consumedItems));
return ProcessState.Processing;
```

通常の `OnExit` はジョブ出力を格納してジョブをクリアする。キャンセルでは `MachineRecipeRefundTransaction.TryExecute` を呼び、成功時は出力せずジョブをクリアする。`VanillaMachineProcessorComponent.TryCancelProcessing` は返却成功後だけProcessingからIdleへ変更し、UniRx Subjectへ通知する。

Processorを200行以下にするため、`ProcessState` と `ProcessStateExtension` を `VanillaMachineProcessorComponent.cs` から分離する。`MachineBlockStateDetail` は削除し、Processorの状態出力には `CommonMachineBlockStateDetail` だけを残し、選択状態は選択コンポーネントから提供する。

- [ ] **手順6: 電力・歯車機械テンプレートへ単一共有状態を組み込む**

タスク2のNew・Load経路では、まだ永続化されていない選択状態を `Guid.Empty` で初期化し、次の順でオブジェクトを作成する。

```csharp
var recipeSelectionState = new MachineRecipeSelectionState(Guid.Empty);
var processor = new VanillaMachineProcessorComponent(
    input, output, effectComponent, recipeSelectionState, requestPower, idlePowerRate, restoredJob);
var recipeSelection = new VanillaMachineRecipeSelectionComponent(
    blockMasterElement.BlockGuid,
    recipeSelectionState,
    processor,
    ServerContext.GetService<IGameUnlockStateData>());
```

両テンプレートのコンポーネント一覧へ `recipeSelection` を追加する。`GetMachineIOInventory` からアンロック状態を除去し、デフォルト引数を使わず全呼び出し側を更新する。タスク3ではLoad経路の `Guid.Empty` を保存済み選択状態へ置換し、その後にProcessorを構築する。

- [ ] **手順7: コンパイルして対象ランタイムテストを実行する**

実行:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProcessingTest|MachineRecipeCancellationRefundTest"
uloop get-logs --project-path ./moorestech_client --log-type Error
```

期待結果: コンパイル成功。対象テストがPASSし、新規Errorログがない。

- [ ] **手順8: ランタイムエンジンをコミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block \
  moorestech_server/Assets/Scripts/Game.Block.Interface/State \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineRecipeSelection
git commit -m "feat: 機械レシピの明示選択と安全な加工中変更を実装"
```

---

### タスク3: 選択状態とGUIDを持たない加工ジョブを永続化する

**対象ファイル:**

- 作成: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSelectionSaveJsonObject.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorSaveJsonObject.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/VanillaMachineRecipeSelectionComponent.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineSaveComponent.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs`
- 変更: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/Util/VanillaMachineProcessorTestUtil.cs`
- テスト作成: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/MachineRecipeSelection/MachineRecipeSelectionSaveLoadTest.cs`
- テスト変更: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/MachineSaveLoadTest.cs`
- テスト変更: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/GearMachineSaveLoadTest.cs`
- テスト変更: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/FluidMachineSaveLoadTest.cs`

**インターフェース:**

- 提供: 選択状態JSON `{ "machineRecipeGuid": "..." }`。
- 提供: `state`、`totalSeconds`、`remainingSeconds`、`pendingOutputs`、`pendingFluidOutputs`、`consumedItems` を持ち、レシピGUIDを持たないProcessor JSON。
- 利用: タスク2のジョブと選択状態。

- [ ] **手順1: 保存・ロードの失敗テストを書く**

次と同等の検証を追加する。

```csharp
[Test]
public void ProcessingSaveContainsSelectionAndRefundSnapshotWithoutProcessingRecipeGuidTest()
{
    var setup = MachineRecipeSelectionTestHelper.CreateProcessingMachine(0);
    var saveState = setup.Block.GetSaveState();

    var selectionJson = JObject.Parse(saveState[typeof(VanillaMachineRecipeSelectionComponent).FullName]);
    var machineJson = JObject.Parse(saveState[typeof(VanillaMachineSaveComponent).FullName]);
    var processorJson = (JObject)machineJson["processor"];

    Assert.AreEqual(ForUnitTestMachineRecipeId.ElectricMachineRecipe.ToString(), selectionJson["machineRecipeGuid"]?.Value<string>());
    Assert.IsNull(processorJson["recipeGuid"]);
    Assert.IsNotNull(processorJson["consumedItems"]);
    Assert.IsNotNull(processorJson["pendingFluidOutputs"]);
}
```

加工中機械をロードしてレシピを解除し、復元済みの消費アイテムが返却されることを確認する往復テストを追加する。電力・歯車・液体機械それぞれの選択状態往復テストも追加する。

- [ ] **手順2: 保存テストを実行し、不足DTO項目による失敗を確認する**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionSaveLoadTest|MachineSaveLoadTest|GearMachineSaveLoadTest|FluidMachineSaveLoadTest"
```

期待結果: 選択コンポーネントに保存キー・状態がまだなく、ジョブJSONのGUID項目不足も検出されるためFAILする。

- [ ] **手順3: GUIDベースのJSON DTOを実装する**

次のデータ契約で実装する。

```csharp
public class MachineRecipeSelectionSaveJsonObject
{
    [JsonProperty("machineRecipeGuid")] public string MachineRecipeGuidStr;
    [JsonIgnore] public Guid MachineRecipeGuid => Guid.Parse(MachineRecipeGuidStr);
}

public class FluidStackSaveJsonObject
{
    [JsonProperty("fluidGuid")] public string FluidGuidStr;
    [JsonProperty("amount")] public double Amount;

    public FluidStack ToFluidStack()
    {
        return new FluidStack(Amount, MasterHolder.FluidMaster.GetFluidId(Guid.Parse(FluidGuidStr)));
    }
}
```

`VanillaMachineProcessorSaveJsonObject` を確認・完成させる。tickではなく秒数を保存し、生成予定・返却アイテムは `ItemStackSaveJsonObject`、液体スタックは `FluidStackSaveJsonObject` でシリアライズする。ItemId・FluidId・レシピGUIDは保存しない。

- [ ] **手順4: Processor構築前に選択状態を復元する**

選択コンポーネントへ、自身のコンポーネントキーを読み `MachineRecipeSelectionState` を返すstaticロードメソッドを追加する。両テンプレートを、最初に選択状態、次にジョブを復元し、コンポーネントとProcessorが同一状態オブジェクトを共有する順序へ更新する。

セーブがProcessingを示していても完全なジョブスナップショットがない場合はIdleとしてロードする。決定論的な不正ジョブのフォールバック以外に、互換移行処理は作らない。

- [ ] **手順5: コンパイルして保存・ロードテストを実行する**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionSaveLoadTest|MachineSaveLoadTest|GearMachineSaveLoadTest|FluidMachineSaveLoadTest"
```

期待結果: 対象テストがすべてPASSする。

- [ ] **手順6: 永続化をコミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine \
  moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad \
  moorestech_server/Assets/Scripts/Tests/Util/VanillaMachineProcessorTestUtil.cs
git commit -m "feat: 機械レシピ選択と加工ジョブを保存"
```

---

### タスク4: サーバー選択プロトコルとBlockState同期を追加する

**対象ファイル:**

- 作成: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe/MachineRecipeSelectionProtocol.cs`
- 作成: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe/MachineRecipeSelectionProtocolMessages.cs`
- 変更: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- テスト作成: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/MachineRecipe/MachineRecipeSelectionProtocolTest.cs`

**インターフェース:**

- 提供: プロトコルタグ `va:machineRecipeSelection`。
- 提供: `CreateGetRequest(Vector3Int)` と `CreateSetRequest(Vector3Int, Guid)`。
- 提供: 応答 `{ Success, AppliedRecipeGuidStr, FailureReason }`。
- 利用: `PacketResponseContext.PlayerId`、`IPlayerInventoryDataStore`、タスク2の選択コンポーネント。

- [ ] **手順1: Get・Set・失敗理由のプロトコル失敗テストを書く**

未選択Get、Set成功、解除成功、不正GUID、ブロック不存在、非機械、プレイヤー未紐付け、別機械レシピ、ロック済みレシピ、返却容量不足、応答シーケンス処理をテストする。プレイヤーはコンテキストだけで紐付ける。

```csharp
var context = new PacketResponseContext();
context.BindPlayerId(0);
var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), context);
var response = MessagePackSerializer.Deserialize<MachineRecipeSelectionResponse>(responseBytes[0]);
```

- [ ] **手順2: プロトコルテストを実行し、タグ未登録による失敗を確認する**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProtocolTest"
```

期待結果: プロトコル・タグが未登録なのでFAILする。

- [ ] **手順3: 200行以下を守るためメッセージを別ファイルへ実装する**

次のenumとファクトリを使用する。

```csharp
public enum MachineRecipeSelectionOperation { Get, Set }

public enum MachineRecipeSelectionFailureReason
{
    None,
    InvalidRequest,
    PlayerNotBound,
    BlockNotFound,
    NotMachine,
    RecipeNotFound,
    RecipeForDifferentBlock,
    RecipeLocked,
    RefundCapacityInsufficient,
}

public static MachineRecipeSelectionRequest CreateGetRequest(Vector3Int position)
public static MachineRecipeSelectionRequest CreateSetRequest(Vector3Int position, Guid recipeGuid)
```

RequestのMessagePackキーは2からPosition、Operation、RecipeGuidStrの順とする。Responseも2からSuccess、AppliedRecipeGuidStr、FailureReasonの順とする。外部GUID文字列は `Guid.TryParse` で解析し、要求検証に例外を使わない。

- [ ] **手順4: プロトコルの調停処理と登録を実装する**

プロトコルは次を実行する。

1. Position・操作種別・GUIDをデシリアライズして検証する。
2. ブロックと `VanillaMachineRecipeSelectionComponent` を解決する。
3. Getでは現在の選択を返す。
4. Setでは `context.PlayerId` を必須とする。
5. `IPlayerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory` を解決する。
6. `TrySetRecipe` を呼び、ドメイン結果を通信層の失敗enumへ対応付ける。
7. 拒否されたSetを含め、常にコンポーネントの現在GUIDを返す。

正確なプロトコルタグで `PacketResponseCreator` へ登録する。

- [ ] **手順5: コンパイルしてプロトコル・BlockStateテストを実行する**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProtocolTest|ChangeBlockEventPacketTest|InvokeBlockStateEventProtocolTest"
```

期待結果: 対象テストがすべてPASSし、Set成功時に既存BlockState経路から選択StateDetailが配信される。

- [ ] **手順6: プロトコル実装をコミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/MachineRecipe
git commit -m "feat: 機械レシピ選択プロトコルを追加"
```

---

### タスク5: 最終レイアウトを固定しない薄い移行UIを追加する

**対象ファイル:**

- 作成: `moorestech_client/Assets/Scripts/Client.Network/API/MachineRecipe/MachineRecipeSelectionApi.cs`
- 作成: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/RecipeSelection/MachineRecipeSelectionCandidateProvider.cs`
- 作成: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/RecipeSelection/MachineRecipeSelectionView.cs`
- 変更: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs`
- 変更: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/MachineBlockInventoryView.cs`
- Editor変更: `moorestech_client/Assets/AddressableResources/UI/Block/MachineBlockInventory.prefab`
- Editor変更: `moorestech_client/Assets/AddressableResources/UI/Block/GearMachineBlockInventory.prefab`
- テスト作成: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MachineRecipeSelection/MachineRecipeSelectionUITest.cs`

**インターフェース:**

- 提供: `MachineRecipeSelectionApi.SendAsync(MachineRecipeSelectionRequest, CancellationToken)`.
- 提供: `MachineRecipeSelectionCandidateProvider.GetCandidates(Guid blockGuid, IGameUnlockStateData) : List<MachineRecipeMasterElement>`。
- 提供: 先頭候補を未設定、以降を対象ブロックのアンロック済みレシピとする、クリック循環型の一時選択UI。

- [ ] **手順1: PlayMode UIの失敗テストを書く**

テストでは電力機械インベントリを開き、選択表示が `未設定` から始まることを確認する。既存レシピ表示へ付けたボタンをクリックして応答を待ち、サーバーの選択と表示文字列が両方変わったことを確認する。全候補をクリックして一周し、`未設定` を含めて循環することも確認する。拒否時にサーバーが以前の適用GUIDを維持するケースも追加する。

実行:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionUITest"
```

期待結果: クライアントAPI・View・Prefabボタンが未作成なのでFAILする。

- [ ] **手順2: `VanillaApiWithResponse` を肥大化させず専用APIを追加する**

次のように実装する。

```csharp
public sealed class MachineRecipeSelectionApi
{
    private readonly PacketExchangeManager _packetExchangeManager;

    public MachineRecipeSelectionApi(PacketExchangeManager packetExchangeManager)
    {
        _packetExchangeManager = packetExchangeManager;
    }

    public UniTask<MachineRecipeSelectionResponse> SendAsync(
        MachineRecipeSelectionRequest request,
        CancellationToken cancellationToken)
    {
        return _packetExchangeManager.GetPacketResponse<MachineRecipeSelectionResponse>(request, cancellationToken);
    }
}
```

既存の小さな集約クラス `VanillaApi` で構築・公開する。425行ある `VanillaApiWithResponse.cs` は変更しない。

- [ ] **手順3: 候補導出と一時循環Viewを実装する**

候補Providerは `MasterHolder.MachineRecipesMaster.MachineRecipes.Data` をBlockGuid完全一致と `IGameUnlockStateData.MachineRecipeUnlockStateInfos[guid].IsUnlocked` で絞り、既存マスタ順を維持する。

`MachineRecipeSelectionView` はVContainer注入で `IGameUnlockStateData` を受け取る。`Initialize` は `BlockGameObject`、既存の `machineRecipeCount` テキスト、そのテキストへ付けたButtonを受け取る。`CancellationTokenSource` を所有し、Getで現在選択を読み、クリック時に `[Guid.Empty, アンロック済み候補...]` の次GUIDを送る。成功応答または現在StateDetailだけから表示を更新し、楽観更新による表示ずれを起こさず失敗理由をログへ出す。`OnDestroy` でキャンセル・破棄する。

- [ ] **手順4: `MachineBlockInventoryView` を200行以下に保つ**

旧 `UpdateMachineRecipeView` ローカル関数と `MachineBlockStateDetail` の利用をすべて削除する。シリアライズされた `MachineRecipeSelectionView` 参照と初期化呼び出し1つだけを追加し、選択固有の非同期・UI処理はすべて選択Viewへ持たせる。

- [ ] **手順5: Prefabシリアライズ前にスクリプトをコンパイルする**

```bash
uloop compile --project-path ./moorestech_client
```

期待結果: コンパイル成功。必要な `.meta` ファイルがUnityによって生成される。

- [ ] **手順6: Unity Editor経由だけで両Prefabを変更する**

`uloop-execute-dynamic-code` スキルを使用する。動的C#で各Prefabを `PrefabUtility.LoadPrefabContents` により読み、ルートの `MachineBlockInventoryView` を探す。シリアライズ済み `machineRecipeCount` のTMP_Text参照を読み、そのテキストGameObjectへ `Button` を追加または再利用して、`targetGraphic` にTMP_Textを設定する。Prefabルートへ `MachineRecipeSelectionView` を追加または再利用し、`MachineBlockInventoryView` の新規シリアライズ参照へ設定する。`PrefabUtility.SaveAsPrefabAsset` で保存し、`PrefabUtility.UnloadPrefabContents` で解放する。

シェルからPrefab YAMLを開いて書き換えない。

- [ ] **手順7: コンパイルしてUIテストを実行する**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionUITest"
uloop get-logs --project-path ./moorestech_client --log-type Error
```

期待結果: テストがPASSし、新規Errorログがない。

- [ ] **手順8: 薄い移行UIをコミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Network/API/MachineRecipe \
  moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block \
  moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MachineRecipeSelection \
  moorestech_client/Assets/AddressableResources/UI/Block/MachineBlockInventory.prefab \
  moorestech_client/Assets/AddressableResources/UI/Block/GearMachineBlockInventory.prefab
git commit -m "feat: 機械レシピ選択の移行用UIを追加"
```

---

### タスク6: 機械回帰テストを移行し、バグハント型QAを行う

**対象ファイル:**

- 変更: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearMachineIoTest.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/IdlePowerRateTest.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineFluidIOTest.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineIOTest.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/QualityModuleOutputTest.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Energy/MachineMultiSegmentPowerSupplyTest.cs`
- 変更: `moorestech_server/Assets/Scripts/Tests/Util/VanillaMachineProcessorTestUtil.cs`

**インターフェース:**

- 利用: タスク2の `MachineRecipeSelectionTestHelper.Select`。
- 提供: 既存の全機械挙動テストが、入力投入・tick実行前に対象レシピを明示選択する状態。

- [ ] **手順1: コンパイルエラーと対象検索で全自動開始テストを列挙する**

実行:

```bash
rg -l "VanillaMachineProcessorComponent|InsertRecipeInputs|TryGetRecipeElement" moorestech_server/Assets/Scripts/Tests
uloop compile --project-path ./moorestech_client
```

失敗したテストファイルをすべて記録し、互換用の自動選択ヘルパーは追加しない。

- [ ] **手順2: 各加工テストを安定GUIDの事前選択へ更新する**

入力投入前に次の明示呼び出しを使用する。

```csharp
var selectionResult = MachineRecipeSelectionTestHelper.Select(
    block,
    ForUnitTestMachineRecipeId.ElectricMachineRecipe,
    playerId: 0);
Assert.AreEqual(MachineRecipeSelectionResult.Success, selectionResult);
InsertRecipeInputs(inventory, recipe);
```

歯車機械には `GearMachineRecipe` を使う。液体テストでは既存の液体レシピを明示アンロックしてから使用する。`Select` のplayerIdにはデフォルト引数を設定しない。

- [ ] **手順3: 対象機械テスト群を実行して境界バグを狩る**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Machine|GearMachine|QualityModule|IdlePowerRate"
```

失敗時はアサーションを弱めず原因を調査する。特に最大スタック直前、複数消費スタック、同一ItemIdで異なるメタデータ、加工完了境界での変更、加工中の入力再充填、液体レシピ、ロード直後の変更を検証する。

- [ ] **手順4: プロトコル・保存・UIテスト群を同時実行し層間競合を検出する**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelection|MachineSaveLoad|GearMachineSaveLoad|FluidMachineSaveLoad"
uloop get-logs --project-path ./moorestech_client --log-type Error
```

期待結果: 対象テストがすべてPASSし、新規Errorログがない。Unityがドメインリロード中と報告した場合は45秒待ち、同じコマンドを再実行する。

- [ ] **手順5: 最終コンパイルと構造検査を実行する**

```bash
uloop compile --project-path ./moorestech_client
find moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection \
  moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/Job \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe \
  moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/RecipeSelection \
  -name '*.cs' -exec wc -l {} +
rg -n "partial|event Action|new Action|try[[:space:]]*\{" \
  moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection \
  moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/Job \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe \
  moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/RecipeSelection
```

期待結果: コンパイル成功。列挙した全ファイルが200行以下で、禁止パターン検索が0件になる。

- [ ] **手順6: 最終差分を確認作業ではなくバグハントとしてレビューする**

実行時レシピGUIDが1つだけであること、返却事前確認が状態を変更しないこと、拒否時に全状態が維持されること、キャンセル成功時に旧ジョブを出力しないこと、保存識別子がGUID文字列であること、両テンプレートに選択コンポーネントがあることを確認する。PrefabがUnityシリアライズ経由でだけ変更され、`.moorestech-external-revisions.json` が差分に含まれないことも確認する。

- [ ] **手順7: 回帰テスト移行と修正をコミットする**

```bash
git add moorestech_server/Assets/Scripts/Tests moorestech_server/Assets/Scripts/Tests.Module \
  moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Server.Protocol \
  moorestech_client/Assets/Scripts moorestech_client/Assets/AddressableResources/UI/Block
git commit -m "test: 機械レシピ明示選択の回帰QAを追加"
```

列挙したパスにはリポジトリ直下の `.moorestech-external-revisions.json` が含まれない。コミット前に `git diff --cached --name-only` で確認し、このファイルを変更しない。

---

## 計画セルフレビュー

### 配置インベントリ

| 項目 | アセンブリ・層 | 使用機構 | 判定 |
|---|---|---|---|
| `MachineRecipeSelectionState`、選択コンポーネント | `Game.Block` の機械ドメイン | 実行時状態、UniRx、ブロック単位JSON保存 | 既存のブロック設定前例と一致 |
| `MachineProcessingJob`、返却トランザクション | `Game.Block` の機械ドメイン | 実行時ジョブスナップショット、インベントリ事前確認 | ドメイン所有。Core.Inventoryには配置しない |
| `MachineRecipeSelectionStateDetail` | `Game.Block.Interface` | MessagePack BlockState契約 | 既存StateDetail配置と一致 |
| GUID索引と一意性検証 | `Core.Master` | マスタの生検索・検証だけ | 実行時選択状態を含まない |
| 選択Request・Response | `Server.Protocol` | MessagePack Request-Response | FilterSplitterのRequest-Response前例と一致 |
| `MachineRecipeSelectionApi` | `Client.Network` | PacketExchangeManager・UniTask | VanillaApiの集約パターンと一致 |
| 候補Providerと一時View | `Client.Game` | クライアント側アンロック導出、VContainer、Unity UI | サーバー状態変更ロジックを含まない |

- 仕様網羅性: タスク1〜6で、GUID一意性、自動判定撤去、実行時GUID単一所有、キャンセル・返却、液体消失、永続化、Request-Response通信、BlockState同期、薄いUI、電力・歯車機械の同等性、QAを網羅している。
- プレースホルダー検査: 合格。すべての変更手順に具体的なコード、コマンド、期待挙動がある。
- 型整合性: `MachineRecipeSelectionResult`、`TrySetRecipe`、`GetSelectedRecipeGuid`、`TryCancelProcessing`、プロトコルのRequest・Response、テストヘルパー名が全タスクで一致している。
- 配置レビュー: 実行時状態は `Game.Block`、通信DTOは `Server.Protocol`、公開StateDetailは `Game.Block.Interface`、UI導出は `Client.Game` に置き、Core.Masterは生マスタの索引・検証だけを行う。
- 新規機構の根拠: 選択GUIDは既存同期データから導出できず、任意GUIDを受け取る既存payloadもないため新規プロトコルが必要。選択変更通知は新規Eventパケットを作らずBlockStateを再利用する。
- 保存レビュー: JSONはMachineRecipeGuid・ItemGuid・FluidGuidを使用し、`MasterHolder` で解決する。揮発IDやマスタ由来容量は保存しない。
