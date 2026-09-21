# Generated map preview outcrop correction

Status: DONE

Worktree: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/feat-generated-map-scene-preview`

Base: `56a02fe6a`. Beads: `moorestech-uvrb.1.1` (left claimed and open for the controller).

## Implementation

The preview now consumes the generated world's `Map.MapVeins` and creates one outcrop per placement under `GeneratedMapPreview/VeinOutcrops`. It resolves `MapVeinMaster.OutcropAddressablePath` through the existing `EditorTerrainAssetLoader`, caching borrowed prefab references and load failures by address for the current placement pass.

The representation follows `OutcropGameObjectDatastore`: the center is `(min + max + 1) / 2` for each axis because the generated bounds contain inclusive cells; world rotation is identity; the prefab's local scale and prefab link are preserved. No placement randomization, generator internals, gameplay startup, datastore registration, communication, or mining initialization is called. Existing prefab components are retained without initialization.

`GeneratedMapPreviewRun` tracks expected/created outcrops separately from its existing MapObject counters. The stage's displayed totals and success criterion now include both categories. Missing masters, unavailable assets, and unsuccessful prefab expansion log their reasons and leave each affected placement missing. Any missing placement prevents `Ready` and disposes the incomplete run.

Instances are parented under the existing Content root immediately. The existing normal-close, failure, and pending-generation cancellation paths own their cleanup. Placement yields every 50 instances and passes cancellation through asset loading and the immediate-cancellation yield.

## Changed files

- `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewRun.cs`
- `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewStage.cs`
- `moorestech_client/Assets/Scripts/Editor/MapScene/Preview/GeneratedMapPreviewOutcrops.cs` and Unity-generated `.meta`
- `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/MapPreview/GeneratedMapPreviewOutcropTest.cs` and Unity-generated `.meta`
- This report.

No Task 3 test/document/evidence file, plan checkbox, master, save format, or generation code was edited. No PR was created.

## Validation

Commands were run against this worktree's existing Editor (PID 7594, confirmed through `moores-wt status` and released by the controller for this fix).

1. `uloop compile --project-path <worktree>/moorestech_client`
   - Final explicit compile: success, 0 errors, 16 warnings. The warnings identify existing UnitGenerator duplicate definitions and obsolete APIs in `EditModeInPlayingTestUtil`; no changed file is named.
   - An initial compile caught a missing `using Server.Boot` in the new test; corrected before running tests.
2. `uloop run-tests --project-path <worktree>/moorestech_client --test-mode EditMode --filter-type class --filter-value GeneratedMapPreviewOutcropTest --unsaved-changes fail`
   - Initial result: 2 passed, 1 failed.
   - The failure exposed an incorrect fixture assumption: some real outcrop prefabs already contain `OutcropGameObject`. All per-placement position, rotation, scale, prefab-link, scene, active-object, and renderer assertions had passed before the incorrect empty-component assertion.
   - Corrected the assertion to require an empty runtime vein GUID on every authored component, plus no `OutcropGameObjectDatastore`. Production initialization was not changed.
   - Initial XML: `moorestech_client/.uloop/outputs/TestResults/20260921_170208_2740950_9e8546223051412d8e8f6a9f22b908e6.xml`.
3. `uloop run-tests --project-path <worktree>/moorestech_client --test-mode EditMode --filter-type regex --filter-value '^(Client\.Tests\.UnitTest\.MapPreview\.|Client\.Tests\.Map\.VeinOutcropAddressableLoadTest\.)' --unsaved-changes fail`
   - Final result: **24/24 passed**, 0 failed, 0 skipped; automatic pre-test compile also succeeded.
   - Completed at `2026-09-21T17:03:51.4612270Z`.
   - This runs the complete MapPreview suite and the existing real-master outcrop-address tests. It includes all three new tests:
     - All real generated veins create active renderer-bearing instances at their runtime centers with matching transforms and prefab links; the stage reaches `Ready`; close destroys owned instances while borrowed prefabs survive.
     - Two placements sharing one missing asset and a missing-master placement produce exactly three missing instances, emit expected reasons, leave the stage `Failed`, and discard partial content.
     - Closing at the 50-instance yield of a 51-placement batch cancels the pending run and destroys its instances through the existing terminal cleanup.
4. `git diff --check`: clean. Changed C# files are 155, 197, 97, and 174 lines respectively, each below 200. The existing preview directory has 8 code files and the MapPreview test directory has 5.

## Self-review

- Compared the new rendering behavior directly with the runtime outcrop precedent, including repeated layouts with the same vein GUID. Counts are per placement, not per GUID.
- The Editor helper consumes only the public `MapVeinInfoJson` results and existing master references. It introduces no generation settings, caches, placement algorithm, or runtime datastore.
- New production members are internal/private. No partial classes, `Func`, default arguments, or event `Action` were added.
- Cancellation checks occur before each layout, after each asset await, and after each yield. New scene objects belong to Content through their parent as soon as they are instantiated.
- Catches are confined to the external asset-loading/prefab-expansion boundaries and record both a reason and a missing placement. Cancellation exceptions from loading are allowed to propagate.
- Save/persistence recheck: production serialization and save version are unchanged; the code only reads the existing GUID-based generated layouts. The test's temporary main scene is saved through Unity and removed in teardown. No save migration is required.

## Scope and remaining integration work

No outstanding correctness concern was found in this bounded correction. The focused tests exercise the actual private run placement and stage completion methods with real generated output; they omit the expensive terrain-assembly portion. Task 3's full `ExecuteAsync`/SceneView integration and evidence capture remain with the controller and were not reclassified as completed here.

The branch does not contain `.agents/skills/agent-runtime-compat/SKILL.md`; this absence was reported to the controller, and the branch's documented uloop CLI procedures were used.
