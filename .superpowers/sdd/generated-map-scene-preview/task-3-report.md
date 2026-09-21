# Task 3 report: real-master preview verification

Status: DONE

Worktree: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/feat-generated-map-scene-preview`

Base: `e5977de83` (Task 1, Task 2 including pending-close corrections, and the independently reviewed outcrop correction).

Beads: `moorestech-uvrb.1`. This issue covers the controller's complete implementation workflow and remains open.

## Implementation

- Added real-master integration tests that call the public `GeneratedMapPreviewStage.Regenerate` entry point in EditMode and require Ready within 600 seconds for each generation. They inspect the actual run's temporary `map.json` and transfer metadata, rather than a separately generated substitute.
- The same Stage completes three generations. Every run compares all MapObject GUIDs, instance IDs, positions, rotations and scales; every outcrop's position, rotation, scale and prefab source; and the spawn position. Tree presence combines the generation placement category with the map master's `tree` mining-sound category. Rock presence uses the generation master's rock effect categories. No asset-name or path heuristic supplies these classifications.
- Every terrain tile is compared with `WorldTerrainSession`: exact terrain count, origin/size/resolution, ordered layer assets, material/shader, collider data, detail settings and prototype assets, every integer density cell in every detail layer, and all four neighbor references. Heights are sampled at 16 asymmetric/boundary indices per tile with Unity's height quantization tolerance.
- Every regeneration asserts destruction of the previous root and TerrainData and deletion of its temporary world. Resource totals must remain equal to one run; closing the final Stage must restore the main Scene and leave no new TerrainData or preview world.
- Added a full-world pending-close test and a first-alphamap-yield cancellation test that disposes Content twice, resumes the continuation, and requires `OperationCanceledException` rather than a missing native object.
- Added a dedicated small-world fixture using copied master inputs, a temporary debug-directory override and a unique generation fingerprint. It owns only its unique generated cache. The public regeneration sequence is zero placements/Ready, one placement/Ready, one missing prefab/Failed with exact GUID/address/count logs, and restored input/Ready. Missing runs must discard their partial content.
- Added an `EditModeInPlayingTest` regression that starts a real pending preview, enters Play with domain reload, verifies MainStage and temporary-world cleanup, then initializes an ordinary generated game from the real master. The existing public skit skip action ends the intro before checking visible terrain. A Unity teardown exits Play and reclaims test assets even after an assertion failure.
- Added the operator guide at `docs/development/generated-map-preview.md`: menu, current-master regeneration, framing, temporary/no-save behavior, window-only close, safe cancellation boundary, and failure/retry diagnostics.

Production code and APIs were not changed by Task 3. No scene/prefab/master/save files were edited. Unity generated all committed `.meta` files. The controller approved the structural test split and the focused Play regression.

## Final validation

Commands used the absolute worktree client path:

```text
uloop compile --project-path <worktree>/moorestech_client
uloop run-tests --project-path <worktree>/moorestech_client --filter-type regex --filter-value 'MapPreview|TerrainDataAssemblerGateTest|TerrainAlphamap' --unsaved-changes fail --timeout-seconds 1500
```

- Final plain compile: **0 errors, 45 warnings**. None of the warning file paths is a new Task 3 source; the output retains the existing generator/obsolete API warnings. Evidence: `compile-final.json`.
- Final affected suite: **41/41 passed, 0 failed, 0 skipped**, including the Play transition/ordinary-game regression and all three complete parity generations. NUnit ran `2026-09-21 17:54:33Z` to `18:01:08Z` (394.93 seconds). Evidence: `affected-suite-final.xml`, `affected-suite-final-summary.json`, `final-observations.log`.
- Targeted final Play regression before the full suite: **1/1 passed**, NUnit completed `2026-09-21 17:43:48Z`. Evidence: `play-regression-skip-intro.xml`.
- The preceding affected suite completed **40/41 passed**; its 40 EditMode tests included three complete real-master parity runs. Its Play failure was a missing local Node dependency, described below. This earlier result is not presented as a fully passing suite.

Real-master measured dimensions: 34,227 MapObjects, including **21,135 placements in the tree category** and **3,019 in the rock category**, plus 1,416 outcrops, 9 terrains, 19 ordered terrain layers, and **226,492,416 detail cells compared per generation**. The shader is `Universal Render Pipeline/Terrain/Lit`.

## Real Editor UI, SceneView and lifecycle evidence

Unity `6000.3.8f1`, owned worktree Editor PID `88265`, real master pin `587ce983`. All preview image/state evidence below was taken with `EditorApplication.isPlaying == false`. The only Play session was the ordinary-game regression and its pending-preview transition.

The menu was opened through `EditorApplication.ExecuteMenuItem`. Generate, Regenerate, Frame Spawn, Frame All, and Close were exercised by actual `EditorWindow.SendEvent` MouseDown/MouseUp events through the window's IMGUI controls, not calls to the private button handlers. Successful event consumption and resulting states are saved in the `ui-final-*.json` evidence. A temporary 1000×700 SceneView was used for legibility and closed afterward.

| Operation | Observed result | Evidence |
| --- | --- | --- |
| UI Generate | Generating → Ready in EditMode; expected/created 35,643, missing 0; 9 terrains, 34,227 MapObjects, 1,416 outcrops | `ui-final-generate.json`, `ui-final-frame-all.json`, Ready-window PNG |
| Frame All / Frame Spawn | Both IMGUI clicks consumed; SceneView showed the full generated terrain and spawn vicinity | `ui-final-frame-all.json`, `ui-final-frame-spawn.json` |
| Close window only | Window count 0 while Stage remained Ready and its root remained alive | `window-only-close.json` |
| Reopen and UI Regenerate | Generating; previous root and all previous TerrainData destroyed immediately | `ui-final-regenerate.json` |
| UI Close during whole-map generation | After synchronous world creation returned, a real pending terrain yield had root count 1/TerrainData count 1. The actual Close button reached Closed/MainStage; captured root/data were destroyed | `ui-pending-close.json` |
| Compilation during generation | `CompilationPipeline.RequestScriptCompilation()` was requested at the same real pending boundary. `beforeAssemblyReload` observed Closed/MainStage with destroyed root/data; afterward no preview Stage, nonasset TerrainData or preview world remained | `reload-requested.json`, `reload-before-assembly.json`, `reload-after.json`, `manual-after-reload.json` |
| Buttons after reload | Generate again reached Generating; immediate UI Close reached Closed. The controls did not remain disabled | `ui-after-reload-retry.json` |
| Error logs after manual lifecycle checks | 0 Error entries after clearing prior test/diagnostic entries | `errors-after-ui-close.json`, `errors-after-manual.json` |

The pending UI Close and compilation requests were issued by one-shot Editor update observers as soon as the main thread reached a real yield after whole-world provisioning. This measures the documented safe boundary; it does not claim that synchronous generation can process input mid-call. The observers and all diagnostic scripts live only in ignored evidence files.

SceneView captures were made with `uloop screenshot --capture-mode window`; each exact returned PNG was opened and visually inspected. The terrain, grass, trees, rocks and outcrops were visible without pink/missing materials:

| View | PNG in the evidence directory | Observation |
| --- | --- | --- |
| All terrain | `Preview Evidence_20260922_024638_503.png` | Complete tiled generated landscape with forest/rock regions |
| Spawn vicinity | `Preview Evidence_20260922_024659_528.png` | Terrain, trees, rocks and outcrops around the spawn framing |
| Ready controls | `Generated Map Preview_20260922_024659_722.png` | Ready, expected 35,643 / created 35,643 / missing 0 |
| Grass surface | `Preview Evidence_20260922_024723_052.png` | Visible ground grass; sampled positive detail cell at layer 16, density 1, Grass1 |
| Tree | `Preview Evidence_20260922_024924_378.png` | Fir3, GUID `794cedcc-8441-58d8-8c0b-835af7d14db4`, selected from master categories |
| Rock | `Preview Evidence_20260922_024937_579.png` | Pebble, GUID `c74efe49-52f3-403b-9c9a-b39eb1c85fce`, selected from the generation rock category |
| Outcrop | `Preview Evidence_20260922_024949_645.png` | Visible outcrop for vein GUID `735633b7-7aac-4fb8-8b42-022f6bfb9e53` |

The `frame-grass/tree/rock/outcrop.json` files bind feature identity, frame position, Ready status and `playing=false` to these captures. Earlier pre-outcrop screenshots and the initially misclassified tree-frame image remain historical diagnostic evidence, not the final acceptance images.

## Play transition and ordinary-game regression

The NUnit `EnterPlayMode(expectDomainReload: true)` command is issued directly from the Unity test after real preview world creation but before preview completion. It is a real Editor Play/domain-reload transition, not a mocked state change or a toolbar click. The controller approved combining this boundary with the planned ordinary-game regression.

After reload the current Stage is MainStage, no GeneratedMapPreviewStage remains, and the recorded preview temporary world no longer exists. The test then uses the existing game initialization helper, its own temporary world directory, real masters, and disabled autosave. It subscribes to `GameInitializedEvent` before startup. The real intro skit temporarily hides EnvironmentRoot, so the existing `SkitPresentationStateStore.TrySkip` action is used before measuring visible terrain.

The passing run observed **9 active terrains**, nonempty terrain layers/detail prototypes, the terrain shader, matching TerrainCollider/TerrainData, and a valid ground hit at `(500.00, 13.44, 500.00)` with the player at `(500.00, 13.45, 500.00)`. This establishes normal-game initialization after the shared terrain component changes; EditMode parity and screenshots remain the preview acceptance evidence.

## Non-mutation and ownership

- The correct user save root is `/Users/sakastudio/Library/Application Support/moorestech/Saves`. Its **59 files** were hashed before any Play test and compared after the manual checks: no added, removed or changed files. The early `Application.persistentDataPath` scan found zero files; it was superseded by the correct root and is not used to support preservation.
- All **252 files** under the real master's `server_v8/mods` and the original `GameInitialaizer.unity` asset retained their SHA-256 hashes. The final post-suite comparison also found no added/removed/changed files among those masters, the **59 user-save files**, or the original Scene asset. Evidence: `baseline-hashes.json`, `baseline-user-saves.json`, `hash-comparison-after-manual.json`, `hash-comparison-final.json`.
- During the uninterrupted manual preview lifecycle, active Scene path and handle hash `397678595`, four root instance IDs, total object count **7**, and `dirty=false` were unchanged before, while Ready, after UI Close and after compilation. Once the temporary evidence SceneView was closed, preview Scene count returned to its baseline **1**, with preview-world count **0**. Evidence: `manual-before.json`, `manual-ready.json`, `manual-after-ui-close.json`, `manual-after-reload.json`, `manual-final.json`.
- Test Runner reloads/restores its own bootstrap scenes around tests, so its instance IDs are not conflated with the uninterrupted manual Scene identity check. Every integration fixture independently asserts its saved main Scene remains unchanged while the Stage is active.
- Shared ordinary generation caches are allowed to remain. Preview-owned worlds are measured separately under `moorestech_client/Temp/GeneratedMapPreview/<PID>/<GUID>` and are reclaimed on rerun/close/reload/Play.
- Final Editor state after the completed suite: EditMode/MainStage, `GameInitialaizer.unity` active and clean, zero preview Stages, zero nonasset TerrainData, zero preview temporary worlds. No test fixture assets remained in git status. Evidence: `final-editor-state.json`.

## Failures found and corrective iterations

1. Initial real preview lacked outcrops. Task 3 paused; the controller completed and reviewed the production correction in `e5977de83`. Final evidence was rerun afterward and verifies all 1,416 outcrops.
2. The small fixture initially assumed `generateObject=false` removed tree-placement output. Trees are generated separately. The fixture now explicitly sets only its uniquely owned cached snapshot to 0 or 1 MapObject and clears veins. Focused retry passed 2/2. One first-attempt fixture cache existed before its cleanup field was assigned; the controller verified its unique 33-resolution fingerprint and moved it recoverably to `/Users/sakastudio/.Trash/moorestech-preview-fixture-94d41ec1052fee24`. It was not a user/shared ordinary world and its original path was confirmed absent.
3. An initial full integration run completed one full parity pass, then the repository TestWatchdog's default 180,000 ms limit called `Environment.FailFast` during the second generation. This is an interrupted run, not a passed repeated-regeneration test. Evidence includes `test-watchdog-timeout.log`, `editor-before-watchdog-restart.log`, and `watchdog-unity.ips`. The controller approved explicit NUnit timeout metadata only for the planned long integration tests. `moores-wt status` confirmed no Editor owned the worktree before only this Editor was relaunched. A later complete three-generation run passed.
4. The first Play run was environment-incomplete: the worktree lacked Node/web UI dependencies. Main and worktree lockfile hashes matched; with controller authorization, ignored `moorestech_web/node` and `moorestech_web/webui/node_modules` were APFS-cloned from the main worktree. Node reported v20.18.1. Main files and lockfiles were not modified. Its failed NUnit result is retained in `affected-suite-first.xml`.
5. The existing minimal test server initializes a terrain with no detail prototypes; its biome detail entries are empty. A targeted attempt therefore failed the new grass assertion (`play-regression-retry.xml`). The regression now uses the real master.
6. The first real-master Play attempt built all 9 terrains but measured `Terrain.activeTerrains` during the intro's hidden-background phase (`play-regression-real-master.xml`). Inspection found the existing public skip action; using it restores the ordinary environment and the focused regression passes. No production visibility change was made.
7. Visual QA found `treePlacement` contains boulders as well as trees. The earlier log label `trees 28654` counted that placement set, not trees alone. The final test intersects it with the map master's authoritative tree mining-sound category; the final full suite reruns this corrected classification. The all-GUID/all-pose comparison was unaffected.
8. Two initial test-only compile errors (unsupported NUnit `Is.AnyOf`, missing SpawnPointObject namespace) were corrected before successful compilation. A one-off dynamic observation queried outcrops before their container existed; its diagnostic exception was cleared before the final manual lifecycle checks. Neither attempt is counted as passing evidence.

The uloop CLI disconnects with `UNITY_DISCONNECTED_AFTER_ACCEPT` during the test's Play domain reload. The accepted Unity run continues. Results are taken from Unity's persisted `TestResults.xml` with its fresh completion time and copied into evidence; no disconnected run is blindly restarted or presumed passed.

## Files and self-review

New code:

- `Client.Tests/UnitTest/MapPreview/GeneratedMapPreviewIntegrationTest.cs`
- `Client.Tests/UnitTest/MapPreview/Integration/GeneratedMapPreviewObservation.cs`
- `Client.Tests/UnitTest/MapPreview/Integration/GeneratedMapPreviewPlacementParity.cs`
- `Client.Tests/UnitTest/MapPreview/Integration/GeneratedMapPreviewTerrainParity.cs`
- `Client.Tests/UnitTest/MapPreview/Integration/GeneratedMapPreviewSmallWorld.cs`
- `Client.Tests/UnitTest/MapPreview/Integration/GeneratedMapPreviewSmallWorldTest.cs`
- `Client.Tests/EditModeInPlayingTest/Terrain/GeneratedMapPreviewPlayTransitionTest.cs`

Paths above are under `moorestech_client/Assets/Scripts/`; each new source is at most 200 lines and each affected directory has at most 10 code files. Supporting files are Unity-generated metas, the operator guide and this report.

Self-review against the brief and lens digest: tests use public Editor features and Unity Stage/Scene APIs; no production test hooks, reflection, partial classes, new callbacks/interfaces, save-format changes or generator reimplementation were introduced. Runtime startup follows the existing EditModeInPlayingTest helper. Resource ownership is concrete and fixture-specific. Full arrays and all placements are compared rather than replacing assertions with screenshots. Previously failed and unmeasured attempts are separated from completed evidence.

The persistence recheck found only fixture writes to copied master JSON, an owned cache's existing `MapInfoJson` format, and test scenes saved through Unity. Fixture placements retain `MapObjectGuidStr`; no ItemId/FluidId/BlockId, master-derived capacity/weight, MessagePack persistence, schema version or migration behavior changed. User-save hashes are the direct non-mutation check. Source/document diff whitespace checks pass; the Unity-generated folder `.meta` retains Unity's three standard empty-value trailing spaces and was not hand-edited.

## Evidence handoff and remaining concerns

Raw evidence remains in the ignored worktree directory `.superpowers/sdd/generated-map-scene-preview/task-3-evidence/`. The controller owns the authorized copy/commit/push to:

`/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/sdd/generated-map-scene-preview/task-3-evidence/`

This report does not claim that archive handoff is already committed. Task 3 has no remaining acceptance blocker. No Tasks 4–5, plan checkboxes or PR actions were performed. Editor quit during generation is not a Task 3 acceptance item and was not measured. The real generation path remains synchronously blocking until its next yield, as the operator guide states.
