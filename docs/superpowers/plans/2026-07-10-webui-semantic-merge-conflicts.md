# Web UI Semantic Merge Conflicts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve the five Web UI semantic merge conflicts against the current game domain model.

**Architecture:** Runtime inventory size and stack limits stay owned by their existing runtime stores and are read at the Web UI boundary. Machine recipes expose BlockId directly and block imagery is served through a dedicated endpoint backed by BlockImageContainer.

**Tech Stack:** Unity 6, C#, ASP.NET Core Kestrel, React, TypeScript, Vitest

## Global Constraints

- Do not edit Unity YAML assets, generated `Mooresmaster.Model.*`, or `.meta` files manually.
- Do not use `partial`, `Action` events, default arguments, or broad `try-catch` additions.
- Keep every source file at 200 lines or fewer and preserve Japanese/English paired intent comments.
- Use `LocalPlayerInventory.MainSlotCount`; hotbar is always the final `PlayerInventoryConst.HotBarSlotCount` slots.
- Resolve MaxStack through `IItemStackLevelLookup.GetMaxStack(ItemId)` and do not cache the JSON response.
- Machine recipe contracts use `blockId`, never a disguised ItemId.
- Machine block icons are display-only; item input/output selection remains interactive.

---

### Task 1: Dynamic inventory area mapping

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/InventoryAreaMapper.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/BlockInventoryActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/InventoryTopic.cs`
- Modify: all action handler call sites of `InventoryAreaMapper`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/InventoryAreaMapperTest.cs`

**Interfaces:**
- Consumes: `ILocalPlayerInventory.MainSlotCount`, `PlayerInventoryConst.HotBarSlotCount`
- Produces: mapping methods that take `int mainSlotCount` explicitly

- [ ] Change mapper tests to use a 54-slot inventory and assert main index 44, hotbar indices 45..53, invalid main 45, and invalid hotbar 9.
- [ ] Run `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "InventoryAreaMapperTest"` and confirm the changed tests fail before implementation.
- [ ] Add `mainSlotCount` to mapper APIs and derive `mainAreaSize = mainSlotCount - HotBarSlotCount` inside each mapping call.
- [ ] Pass `_controller.LocalPlayerInventory.MainSlotCount` from every action and topic call site; map block slot zero to combined index `mainSlotCount`.
- [ ] Re-run the filtered Unity test and confirm it passes.
- [ ] Commit the task.

### Task 2: Runtime MaxStack item master response

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/ItemMasterEndpoint.cs`
- Modify if required: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`
- Modify: `moorestech_web/webui/src/bridge/store/itemMasterStore.ts`
- Test: `moorestech_web/webui/src/bridge/store/itemMasterStore.test.ts` or the existing focused store test file
- Test: add or extend the focused Web UI endpoint test under `moorestech_client/Assets/Scripts/Client.Tests/WebUi/`

**Interfaces:**
- Consumes: `ItemStackLevelDataStore.Instance` exposed as `IItemStackLevelLookup`, `GetMaxStack(ItemId)`
- Produces: `/api/master/items` JSON with the current MaxStack on every request and a browser store that refreshes after successful loads

- [ ] Add a focused test or directly test extracted JSON construction so a level change changes MaxStack between consecutive builds.
- [ ] Run the focused test and confirm the old cached/master-property behavior fails.
- [ ] Remove `_cachedJson` and `ClearCache`; build JSON on every request and call the lookup for each ItemId.
- [ ] Remove obsolete ItemMaster cache clearing from WebUiHost shutdown and add only the necessary assembly reference if the concrete static access requires it.
- [ ] Keep `itemMasterStore` loading after a successful response by waiting the existing `RETRY_INTERVAL_MS` and fetching again; replace the Map on each success.
- [ ] Use fake timers/fetch mocks to prove a second successful response updates MaxStack, then run the focused C# and TypeScript tests and commit the task.

### Task 3: BlockId machine recipes and block icon endpoint

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/MachineRecipesTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/BlockIconEndpoint.cs` through normal source editing; let Unity generate its `.meta`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.ts`
- Modify: `moorestech_web/webui/src/features/recipe/craftLogic.ts`
- Modify: `moorestech_web/webui/src/features/recipe/craftLogic.test.ts`
- Modify: `moorestech_web/webui/src/features/recipe/views/RecipeContent.tsx`
- Modify: `moorestech_web/webui/src/features/recipe/views/MachineRecipeView.tsx`
- Create: a focused `BlockIcon.tsx` beside the existing shared ItemIcon component, following the current directory structure and 10-file rule

**Interfaces:**
- Produces: `MachineRecipe.blockId: number`, `/api/block-icons/{blockId}.png`, display-only `BlockIcon`

- [ ] Change TypeScript tests and validators from `blockItemId` to `blockId`, preserving the craft-tab null discriminator with an accurately named field.
- [ ] Run the focused Vitest file and confirm it fails before implementation.
- [ ] Emit `BlockId = blockId.AsPrimitive()` in C# and remove BlockMaster-to-Item conversion.
- [ ] Implement BlockIconEndpoint by mirroring ItemIconEndpoint parsing, main-thread PNG encoding, ETag behavior, 503/404 handling, and cache clearing.
- [ ] Route the new endpoint and clear its cache on host shutdown.
- [ ] Add BlockIcon rendering for tabs and machine center; remove only the machine icon item-selection callback.
- [ ] Run `pnpm test -- --run src/features/recipe/craftLogic.test.ts` and `pnpm build` from `moorestech_web/webui`.
- [ ] Run Unity compile, fix all resulting contract errors, and commit the task.

### Task 4: Integration QA and conflict residue scan

**Files:**
- Inspect all files changed by Tasks 1-3.

**Interfaces:**
- Consumes: all prior task outputs
- Produces: verified, merge-ready integration

- [ ] Run `rg -n "blockItemId|PlayerInventoryConst\\.MainInventorySize|\\.MaxStack" moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_web/webui/src` and require no legacy semantic references.
- [ ] Run the focused Unity Web UI tests with regex `InventoryAreaMapperTest|.*WebUi.*` as supported by the current test assembly.
- [ ] Run `pnpm test -- --run` and `pnpm build` in `moorestech_web/webui`.
- [ ] Run `uloop compile --project-path ./moorestech_client` and require zero errors.
- [ ] Inspect Unity error logs with `uloop get-logs --project-path ./moorestech_client --log-type Error` and distinguish pre-existing runtime logs from new defects.
- [ ] Review every diff for 200-line limits, bilingual comment intent, hidden fixed-size assumptions, stale caches, and false ItemId/BlockId equivalence.
- [ ] Commit any QA fixes and the final integration state.
