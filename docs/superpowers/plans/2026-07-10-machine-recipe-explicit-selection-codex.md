# Machine Recipe Explicit Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace input-based automatic machine recipe detection with one explicitly selected recipe per placed machine, including transactional cancellation/refund, persistence, synchronization, and a migration-safe minimal UI.

**Architecture:** `Game.Block` owns one shared `MachineRecipeSelectionState` per machine. The processor reads that state but never stores a second recipe GUID; processing stores only a job snapshot. A machine selection component validates and persists selection, coordinates cancellation, and publishes BlockState changes, while a Request-Response protocol identifies the operating player from `PacketResponseContext`.

**Tech Stack:** Unity 6, C#/.NET, NUnit, UniRx, MessagePack for transport, Newtonsoft JSON for saves, VContainer, UniTask, uloop.

## Global Constraints

- The machine holds exactly one runtime recipe GUID: the selected recipe GUID.
- An unselected machine uses `Guid.Empty` and never starts processing.
- A processing-state object must not store `MachineRecipeGuid` or `MachineRecipeMasterElement`.
- Cancellation eligibility depends only on whether every consumed item can return to machine input first and the operating player's main inventory second.
- Consumed fluids disappear on successful cancellation and never block a recipe change.
- Save recipe/item/fluid identities as MachineRecipeGuid/ItemGuid/FluidGuid JSON strings; never persist ItemId, FluidId, BlockId, capacity, max stack, or slot count.
- Do not add schema fields or edit generated `Mooresmaster.Model.*` code.
- Do not use `partial`, C# `Action`/`event` for new notifications, `try-catch`, or default arguments.
- Every created or modified code file must be 200 lines or fewer; create a subdirectory before a directory would exceed 10 code files.
- Complex methods use local functions under a method-local `#region Internal`; no class-level `#region Internal`.
- Add concise Japanese/English two-line comments to major processing sections about every 3–10 lines.
- Do not create `.meta` files manually. Let Unity create them.
- Do not edit prefab YAML directly. Change prefabs only through `uloop execute-dynamic-code`/Unity Editor serialization.
- After every `.cs` task, run `uloop compile --project-path ./moorestech_client`.
- Run tests with `--filter-type regex`; after a domain-reload error wait 45 seconds before retrying.
- Do not touch the user's unrelated `.moorestech-external-revisions.json` change.

---

### Task 1: Replace Automatic Recipe Identity With Unique GUID Lookup

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestMachineRecipeId.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/MachineRecipeConfigTest.cs`

**Interfaces:**
- Produces: `MachineRecipesMaster.GetRecipeElement(Guid machineRecipeGuid) : MachineRecipeMasterElement`
- Produces: globally unique `MachineRecipeGuid` values in the test master.
- Removes: `MachineRecipesMaster.TryGetRecipeElement(BlockId, List<ItemId>, List<FluidId>, out MachineRecipeMasterElement)` and input-key helpers.

- [ ] **Step 1: Replace the automatic-lookup tests with GUID invariants**

Keep `MachineRecipeConfigTest` under 200 lines and replace its input-to-recipe tests with the following core assertions:

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

- [ ] **Step 2: Run the focused test and confirm the shared GUID data fails**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeConfigTest"
```

Expected: FAIL because the first seven test recipes currently share `bd3d4d7d-9c3b-4ae1-875b-950327eedd9d`, and the alternate recipe constant/data does not exist yet.

- [ ] **Step 3: Assign stable unique test GUIDs and add explicit constants**

Use these exact GUIDs for the formerly shared entries, in current JSON order:

```text
MachineId             10000000-0000-0000-0000-000000000001
BlockId               10000000-0000-0000-0000-000000000002
BeltConveyorId        10000000-0000-0000-0000-000000000003
GearMachine           10000000-0000-0000-0000-00000000000f
MachineRecipeTest1    10000000-0000-0000-0000-000000000019
MachineRecipeTest2    10000000-0000-0000-0000-00000000001a
MachineRecipeTest3    10000000-0000-0000-0000-00000000001b
```

Add these exact two recipes for same-input selection and locked-selection tests:

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

Add these constants:

```csharp
public static readonly Guid ElectricMachineRecipe = new("10000000-0000-0000-0000-000000000001");
public static readonly Guid GearMachineRecipe = new("10000000-0000-0000-0000-00000000000f");
public static readonly Guid MachineRecipeTest1Basic = new("10000000-0000-0000-0000-000000000019");
public static readonly Guid ElectricMachineAlternateRecipe = new("10000000-0000-0000-0000-000000000101");
public static readonly Guid LockedElectricMachineRecipe = new("10000000-0000-0000-0000-000000000102");
```

