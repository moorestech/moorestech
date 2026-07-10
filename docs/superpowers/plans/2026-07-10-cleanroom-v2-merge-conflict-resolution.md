# Cleanroom v2 Merge Conflict Resolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the cleanroom feature while integrating the Blueprint and placement-system changes from `master-fable-tmp` without reviving removed block `itemGuid` data.

**Architecture:** Resolve each conflict according to domain ownership: schema additions stay in `VanillaSchema`, Blueprint and CleanRoom persistence coexist in `Game.SaveLoad`, and tests reference both domain assemblies. Keep the external master revision on the reachable incoming compatibility pin because the cleanroom-side SHA no longer exists locally or on the remote.

**Tech Stack:** Unity 6, C#, asmdef, Mooresmaster YAML source generation, NUnit, uloop

## Placement and precedent review

| Item | Assembly/layer | Mechanism and precedent |
|---|---|---|
| Cleanroom block enum values and parameters | `VanillaSchema` → generated `Core.Master` models | Existing `switch/cases` block schema; `edit-schema` workflow |
| Blueprint and CleanRoom load ordering | `Game.SaveLoad` | Constructor-injected datastore restore calls in `WorldLoaderFromJson` |
| Blueprint and CleanRoom test access | `Server.Tests` | Direct asmdef references already introduced by each feature branch |
| Cleanroom item reference validation | `Core.Master.Validator` | `BlockMasterUtil.BlockParamValidation` and `GetItemIdOrNull` |
| External master compatibility revision | Repository metadata | `.moorestech-external-revisions.json` reachable incoming pin; focused CleanRoom tests use the repository TestMod |

No new event, protocol, or generated class is introduced by the conflict resolution. Blueprint persistence keeps `BlockGuid`, while CleanRoom persistence keeps keyed cell coordinates and runtime impurity/class state; neither stores volatile `BlockId`, `ItemId`, or `FluidId` values.

## Global Constraints

- Do not edit Unity YAML assets, `.meta` files, generated `Mooresmaster.Model.*` classes, or `Library/` manually.
- Do not restore the intentionally removed block-level `itemGuid` schema property.
- Compile all merged C# changes and run focused CleanRoom, Blueprint, and save/load tests before committing.

---

### Task 1: Resolve schema and integration conflicts

**Files:**
- Modify: `.moorestech-external-revisions.json`
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `mooresmaster/mooresmaster.SandBox/TestMod/blocks.json`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Save/CleanRoomCellSaveJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Save/CleanRoomSaveData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Save/CleanRoomSaveRestore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomSaveLoadTest.cs`

**Interfaces:**
- Consumes: `IBlueprintDatastore`, `CleanRoomDatastore`, and generated cleanroom block parameter types.
- Produces: one `WorldLoaderFromJson` constructor accepting both datastores and one test assembly referencing both domain assemblies.

- [x] **Step 1: Resolve the block schema**

Keep the six cleanroom enum values while removing the obsolete block-level `itemGuid` property:

```yaml
      - FilterSplitter
      - CleanRoomWall
      - CleanRoomDoor
      - CleanRoomItemHatch
      - CleanRoomPipeHatch
      - CleanRoomAirFilter
      - CleanRoomMachine
    - key: requiredItems
```

- [x] **Step 2: Resolve save/load dependency injection**

Use one constructor tail containing both feature dependencies:

```csharp
IPlayerRidingDatastore playerRidingDatastore, IBlueprintDatastore blueprintDatastore,
ItemStackLevelDataStore itemStackLevelDataStore,
IPlayerInventorySlotLevelDataStore playerInventorySlotLevelDataStore,
CleanRoomDatastore cleanRoomDatastore)
```

Assign both `_blueprintDatastore` and `_cleanRoomDatastore`; preserve the existing Blueprint restore and CleanRoom rebuild/restore flows.

- [x] **Step 3: Resolve supporting metadata**

Set `_CompileRequester.cs` to a fresh merge-specific trigger string, include both `"Game.CleanRoom"` and `"Game.Blueprint"` in `Server.Tests.asmdef`, and use reachable external master commit `f8d25b0725600fbb47814179211d0035f383eba6`.

- [x] **Step 4: Verify foreign-key validation and remove stale block item keys**

Confirm `CleanRoomAirFilterBlockParam.FilterItemGuid` is checked with `MasterHolder.ItemMaster.GetItemIdOrNull` in `BlockMasterUtil.BlockParamValidation`. Remove every block-element-level `itemGuid` from the repository TestMod and Sandbox JSON while retaining nested `requiredItems[].itemGuid` and `filterItemGuid` references.

- [x] **Step 5: Store clean-room cells as keyed JSON objects**

Replace positional `[x,y,z]` arrays with `CleanRoomCellSaveJsonObject` values containing explicit `x`, `y`, and `z` keys. Add a save JSON test that asserts all three keys are present; old-save migration is intentionally unnecessary during development.

- [x] **Step 6: Compile and run focused tests**

Run:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(CleanRoom|Blueprint|SaveLoad)"
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: compilation succeeds, focused tests pass, and Unity reports no new error logs.

- [x] **Step 7: Commit the completed merge**

```bash
git add .moorestech-external-revisions.json VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef docs/superpowers/plans/2026-07-10-cleanroom-v2-merge-conflict-resolution.md
git commit
```
