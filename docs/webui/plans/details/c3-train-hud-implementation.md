# Phase C3 Train HUD Implementation Plan

> **For agentic workers:** Execute the tasks in order. Each task is one reviewable commit boundary, but this worktree must not be committed by Codex per the task instruction.

**Goal:** Preserve C# ownership of train driving while rendering the riding HUD, nested riding pause menu, and cargo-car sub inventory in Web UI with reconnect-safe snapshots.

**Architecture:** `UIStateControl` remains the top-level state authority. `TrainHUDScreenState` exposes its nested state and riding presentation through notifications; `ui_state.current` carries the nested sub-state, while `train.riding` carries reconnect-safe riding/branch presentation. The existing `block_inventory.current` and block-slot action path are generalized to the already-unified `SubInventoryState`, so block and train cargo inventories share gestures without adding a parallel inventory stack.

**Tech Stack:** Unity 6/C#, UniRx, MessagePack event packets, WebSocket Topic envelopes, React 18, TypeScript, Zustand, Zod, Mantine, Vitest, Playwright mock host.

## Global Constraints

- Driving input remains in `TrainRidingInputSender`: W/S are held state plus a 2-second heartbeat; A/D are key-down previous/next branch selections.
- Web adds no speedometer and no branch-selection Action. It displays the selected branch only; C# continues to drive the 3D preview.
- Every state Topic is revisioned by the existing hub envelope and restores from a current snapshot after reconnect.
- Visible strings use `useI18n().t(key)` and tutorial-facing controls use `tutorialAnchor`.
- Do not modify `bridge/transport/webSocketClient`, `bridge/store/topicStore`, or `Client.WebUiHost/Boot`.
- C# files stay under 200 lines, use no `partial`, no default arguments, no `Action` for new event APIs, and retain bilingual intent comments.
- Do not hand-create `.meta` files or directly edit Unity YAML assets.

## Verified uGUI behavior

### Top-level and nested transitions

| Current | Input/event/condition | Next | Side effects |
|---|---|---|---|
| `GameScreen` | E down and nearest train car within 3 m | `TrainHUDScreen/GameScreen` | Sends Ride RPC, then sets player state to Riding on success |
| `TrainHUDScreen/GameScreen` | Esc | `TrainHUDScreen/PauseMenuScreen` | Camera control off; pause service enters |
| `TrainHUDScreen/PauseMenuScreen` | pause service close (Esc) | `TrainHUDScreen/GameScreen` | pause service exits; camera control/cursor driving state restored |
| `TrainHUDScreen/GameScreen` | E down and Dismount RPC succeeds | `GameScreen` | player state becomes Normal on TrainHUD exit |
| either nested state | matching-player `RidingStateEventPacket(Dismount)` | `GameScreen` | forced dismount flag; next top-level update exits |
| either nested state | riding target missing or no cached train snapshot | `GameScreen` | forced local exit |
| ride request pending | failure response | `GameScreen` | no driving input is accepted before ride context exists |

Pause suppresses E/W/S/A/D driving and hides the branch preview because `TrainHUDScreenState` only calls those paths while the nested state is `GameScreen`.

### Topic and Action contract

`ui_state.current`:

```ts
type UiStateData = {
  state: string;
  subState?: "GameScreen" | "PauseMenuScreen";
};
```

`subState` is present only for `TrainHUDScreen`. This preserves the actual top-level state instead of masquerading the nested pause as `PauseMenu`.

`train.riding`:

```ts
type TrainRidingData =
  | { riding: false; branchCandidateCount: 0; selectedBranchIndex: 0 }
  | { riding: true; branchCandidateCount: number; selectedBranchIndex: number };
```

The snapshot derives `riding` from the current `UIStateControl`/`TrainHUDScreenState`, never from the last event. The handler subscribes to top-level changes, the train state presentation notification, and `RidingStateEventPacket`; this is the required initial snapshot + event relay + subscription set. A forced dismount publishes false immediately and the subsequent state transition remains authoritative. Thus a dismount while Web is disconnected cannot survive reconnect as stale `riding:true`.

No train-driving or branch-selection Action is added. Riding-pause close uses the existing `ui_state.request(GameScreen)` intent only after the handler is extended to interpret that request as “close nested pause” while the top-level state is `TrainHUDScreen`; it must not dismount.