- [ ] **Step 4: Replace the input-key dictionary with a GUID dictionary**

Implement the master with this shape:

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

Change `MachineRecipesMasterUtil.Initialize` to build `Dictionary<Guid, MachineRecipeMasterElement>`. In `Validate`, use a `HashSet<Guid>` and append a validation error for every duplicate GUID. Delete `GetRecipeElementKey`, input sorting, and duplicate-input rejection; recipes with equal inputs are now legal.

- [ ] **Step 5: Compile and run the master tests**

Run:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeConfigTest|MasterValidation"
```

Expected: compile succeeds and all matching tests PASS.

- [ ] **Step 6: Commit the identity migration**

```bash
git add moorestech_server/Assets/Scripts/Core.Master/MachineRecipesMaster.cs \
  moorestech_server/Assets/Scripts/Core.Master/Validator/MachineRecipesMasterUtil.cs \
  moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json \
  moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestMachineRecipeId.cs \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Block/MachineRecipeConfigTest.cs
git commit -m "refactor: 機械レシピを一意GUID参照へ移行"
```

---

### Task 2: Implement the Single-Recipe Machine Runtime and Transactional Cancellation

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSelectionState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSelectionResult.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeInputValidator.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/VanillaMachineRecipeSelectionComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/Job/MachineProcessingJob.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/Job/MachineRecipeRefundTransaction.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/ProcessState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorSaveJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/MachineRecipe/MachineRecipeSelectionStateDetail.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/MachineRecipeMaster.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.Block.Interface/State/MachineBlockStateDetail.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineInputInventory.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/MachineProcessContext.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/IdleMachineProcessState.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/State/ProcessingMachineProcessState.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineSaveComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs`
- Test Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineRecipeSelection/MachineRecipeSelectionTestHelper.cs`
- Test Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineRecipeSelection/MachineRecipeSelectionProcessingTest.cs`
- Test Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineRecipeSelection/MachineRecipeCancellationRefundTest.cs`

**Interfaces:**
- Produces: `MachineRecipeSelectionResult VanillaMachineRecipeSelectionComponent.TrySetRecipe(Guid recipeGuid, IOpenableInventory playerMainInventory)`
- Produces: `Guid VanillaMachineRecipeSelectionComponent.GetSelectedRecipeGuid()`
- Produces: `bool VanillaMachineProcessorComponent.TryCancelProcessing(IOpenableInventory playerMainInventory)`
- Produces: `List<IItemStack> VanillaMachineInputInventory.ConsumeInputs(MachineRecipeMasterElement recipe)`
- Consumes: `MachineRecipesMaster.GetRecipeElement(Guid)` from Task 1.

- [ ] **Step 1: Write failing explicit-selection and refund tests**

Cover these exact cases across the two test files:

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

Also test machine-only refund, machine-then-player refund, same-GUID idempotence, locked recipe rejection, other-machine recipe rejection, successful clear producing no old outputs, `IsRemain` exclusion, and consumed fluid disappearance.

- [ ] **Step 2: Run the focused tests and verify they fail before implementation**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProcessingTest|MachineRecipeCancellationRefundTest"
```

Expected: FAIL because selection component, result enum, and transactional cancellation do not exist.

- [ ] **Step 3: Implement the one-GUID selection state and public selection component**

Use method-based access rather than new trivial getter/setter properties:

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

`VanillaMachineRecipeSelectionComponent` must validate in this order: same GUID (success/no-op), `Guid.Empty` clear, recipe existence, matching BlockGuid, unlock state, cancellation/refund, then state mutation and `_onChangeBlockState.OnNext(Unit.Default)`. Its notification is a private `Subject<Unit>` exposed as `IObservable<Unit>`.

- [ ] **Step 4: Implement input validation, exact consumption snapshots, and refund preflight**

Move `RecipeConfirmation` behavior into `MachineRecipeInputValidator` in the machine domain. Remove the old automatic lookup utility and remove `IGameUnlockStateData` from `VanillaMachineInputInventory`.

Change input consumption to return the exact consumed stacks:

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

The consumed fragment must be created from the source stack before subtraction with `source.SubItem(source.Count - input.Count)` so item metadata is preserved.

Implement refund as preflight then commit:

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

`SimulateInsert` must create a temporary `OpenableInventoryItemDataStoreService` with `AllowMultipleStacksPerItemOnInsert = false`, copy current machine input slots with `SetItemWithoutEvent`, and return its `InsertItem` remainders.

- [ ] **Step 5: Replace the processing recipe snapshot with a GUID-free job snapshot**

`MachineProcessingJob` stores total ticks, remaining ticks, pending item outputs, pending fluid outputs, and consumed items. It exposes behavior methods such as `ConsumeTicks`, `IsComplete`, `GetPendingItemOutputs`, `GetPendingFluidOutputs`, and `GetConsumedItems`; it has no recipe/master field.

Move the processor save DTO out of `VanillaMachineProcessorComponent.cs` in this task so the runtime refactor compiles. Serialize the job as `totalSeconds`, `remainingSeconds`, `pendingOutputs`, `pendingFluidOutputs`, and `consumedItems`; reconstruct the same job in `BlockTemplateUtil.MachineLoadState`. Task 3 adds selection-component persistence and verifies the complete JSON contract.

Change Idle flow to:

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

Normal `OnExit` inserts job outputs and clears the job. Cancellation calls `MachineRecipeRefundTransaction.TryExecute`; on success it clears the job without output. `VanillaMachineProcessorComponent.TryCancelProcessing` changes Processing to Idle and notifies its UniRx subject only after refund succeeds.

Move `ProcessState`/`ProcessStateExtension` out of `VanillaMachineProcessorComponent.cs` so the processor is under 200 lines. Delete `MachineBlockStateDetail`; processor state output keeps only `CommonMachineBlockStateDetail`, while selection state comes from the selection component.

- [ ] **Step 6: Wire one shared state into electric and gear templates**

For Task 2's New and Load paths, initialize the not-yet-persisted selection as `Guid.Empty` and create objects in this order:

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

Add `recipeSelection` to both template component lists. Remove unlock state from `GetMachineIOInventory` and update every constructor call without default arguments. Task 3 replaces `Guid.Empty` in Load paths with the persisted selection before processor construction.

- [ ] **Step 7: Compile and run focused runtime tests**

Run:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProcessingTest|MachineRecipeCancellationRefundTest"
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: compile succeeds, focused tests PASS, and no new Error logs exist.

- [ ] **Step 8: Commit the runtime engine**

```bash
git add moorestech_server/Assets/Scripts/Game.Block \
  moorestech_server/Assets/Scripts/Game.Block.Interface/State \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineRecipeSelection
git commit -m "feat: 機械レシピの明示選択と安全な加工中変更を実装"
```

---

### Task 3: Persist Selection and GUID-Free Processing Jobs

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/MachineRecipeSelectionSaveJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorSaveJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/RecipeSelection/VanillaMachineRecipeSelectionComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineSaveComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/BlockTemplateUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaMachineTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaGearMachineTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Util/VanillaMachineProcessorTestUtil.cs`
- Test Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/MachineRecipeSelection/MachineRecipeSelectionSaveLoadTest.cs`
- Test Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/MachineSaveLoadTest.cs`
- Test Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/GearMachineSaveLoadTest.cs`
- Test Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/FluidMachineSaveLoadTest.cs`

**Interfaces:**
- Produces: JSON selection save `{ "machineRecipeGuid": "..." }`.
- Produces: processor JSON fields `state`, `totalSeconds`, `remainingSeconds`, `pendingOutputs`, `pendingFluidOutputs`, `consumedItems` and no recipe GUID.
- Consumes: Task 2 job and selection state.

- [ ] **Step 1: Write failing save/load tests**

Add assertions equivalent to:

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

Add a round-trip test that loads a processing machine, clears its recipe, and verifies restored consumed items are refunded. Add electric, gear, and fluid selection round trips.

- [ ] **Step 2: Run the save tests and confirm missing DTO fields fail**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionSaveLoadTest|MachineSaveLoadTest|GearMachineSaveLoadTest|FluidMachineSaveLoadTest"
```

Expected: FAIL because the selection component has no save key/state yet; job JSON assertions expose any missing GUID-based fields.

- [ ] **Step 3: Implement GUID-based JSON DTOs**

Use this data contract shape:

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

Verify and complete `VanillaMachineProcessorSaveJsonObject`: it stores seconds rather than ticks, serializes pending/refund items with `ItemStackSaveJsonObject`, and serializes fluid stacks with `FluidStackSaveJsonObject`. It must not serialize ItemId/FluidId or the recipe GUID.