`block_inventory.current` becomes a sub-inventory discriminated contract:

```ts
type SubInventoryData =
  | { open: false }
  | { open: true; source: "block"; blockType: string; identifier: string; blockName: string; itemSlots: SlotData[]; ...capabilities }
  | { open: true; source: "train"; identifier: string; blockName: string; itemSlots: SlotData[]; error?: "containerMissing" | "trainCarMissing" | "openFailed"; fluidSlots: [] };
```

The wire topic/action names remain unchanged to avoid duplicating the unified inventory transport. Existing `"block"` slot refs are retained as the protocol’s established “sub inventory area”; the C# parser is generalized to accept either `BlockSubInventorySource` or `TrainSubInventorySource`.

## Data flow and ownership

```text
HybridInput W/S/A/D → TrainRidingInputSender → server train input → ClientTrainUnit selected index → TrainHUDScreenState presentation → train.riding → Web HUD
UIStateControl → TrainHUDScreenState nested controller → ui_state.current{subState} → Web router/pause
SubInventoryState + TrainSubInventorySource → block_inventory.current → existing slot gestures/actions → LocalPlayerInventoryController
```

The new code is a reader/adapter at existing state stations. It does not introduce a second driving writer or inventory controller.

## Task 1: Riding presentation and nested-state notifications

**Files:**

- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreen/TrainHudScreenUIStateController.cs`
- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreenState.cs`
- Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/UiStateTopic.cs`
- Create `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/TrainRidingTopic.cs`
- Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`

- [ ] Add a UniRx `Subject<TrainHudScreenUIStateEnum>` notification to the nested controller and publish after every actual transition.
- [ ] Expose read-only riding presentation (`IsRiding`, nested state, candidate count, selected index) from `TrainHUDScreenState`; update it from the same `ClientTrainUnit` used by the preview.
- [ ] Add an explicit `RequestClosePauseMenu()` entry point that only affects `PauseMenuScreen`.
- [ ] Extend `UiStateTopic` snapshot/event data with optional `SubState`, subscribing to nested-state changes.
- [ ] Implement `TrainRidingTopic` with snapshot plus UI-state/presentation/packet subscriptions and immediate forced-dismount publishing.
- [ ] Register both topics and dispose their subscriptions through the hub’s existing handler lifecycle.

## Task 2: Routing and riding HUD

**Files:**

- Modify `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`
- Modify `moorestech_web/webui/src/bridge/contract/schemas/index.ts`
- Modify `moorestech_web/webui/src/bridge/contract/payloadTypes.ts`
- Modify `moorestech_web/webui/src/bridge/contract/validators.ts`
- Modify `moorestech_web/webui/src/bridge/transport/protocol.ts`
- Modify `moorestech_web/webui/src/shared/uiState/uiScreenRouting.ts`
- Modify `moorestech_web/webui/src/shared/uiState/uiScreenRouting.test.ts`
- Create `moorestech_web/webui/src/features/trainHud/TrainRidingHud.tsx`
- Create `moorestech_web/webui/src/features/trainHud/style.module.css`
- Create `moorestech_web/webui/src/features/trainHud/index.ts`
- Modify `moorestech_web/webui/src/app/App.tsx`

- [ ] Add Zod schemas for the optional nested sub-state and `train.riding`.
- [ ] Add `Topics.trainRiding`, `UiStateNames.trainHud`, and typed payload registration.
- [ ] Route `TrainHUDScreen/GameScreen` to `trainHud` and `TrainHUDScreen/PauseMenuScreen` to `trainPause`.
- [ ] Render an i18n riding label and W/S/A/D/E/Esc hints with `tutorialAnchor("train-hud.status")`; display branch position only when candidate count is greater than one.
- [ ] Reuse `PauseMenuPanel` for `trainPause`, but do not mount inventory chrome/player inventory around the HUD.
- [ ] Add Vitest coverage for both nested routes, non-riding absence, riding HUD, and branch display.

## Task 3: Train sub-inventory contract and operations

**Files:**

- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Train/ITrainInventoryView.cs`
- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Train/TrainInventoryView.cs`
- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/TrainSubInventorySource.cs`
- Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockInventoryTopic.cs`
- Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs`
- Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Inventory/BlockInventoryActions.cs`
- Modify `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`
- Modify `moorestech_web/webui/src/features/blockInventory/BlockInventoryPanel.tsx`
- Modify `moorestech_web/webui/src/features/blockInventory/BlockItemGrid.tsx`

- [ ] Persist the typed train error on `TrainInventoryView` so the Topic can read it without scraping localized text.
- [ ] In `BlockInventoryTopic`, build a train DTO directly from `CurrentSubInventory` and `TrainSubInventorySource`, including source, identifier, slots, and error.
- [ ] Add `source:"block"` to block-open DTOs and a `source:"train"` Zod branch.
- [ ] Generalize the existing sub-area parser to accept block or train sources and keep closed/range checks.
- [ ] Render train title, cargo slots, or the i18n error; retain existing close and slot gesture behavior.
- [ ] Add unit tests for success/error train payloads and for train slot action acceptance.

## Task 4: uGUI gates and deterministic classification

**Files:**

- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Train/TrainInventoryView.cs`
- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreen/TrainHudGameScreenSubState.cs`
- Modify `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/TrainHUDScreen/TrainHudPauseMenuSubState.cs`
- Modify `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs`

- [ ] Gate the train inventory root GameObject with `!WebUiScreenGate.IsWebUiMode`.
- [ ] Gate TrainHUD screen-space key-hint/pause presentation while leaving input sender, camera state, and 3D route preview alive.
- [ ] Classify `Inventory/Train/TrainInventoryView.cs` as `GatedRoot`; classify the remaining train inventory files as covered.
- [ ] Add explicit TrainHUD rules before the general `UIState` infra rule so migrated view-bearing files are not hidden by longest-prefix classification.

## Task 5: Wire fixtures, mock host, and tests

**Files:**

- Create `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/train_riding.json`
- Create `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/train_inventory.json`
- Create `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractC3Test.cs`
- Modify Web wire-contract fixture lists/tests
- Modify `moorestech_web/webui/e2e/mock-host/fixtures.ts`
- Modify `moorestech_web/webui/e2e/mock-host/wsHandler.ts`
- Modify `moorestech_web/webui/e2e/mock-host/state.ts`
- Create `moorestech_web/webui/e2e/tests/train.spec.ts`

- [ ] Add the mandatory default `train.riding` snapshot `{riding:false, branchCandidateCount:0, selectedBranchIndex:0}` to `topicData`.
- [ ] Add mock controls/fixtures for riding, nested pause, cargo slots, and each error.
- [ ] Verify HUD display, nested pause display/close, non-riding absence, cargo slots, cargo error, and reconnect restoring to non-riding.
- [ ] Do not run `pnpm test:e2e`; the user owns that sandbox-incompatible command.

## Task 6: Verification and review

- [ ] Run `cd moorestech_web/webui && pnpm test`.
- [ ] Run `cd moorestech_web/webui && pnpm build`.
- [ ] Run `cd moorestech_web/webui && pnpm lint`.
- [ ] Run `uloop compile --project-path ./moorestech_client`.
- [ ] Run focused C# tests with `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WebUi.*(C3|Gate|WireContract)"`.
- [ ] Review every changed C# file for 200-line, bilingual-comment, null, no-default-argument, no-partial, and no-Action-event compliance.
- [ ] Record PlayMode riding → branch → pause → dismount → cargo open/close as remaining real-device verification; do not claim it was executed.

## Self-review

- Requirement coverage: all seven requested areas map to Tasks 1–5; commands and the explicit real-device remainder map to Task 6.
- Counterexample: a forced dismount received while Web is disconnected must not leave the HUD mounted. The event immediately updates the Unity-side state, and reconnect snapshot derives from current top-level/riding state rather than cached Web/event data, so stale `riding:true` is rejected.
- Structural review: driving writes remain in `TrainRidingInputSender`; nested UI remains owned by `TrainHUDScreenState`; inventory operations remain in `LocalPlayerInventoryController`; Web topics are read adapters. No domain vocabulary is added to Boot/transport or a generic master layer.
- No placeholder work is deferred except the explicitly excluded PlayMode real-device smoke and user-owned e2e command.