- [ ] **Step 4: Restore selection before constructing the processor**

Add a static load method on the selection component that reads its own component key and returns a `MachineRecipeSelectionState`. Update both templates so the state is loaded first, the job is reconstructed second, and the component/processor share that same state object.

If a save claims Processing but lacks a complete job snapshot, load it as Idle. No compatibility migration logic beyond a deterministic invalid-job fallback is required.

- [ ] **Step 5: Compile and run save/load tests**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionSaveLoadTest|MachineSaveLoadTest|GearMachineSaveLoadTest|FluidMachineSaveLoadTest"
```

Expected: all matching tests PASS.

- [ ] **Step 6: Commit persistence**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine \
  moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad \
  moorestech_server/Assets/Scripts/Tests/Util/VanillaMachineProcessorTestUtil.cs
git commit -m "feat: 機械レシピ選択と加工ジョブを保存"
```

---

### Task 4: Add the Server Selection Protocol and BlockState Synchronization

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe/MachineRecipeSelectionProtocol.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe/MachineRecipeSelectionProtocolMessages.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Test Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/MachineRecipe/MachineRecipeSelectionProtocolTest.cs`

**Interfaces:**
- Produces: protocol tag `va:machineRecipeSelection`.
- Produces: `CreateGetRequest(Vector3Int)` and `CreateSetRequest(Vector3Int, Guid)`.
- Produces: response `{ Success, AppliedRecipeGuidStr, FailureReason }`.
- Consumes: `PacketResponseContext.PlayerId`, `IPlayerInventoryDataStore`, and Task 2 selection component.

- [ ] **Step 1: Write failing Get/Set/failure-reason protocol tests**

Test Get-unselected, Set-success, clear-success, malformed GUID, block-not-found, non-machine, unbound player, wrong-block recipe, locked recipe, refund-capacity failure, and response sequence handling. Bind the player only through context:

```csharp
var context = new PacketResponseContext();
context.BindPlayerId(0);
var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), context);
var response = MessagePackSerializer.Deserialize<MachineRecipeSelectionResponse>(responseBytes[0]);
```

- [ ] **Step 2: Run the protocol tests and verify the missing tag fails**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProtocolTest"
```

Expected: FAIL because the protocol/tag is not registered.

- [ ] **Step 3: Implement messages in a separate file to stay under 200 lines**

Use these enums and factories:

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

Request MessagePack keys start at 2: Position, Operation, RecipeGuidStr. Response keys start at 2: Success, AppliedRecipeGuidStr, FailureReason. Parse external GUID strings with `Guid.TryParse`; do not use exceptions for request validation.

- [ ] **Step 4: Implement protocol orchestration and registration**

The protocol must:

1. Deserialize and validate Position/operation/GUID.
2. Resolve block and `VanillaMachineRecipeSelectionComponent`.
3. Return current selection for Get.
4. Require `context.PlayerId` for Set.
5. Resolve `IPlayerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory`.
6. Call `TrySetRecipe` and map the domain result to the transport failure enum.
7. Always return the component's current GUID, including rejected Sets.

Register it in `PacketResponseCreator` with its exact protocol tag.

- [ ] **Step 5: Compile and run protocol plus BlockState tests**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionProtocolTest|ChangeBlockEventPacketTest|InvokeBlockStateEventProtocolTest"
```

Expected: all matching tests PASS and a successful Set produces selection StateDetail through the existing BlockState path.

- [ ] **Step 6: Commit protocol work**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MachineRecipe \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/MachineRecipe
git commit -m "feat: 機械レシピ選択プロトコルを追加"
```

---

### Task 5: Add a Thin Migration UI Without Finalizing Layout

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Network/API/MachineRecipe/MachineRecipeSelectionApi.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/RecipeSelection/MachineRecipeSelectionCandidateProvider.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/RecipeSelection/MachineRecipeSelectionView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/MachineBlockInventoryView.cs`
- Editor Modify: `moorestech_client/Assets/AddressableResources/UI/Block/MachineBlockInventory.prefab`
- Editor Modify: `moorestech_client/Assets/AddressableResources/UI/Block/GearMachineBlockInventory.prefab`
- Test Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MachineRecipeSelection/MachineRecipeSelectionUITest.cs`

**Interfaces:**
- Produces: `MachineRecipeSelectionApi.SendAsync(MachineRecipeSelectionRequest, CancellationToken)`.
- Produces: `MachineRecipeSelectionCandidateProvider.GetCandidates(Guid blockGuid, IGameUnlockStateData) : List<MachineRecipeMasterElement>`.
- Produces: a click-to-cycle temporary selector whose first candidate is unset and whose remaining candidates are unlocked recipes for that block.

- [ ] **Step 1: Write the failing PlayMode UI test**

The test opens an electric machine inventory, verifies the selector text starts at `未設定`, clicks the existing recipe text/button, waits for the response, and verifies both server selection and visible text changed. Click through all candidates and verify wraparound includes `未設定`. Add a rejection case where the server keeps the previous applied GUID.

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionUITest"
```

Expected: FAIL because the client API/view/prefab button do not exist.

- [ ] **Step 2: Add a dedicated API instead of enlarging `VanillaApiWithResponse`**

Implement:

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

Construct and expose it from the existing small `VanillaApi` aggregator. Do not modify the 425-line `VanillaApiWithResponse.cs`.

- [ ] **Step 3: Implement candidate derivation and the temporary cycle view**

The candidate provider filters `MasterHolder.MachineRecipesMaster.MachineRecipes.Data` by exact BlockGuid and `IGameUnlockStateData.MachineRecipeUnlockStateInfos[guid].IsUnlocked`, then orders by existing master order.

`MachineRecipeSelectionView` receives `IGameUnlockStateData` through VContainer injection. `Initialize` receives the `BlockGameObject`, existing `machineRecipeCount` text, and a Button attached to that text. It owns a `CancellationTokenSource`, loads current selection with Get, and on click sends the next GUID from `[Guid.Empty, unlocked candidates...]`. It updates text only from successful response/current StateDetail and logs the failure reason without optimistic state drift. Cancel/dispose in `OnDestroy`.

- [ ] **Step 4: Keep `MachineBlockInventoryView` below 200 lines**

Remove its old `UpdateMachineRecipeView` local function and all `MachineBlockStateDetail` usage. Add only a serialized `MachineRecipeSelectionView` reference and one initialization call. The selection view owns all selection-specific async/UI logic.

- [ ] **Step 5: Compile scripts before prefab serialization**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: compile succeeds and Unity generates required `.meta` files.

- [ ] **Step 6: Modify both prefabs through Unity Editor only**

Use the `uloop-execute-dynamic-code` skill. The dynamic C# must load each prefab with `PrefabUtility.LoadPrefabContents`, find the root `MachineBlockInventoryView`, read its serialized `machineRecipeCount` TMP_Text reference, add/reuse a `Button` on that text GameObject, set `targetGraphic` to the TMP_Text, add/reuse `MachineRecipeSelectionView` on the prefab root, assign the new serialized reference on `MachineBlockInventoryView`, save with `PrefabUtility.SaveAsPrefabAsset`, and unload with `PrefabUtility.UnloadPrefabContents`.

Do not open or rewrite prefab YAML from the shell.

- [ ] **Step 7: Run compile and the UI test**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectionUITest"
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: test PASS and no new Error logs.

- [ ] **Step 8: Commit the thin UI**

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

### Task 6: Migrate Machine Regression Tests and Perform Bug-Hunting QA

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/GearMachineIoTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/IdlePowerRateTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineFluidIOTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineIOTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/MachineModuleSlotTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/QualityModuleOutputTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Energy/MachineMultiSegmentPowerSupplyTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Util/VanillaMachineProcessorTestUtil.cs`

**Interfaces:**
- Consumes: `MachineRecipeSelectionTestHelper.Select` from Task 2.
- Produces: all existing machine behavior tests explicitly select their intended recipe before inserting inputs/ticking.

- [ ] **Step 1: Use compile errors and focused search to enumerate every auto-start test**

Run:

```bash
rg -l "VanillaMachineProcessorComponent|InsertRecipeInputs|TryGetRecipeElement" moorestech_server/Assets/Scripts/Tests
uloop compile --project-path ./moorestech_client
```

Record every failing test file; do not add a compatibility auto-selection helper.

- [ ] **Step 2: Update each processing test to select a stable recipe GUID first**

Use an explicit call before input insertion:

```csharp
var selectionResult = MachineRecipeSelectionTestHelper.Select(
    block,
    ForUnitTestMachineRecipeId.ElectricMachineRecipe,
    playerId: 0);
Assert.AreEqual(MachineRecipeSelectionResult.Success, selectionResult);
InsertRecipeInputs(inventory, recipe);
```

Use `GearMachineRecipe` for gear machines and the existing unlocked fluid recipe after explicitly unlocking it in fluid tests. Do not give `Select` a default playerId argument.

- [ ] **Step 3: Run focused machine suites and hunt for boundary bugs**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Machine|GearMachine|QualityModule|IdlePowerRate"
```

Investigate failures rather than weakening assertions. Specifically exercise max-stack-minus-one, multiple consumed stacks, identical item IDs with different metadata, completion-boundary recipe changes, input refill during processing, fluid recipes, and change immediately after load.

- [ ] **Step 4: Run protocol, save, and UI suites together to expose cross-layer races**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelection|MachineSaveLoad|GearMachineSaveLoad|FluidMachineSaveLoad"
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: all matching tests PASS and no new Error logs. If Unity reports domain reload, wait 45 seconds and rerun the identical command.

- [ ] **Step 5: Perform the final compile and structural checks**

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

Expected: compile succeeds, every listed file is at most 200 lines, and the prohibited-pattern search returns no matches.

- [ ] **Step 6: Review the final diff as bug hunting, not confirmation**

Check that only one runtime recipe GUID exists, refund preflight cannot mutate, rejected changes preserve every state, successful cancellation cannot output the old job, saved identities are GUID strings, and both templates contain the selection component. Confirm prefabs changed only through Unity serialization and `.moorestech-external-revisions.json` is absent from the diff.

- [ ] **Step 7: Commit regression migrations and fixes**

```bash
git add moorestech_server/Assets/Scripts/Tests moorestech_server/Assets/Scripts/Tests.Module \
  moorestech_server/Assets/Scripts/Game.Block moorestech_server/Assets/Scripts/Server.Protocol \
  moorestech_client/Assets/Scripts moorestech_client/Assets/AddressableResources/UI/Block
git commit -m "test: 機械レシピ明示選択の回帰QAを追加"
```

The listed paths do not include the repository-root `.moorestech-external-revisions.json`; verify with `git diff --cached --name-only` before committing and do not alter that file.

---

## Plan Self-Review

### Placement Inventory

| Item | Assembly/layer | Mechanism | Verdict |
|---|---|---|---|
| `MachineRecipeSelectionState`, selection component | `Game.Block` machine domain | Runtime state, UniRx, block-local JSON save | Matches block settings precedent |
| `MachineProcessingJob`, refund transaction | `Game.Block` machine domain | Runtime job snapshot and inventory preflight | Domain-owned; not placed in Core.Inventory |
| `MachineRecipeSelectionStateDetail` | `Game.Block.Interface` | MessagePack BlockState contract | Matches existing StateDetail placement |
| GUID index and uniqueness validation | `Core.Master` | Raw master lookup/validation only | Contains no runtime selection state |
| Selection Request/Response | `Server.Protocol` | MessagePack Request-Response | Matches FilterSplitter request/response precedent |
| `MachineRecipeSelectionApi` | `Client.Network` | PacketExchangeManager/UniTask | Matches the VanillaApi composition pattern |
| Candidate provider and temporary view | `Client.Game` | Client unlock derivation, VContainer, Unity UI | Contains no server mutation logic |

- Spec coverage: Tasks 1–6 cover GUID uniqueness, removal of automatic detection, one-GUID runtime ownership, cancellation/refund, fluid loss, persistence, Request-Response transport, BlockState synchronization, thin UI, electric/gear parity, and QA.
- Placeholder scan: passed; every change step names concrete code, commands, and expected behavior.
- Type consistency: `MachineRecipeSelectionResult`, `TrySetRecipe`, `GetSelectedRecipeGuid`, `TryCancelProcessing`, protocol request/response, and test helper names are consistent across tasks.
- Placement review: runtime state remains in `Game.Block`; transport DTOs remain in `Server.Protocol`; the public StateDetail remains in `Game.Block.Interface`; UI derivation remains in `Client.Game`; Core.Master only indexes/validates raw master data.
- New-mechanism justification: a new protocol is required because selected GUID is neither derivable from existing synchronized data nor accepted by an existing payload. Selection change notification reuses BlockState instead of adding an event packet.
- Save review: JSON uses MachineRecipeGuid/ItemGuid/FluidGuid and resolves through `MasterHolder`; no volatile IDs or master-derived capacities are persisted.
