# Moorestech Server Performance Optimization Summary

This is the single source of truth for the current performance investigation branch.

Old split docs were merged into this file:

- `server-performance-investigation.md`
- `game-update-deep-dive-2026-06-17.md`

Scope:

- Save: `C:\Users\5080\AppData\Roaming\.moorestech\Saves\save_1.json`
- Server data: `C:\Users\5080\Documents\GitHub\moorestech_master\server_v8\`
- Save size at baseline: about `2.71MB`
- Blocks: `6,302`
- Updatable block components: about `14,571-14,712`
- Gear networks: `6`
- Trains: `1`

Important caveat:

- These numbers are investigation measurements on the current save, not a formal isolated benchmark suite.
- Full tick measurements have spikes. Use averages, P50/P95/P99, and repeated runs.
- For micro-optimizations, prefer amplified measurements over one-call-per-component timings.

## Executive Summary

| Area | Initial / before | Latest / after | Improvement |
| --- | ---: | ---: | ---: |
| Startup `LoadOrInitialize` | `100.7s` | `1.396s` | about `72x` |
| Full `GameUpdater.Update` | `59.463ms/tick` | `28.065ms/tick` | about `52.8%` |
| `GearNetworkDatastore`, stable network | `15.510ms/tick` | `0.003ms/tick` | about `5160x` |
| Largest gear network, stable | `6.068ms/tick` | `0.005-0.006ms/tick` | about `1000x` |
| Largest gear network, generator add + update | `53.631ms` | `1.758ms` | about `30.5x` |
| `BlockSystem` direct update after profiler removal | `37.989ms/tick` | `31.750ms/tick` | `6.239ms/tick` |
| Belt component body, amplified | `1.581us/call` | `1.284us/call` | about `18.8%` |
| Fluid pipe body, amplified | `32.226us/call` | `23.514us/call` | about `27.0%` |
| Chest body, amplified | `108.449us/call` | `101.071us/call` | about `6.8%` |

Current conclusion:

- Startup and stable gear-network ticks are no longer the main bottleneck.
- Runtime profiler marker overhead was real and has been removed.
- Remaining work is mostly real `BlockSystem` component behavior, state event emission, and spike reduction.
- Current full tick average is below the `50ms` budget for `20 TPS`, but spikes still matter.

## Commit Timeline

Relevant performance commits on this investigation branch:

```text
eeeeacfbf Document game update performance baseline
ba4613647 Investigate game update hotspots
8b4022f85 Add gear consumption power curve
b8dde8ea1 Document performance optimization summary
4317bdfc9 Optimize chest transfer inventory paths
1390c3c5f Document gear balanced rpm experiment
bffad332d Optimize gear network dirty updates
f94196db6 Optimize non-gear block updates
```

Other important commits referenced by the notes:

```text
24b1bd06c Cache gear network topology
7b28abba1 Skip stable gear network ticks
52e0623aa Apply gear power balanced rpm search
256e20d8a Remove runtime profiler markers
```

## Measurement Rules

Baseline method:

- Load the current save through `MoorestechServerDIContainerGenerator` and `IWorldSaveDataLoader.LoadOrInitialize()`.
- Warm up before measuring.
- For full tick: collect `avg`, `p50`, `p95`, `p99`, `max`, and 50-tick windows.
- For component attribution: compare actual-order measurements with 100x amplified measurements.
- Treat sub-microsecond one-call timings as measurement-overhead dominated.

Common current-save game update command:

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.Investigation\.(GameUpdatePerformanceInvestigationTest\.ProfileCurrentSaveSteadyStateTickDistribution|GameUpdatePerformanceInvestigationTest\.ProfileCurrentSaveBlockComponentBreakdown|BlockSystemDeepDiveInvestigationTest\.ProfileCurrentSaveBlockSystemCostByBlockType|BlockSystemStateAndHotComponentInvestigationTest\.ProfileCurrentSaveHotComponentShapeAndActualCost)"
```

## Startup Load

Initial target:

- `C:\Users\5080\AppData\Roaming\.moorestech\Saves\save_1.json`
- Blocks: `6,302`

Initial result:

| Metric | Value |
| --- | ---: |
| `LoadOrInitialize` | about `100.7s` |

Root cause:

- Every block placement event scanned many coordinate-specific subscribers.
- Coordinate lookup and overlap checks used broad scans.
- Initial block load emitted placement events that were not useful for first client sync.

Applied changes:

- Added coordinate-indexed block lookup through `BlockCoordinateIndex`.
- Replaced overlap and `GetBlock(pos)` scans with coordinate-index lookups.
- Replaced coordinate-specific block place/remove subscriber scans with dictionary dispatch.
- Added `BlockPlaceProperties.IsInitialLoad`.
- Suppressed initial-load block placement broadcasts in `PlaceBlockEventPacket`.

After optimization:

| Metric | Value |
| --- | ---: |
| `LoadOrInitialize` | `1.396s` |
| `BlockFactoryLoad` | `771.882ms` |
| `TryAddBlock` | `318.645ms` |
| `PostBlockLoad` | `30.107ms` |

Startup conclusion:

- The old startup bottleneck from block placement subscriber dispatch is gone.
- Remaining startup cost is mainly block template restoration.

## Original Game Update Baseline

Purpose:

- Establish repeatable baseline before optimizing game update.
- Identify which `GameUpdater.Update()` parts are heavy.

Initial full tick distribution after warmup:

| Metric | Value |
| --- | ---: |
| Count | `300` |
| Total | `17,838.997ms` |
| Average | `59.463ms` |
| Min | `44.581ms` |
| P50 | `51.722ms` |
| P90 | `81.592ms` |
| P95 | `114.282ms` |
| P99 | `138.591ms` |
| Max | `157.895ms` |
| StdDev | `20.822ms` |

Initial 50-tick windows:

| Ticks | Average | P95 | Max |
| --- | ---: | ---: | ---: |
| `0-49` | `57.893ms` | `72.866ms` | `137.741ms` |
| `50-99` | `56.586ms` | `90.633ms` | `157.895ms` |
| `100-149` | `58.689ms` | `111.204ms` | `123.360ms` |
| `150-199` | `57.818ms` | `103.512ms` | `121.770ms` |
| `200-249` | `66.904ms` | `134.547ms` | `138.591ms` |
| `250-299` | `58.888ms` | `107.054ms` | `124.059ms` |

Phase split:

| Phase | Average | P50 | P95 | P99 | Max |
| --- | ---: | ---: | ---: | ---: | ---: |
| `Update` | `60.198ms` | `54.032ms` | `103.756ms` | `177.307ms` | `178.547ms` |
| `LateUpdate` | `0.002ms` | `0.001ms` | `0.002ms` | `0.002ms` | `0.234ms` |

Top-level baseline findings:

| Target | Average | P50 | P95 | P99 | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| `DirectBlockSystemTick` | `37.989ms` | `33.107ms` | `66.639ms` | `102.354ms` | Directly updates all block systems. |
| `BlockSystemWrapperOnlyTick` | `14.441ms` | `12.820ms` | `20.322ms` | `62.036ms` | Wrapper and profiler-marker path. |
| `GearNetworkDatastore` | `15.510ms` | `14.160ms` | `20.685ms` | `42.642ms` | Gear network update. |
| `MachineOutputInventories` | `0.851ms` | `0.806ms` | `1.462ms` | `1.935ms` | Small relative to block/gear. |
| `EnergySegments` | `0.029ms` | `0.024ms` | `0.041ms` | `0.075ms` | Negligible. |
| `TrainUpdateService` | `0.011ms` | `0.009ms` | `0.013ms` | `0.030ms` | Negligible. |
| `ChallengeDatastore` | `0.005ms` | `0.003ms` | `0.006ms` | `0.017ms` | Negligible. |
| `EmptySubscriberFullTick` | `0.081ms` | `0.077ms` | `0.091ms` | `0.180ms` | UniRx dispatch itself was not the bottleneck. |

Initial conclusion:

- Almost all cost is in `Update`, not `LateUpdate`.
- `GameUpdater` and UniRx dispatch are not the main problem.
- The dominant areas are `BlockSystem` and `GearNetworkDatastore`.

## BlockSystem Deep Dive

Measured all `6,302` block systems after warmup.

| Path | Average | P50 | P95 | P99 | Interpretation |
| --- | ---: | ---: | ---: | ---: | --- |
| `BlockSystemFullWithProfiler` | `39.236ms` | `34.789ms` | `65.893ms` | `113.896ms` | Old direct BlockSystem path. |
| `BlockSystemNoProfilerComponentBody` | `26.812ms` | `23.837ms` | `39.775ms` | `77.235ms` | Component bodies without per-component profiler marker. |
| `BlockSystemWrapperProfilerNoBody` | `13.874ms` | `12.898ms` | `17.873ms` | `57.671ms` | Profiler/wrapper path without `component.Update()`. |
| `BlockSystemBlockProfilerOnly` | `0.388ms` | `0.340ms` | `0.523ms` | `0.916ms` | Per-block outer markers only. |
| `BlockSystemComponentIterationOnly` | `0.740ms` | `0.631ms` | `1.015ms` | `1.322ms` | Component list iteration only. |

Reliable split:

- About `26.8ms` was real component body work.
- About `12.7ms` was per-component profiler marker/wrapper cost.
- Plain dispatch overhead was already only about `0.081ms`.

Block type amplified ranking:

| Block type | Blocks | Components | Full ms/tick | No-profiler ms/tick | Wrapper ms/tick | Notes |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `GearBeltConveyor` | `4,307` | `12,921` | `23.637` | `9.239` | `10.565` | Dominates wrapper cost and large body cost. |
| `Chest` | `41` | `41` | `6.068` | `6.795` | `0.040` | Mutation-sensitive; use for ranking. |
| `FluidPipe` | `143` | `143` | `6.241` | `2.524` | `0.139` | Real body cost. |
| `GearMachine` | `115` | `115` | `0.924` | `0.732` | `0.115` | Smaller but visible. |
| `ElectricMachine` | `85` | `88` | `0.546` | `0.467` | `0.057` | Smaller but visible. |
| `FuelGearGenerator` | `47` | `94` | `0.493` | `0.371` | `0.094` | Smaller but visible. |

Original component detail:

| Component | Count | Actual-order ms/tick | 100x amplified ms/tick | Confidence |
| --- | ---: | ---: | ---: | --- |
| `VanillaBeltConveyorComponent` | `4,307` | `7.836ms` | `6.321ms` | High |
| `VanillaChestComponent` | `41` | `5.623ms` | `5.267ms` | High |
| `FluidPipeComponent` | `143` | `3.026ms` | `2.448ms` | High |
| `VanillaMachineProcessorComponent` | `200` | `1.794ms` | `2.386ms` | High enough |
| `FuelGearGeneratorComponent` | `47` | `0.622ms` | `0.345ms` | Medium |
| `VanillaMinerProcessorComponent` | `26` | `0.301ms` | `0.188ms` | Medium |
| `GearBeltConveyorComponent` | `4,307` | `0.838ms` | `0.030ms` | Low; one-call timer overhead dominated. |
| `GearOverloadBreakageComponent` | `5,423` | `0.708ms` | `0.048ms` | Low; one-call timer overhead dominated. |

## Runtime Profiler Marker Removal

Bottleneck:

- `BlockSystem.Update()` created profiler markers for every updatable component every tick.
- The current save had about `14,712` updatable components.
- Under `ENABLE_PROFILER`, the old path built marker names like `BlockName_ComponentTypeName`.

Earlier decomposition:

| Path | Average |
| --- | ---: |
| `BlockSystemFullWithProfiler` | `44.183ms` |
| `BlockSystemNoProfilerComponentBody` | `29.749ms` |
| `BlockSystemWrapperProfilerNoBody` | `16.674ms` |
| `BlockSystemComponentIterationOnly` | `0.928ms` |

Changes:

- Removed per-component profiler marker creation and `Begin`/`End` calls from `BlockSystem.Update()`.
- Removed `ProfilerMarkerCreator`.
- Removed small runtime profiler markers from `GameUpdater.Update()` and `ServerGameUpdater.StartUpdate()`.
- Updated investigation tests so removed profiler paths are not reported as current behavior.

Result:

| Metric | Before | After |
| --- | ---: | ---: |
| `DirectBlockSystemTick` average | `37.989ms` | `31.750ms` |
| `BlockSystemUpdate` average | old profiler run `44.183ms` | `33.287ms` |
| `BlockSystemIterationOnlyTick` | about `0.928ms` | `0.926ms` |

Interpretation:

- The pure iteration floor did not change.
- The removed cost was profiler marker/wrapper work, not gameplay logic.
- Exact delta varies because component bodies mutate state and produce spikes.

## Gear Network Deep Dive

Initial `GearNetworkDatastore` average:

| Metric | Value |
| --- | ---: |
| `GearNetworkDatastore` average | `15.510ms/tick` |

The heavy network was index `2`.

| Network | Transformers | Generators | Direct avg | Direct P50 | Direct P95 | Direct P99 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `2` | `5,255` | `45` | `19.934ms` | `19.082ms` | `23.818ms` | `39.581ms` |
| `3` | `198` | `2` | `0.439ms` | `0.413ms` | `0.591ms` | `0.747ms` |
| `5` | `133` | `0` | `0.026ms` | `0.025ms` | `0.029ms` | `0.058ms` |

Network `2` phase attribution:

| Phase | Average | P50 | P95 | P99 | Meaning |
| --- | ---: | ---: | ---: | ---: | --- |
| `ProbeTotal` | `14.683ms` | `13.515ms` | `20.803ms` | `34.282ms` | Phase ratio only. |
| `PropagateRotation` | `8.659ms` | `7.779ms` | `11.741ms` | `22.522ms` | DFS and connection lookup. |
| `DistributeGearPower` | `5.295ms` | `5.094ms` | `7.154ms` | `8.479ms` | Power sum and `SupplyPower()`. |
| `EnergyBalance` | `0.669ms` | `0.592ms` | `1.152ms` | `1.430ms` | First power sum. |
| `SelectGenerator` | `0.002ms` | `0.002ms` | `0.003ms` | `0.015ms` | Not relevant. |

Network `2` work counters per tick:

- Visited gears: `5,300`
- Required torque calls: `5,300`
- `GetGearConnects()` calls: `5,300`
- Gear connect edges returned: `10,684`
- Energy balance transformer iterations: `5,255`
- Energy balance generator iterations: `45`
- Distribute transformer iterations: `5,255`
- Distribute generator iterations: `45`
- Supply calls: `5,255` transformer + `45` generator

Operation micro-costs:

| Operation | Average | P50 | P95 | P99 | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| `GetConnectsOnly` | `3.837ms` | `3.079ms` | `5.543ms` | `16.232ms` | High variance; `5,300` gears. |
| `SupplyPowerOnly` | `2.179ms` | `2.050ms` | `2.823ms` | `3.469ms` | `5,300` supply calls. |
| `RequiredTorqueOnly` | `0.469ms` | `0.437ms` | `0.650ms` | `0.822ms` | Not the main bottleneck. |

Gear conclusion:

- Generator selection and required torque calculation were not the main cost.
- The main cost was repeated traversal of a stable `5,300` gear connected component.
- Repeated `GetGearConnects()` list allocation/enumeration and per-gear `SupplyPower()` event emission were strong suspects.

## Gear Network Optimizations

Stable topology cache changes:

- Cached gear network topology.
- Reused topology when the network did not change.
- Added stable-skip path so unchanged gear networks avoid full per-tick traversal.
- Allowed network-change work to be heavier.

Stable network result:

| Metric | Before | After |
| --- | ---: | ---: |
| `GearNetworkDatastore` after topology cache | `4.151ms/tick` | `0.043ms/tick` |
| Large network index `2` | `6.068ms/tick` | `0.006ms/tick` |
| Initial baseline comparison | `15.510ms/tick` | about `0.043-0.045ms/tick` |

Long-run confirmation:

| Metric | Average | P99 | Max |
| --- | ---: | ---: | ---: |
| `LongRunGearNetworkDatastore` | `0.045ms` | `0.139ms` | `0.479ms` |
| `LongRunLargestGearNetwork` | `0.005ms` | `0.009ms` | `0.129ms` |

Balanced RPM experiment:

- Generator available power: `sum(GenerateRpm * GenerateTorque)`
- Consumer required power: `sum(GetRequiredTorque(rpm) * rpm)`
- Root RPM is binary-searched in `[0, 1,000,000]`
- The selected RPM is the highest root RPM whose required power is less than or equal to available generator power.

Balanced RPM stable result:

| Baseline | Average |
| --- | ---: |
| Original `GearNetworkDatastore` baseline | `15.510ms/tick` |
| Stable topology cache / skip previous result | `0.043-0.045ms/tick` |
| Balanced RPM experiment final result | `0.012ms/tick` |

Balanced RPM topology-mutation result before follow-up cache:

| Metric | Average | P50 | P95 | P99 | Max |
| --- | ---: | ---: | ---: | ---: | ---: |
| `AddGeneratorToLargestNetwork` | `0.023ms` | `0.018ms` | `0.026ms` | `0.252ms` | `0.489ms` |
| `ManualUpdateAfterGeneratorAdd` | `53.631ms` | `51.761ms` | `63.622ms` | `101.091ms` | `121.600ms` |
| `CombinedAddAndManualUpdate` | `53.653ms` | `51.781ms` | `63.639ms` | `101.108ms` | `121.619ms` |

Follow-up gear cache changes:

- Replaced generator snapshot comparison with explicit dirty flags.
- Generator components now dirty their network only when generated RPM/torque/fulfillment actually changes.
- Added incremental topology add for a new gear connected to an already-built network.
- Added demand cache grouping by `(consumption profile, RPM ratio)`.
- Reused demand cache to supply transformers.
- Removed unused `GearNetworkSupplyInfo` intermediate list path.
- Suppressed repeated `SupplyPower` / `StopNetwork` events when state did not change.

Follow-up stable result:

| Metric | Average | P50 | P95 | P99 | Max |
| --- | ---: | ---: | ---: | ---: | ---: |
| `GearNetworkDatastore` | `0.003ms/tick` | `0.001ms` | `0.001ms` | `0.004ms` | `0.492ms` |

Follow-up topology mutation:

| Metric | Previous | Current | Improvement |
| --- | ---: | ---: | ---: |
| `AddGeneratorToLargestNetwork` average | `0.023ms` | `0.013ms` | about `1.8x` |
| `ManualUpdateAfterGeneratorAdd` average | `53.631ms` | `1.758ms` | about `30.5x` |
| `CombinedAddAndManualUpdate` average | `53.653ms` | `1.771ms` | about `30.3x` |

Latest topology mutation distribution:

| Metric | Average | P50 | P95 | P99 | Max |
| --- | ---: | ---: | ---: | ---: | ---: |
| `AddGeneratorToLargestNetwork` | `0.013ms` | `0.007ms` | `0.023ms` | `0.035ms` | `0.493ms` |
| `ManualUpdateAfterGeneratorAdd` | `1.758ms` | `1.462ms` | `2.434ms` | `4.686ms` | `33.705ms` |
| `CombinedAddAndManualUpdate` | `1.771ms` | `1.471ms` | `2.456ms` | `4.722ms` | `33.711ms` |

Gear verification:

- Compile: success.
- Gear-related tests: `59` run, `45` passed, `14` failed with old fixed-RPM/hard-stop expectations.
- Measurement tests: passed.

Important:

- The remaining gear test failures are mostly expected incompatibilities with the balanced RPM experiment.
- If the balanced RPM model is kept, those tests need spec migration.

## Chest Transfer Optimization

Current-save chest shape:

| Metric | Value |
| --- | ---: |
| Chests | `41` |
| Total chest slots | `750` |
| Non-empty chest slots | `398` |

Changes:

- Added source-side non-empty slot index for indexed inventories.
- Added destination-side insertability cache through `HasInsertableSlot` / `CanInsertItem`.
- Added chest-to-chest fast insertion through `IBlockInventoryFastInsertTarget.InsertItemFast`.
- Scoped the slot index to chest inventories only.
- Added adaptive chest update path: small chests below `64` slots use sequential scan; large chests use non-empty slot index.
- Removed per-item destination precheck from `VanillaChestComponent.Update()` because it added extra target-array scans and did not help the current save.

Current-save result:

| Measurement | Before / baseline | After | Interpretation |
| --- | ---: | ---: | --- |
| `ChestTransferFullSlotScan` vs `ChestTransferCurrentUpdate` | `5.734ms` | `5.548ms` | about `3.2%` faster in same-process current-code comparison |
| `VanillaChestComponent` actual-order breakdown | `58.214ms / 410 calls` | `55.325ms / 410 calls` | about `5.0%` faster than old worktree run |
| `VanillaChestComponent` per call | `141.986us` | `134.940us` | small but measurable |

Large-slot synthetic result:

| Scenario | Before | After | Improvement |
| --- | ---: | ---: | ---: |
| Source scan, `65,536` slots and `16` non-empty | `1604.010us` | `0.047us` | about `34,128x` |
| Full destination lock check, `65,536` slots | `7887.029us` | `0.052us` | about `151,674x` |
| Chest-to-chest insert, last slot empty in `65,536` slots | `95393.941us` | `3.386us` | about `28,177x` |

Chest conclusion:

- Current save has too few chest slots for a large full-tick win.
- The algorithmic improvement is real for huge inventories.
- It matters when inventories become very large or many chest outputs are blocked by full destinations.

## Non-Gear Follow-Up

Measured on `2026-06-19` with the same current save.

Changes:

- Cached inventory connector target arrays for belt/chest output services instead of rebuilding `ConnectedTargets.ToArray()`.
- Split `FluidPipeComponent` update logic into a transfer service and cached fluid connector targets plus per-connection flow amount.
- Removed `FluidPipeComponent.Update()` allocations from `Keys.ToList()` and per-bucket eligible-target `List` creation.
- Added `IBlockInventoryInsertableTargetState` so belts can answer destination insertability without context-less fast insertion.
- Split `VanillaBeltConveyorComponent` save/update helpers so touched files remain below 200 lines.

100x amplified component body comparison:

| Measurement | Before this follow-up | After | Improvement |
| --- | ---: | ---: | ---: |
| `VanillaBeltConveyorComponent` amplified | `681.036ms / 430,700 calls`, `1.581us/call` | `552.840ms / 430,700 calls`, `1.284us/call` | about `18.8%` |
| `FluidPipeComponent` amplified | `460.836ms / 14,300 calls`, `32.226us/call` | `336.244ms / 14,300 calls`, `23.514us/call` | about `27.0%` |
| `VanillaChestComponent` amplified | `444.641ms / 4,100 calls`, `108.449us/call` | `414.392ms / 4,100 calls`, `101.071us/call` | about `6.8%` |

Latest block-type and full-tick result:

| Measurement | Current value |
| --- | ---: |
| Full tick average, 300 ticks | `28.065ms` |
| Full tick P50 | `26.329ms` |
| Full tick P95 | `38.349ms` |
| `GearBeltConveyor` block type | `9.027ms/tick` |
| `Chest` block type | `4.731ms/tick` |
| `FluidPipe` block type | `2.451ms/tick` |

Non-gear verification:

```powershell
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\.(CombinedTest\.Core\.(FluidTest|ElectricPumpTest|MachineFluidIOTest|BeltConveyorTest|ChestLogicTest|InsertItemContextTest)|UnitTest\.Core\.Other\.(ConnectingInventoryListPriorityInsertItemServiceTest|VanillaBeltConveyorBlockInventoryInserterRoundRobinTest)|UnitTest\.Game\.SaveLoad\.FluidPipeSaveLoadTest)"
```

Result:

- Compile: success, `0` errors.
- Target tests: `39/39` passed.

## Latest Full Game Update

Latest measured full tick after current optimizations:

| Metric | Value |
| --- | ---: |
| Count | `300` |
| Average | `28.065ms` |
| Min | `20.050ms` |
| P50 | `26.329ms` |
| P90 | `32.225ms` |
| P95 | `38.349ms` |
| P99 | `84.684ms` |
| Max | `115.705ms` |
| StdDev | `8.838ms` |

Comparison with original baseline:

| Metric | Original baseline | Latest |
| --- | ---: | ---: |
| Average | `59.463ms` | `28.065ms` |
| P50 | `51.722ms` | `26.329ms` |
| P95 | `114.282ms` | `38.349ms` |
| P99 | `138.591ms` | `84.684ms` |
| Max | `157.895ms` | `115.705ms` |

Conclusion:

- Average tick is now comfortably below `50ms`.
- P95 is also below `50ms` in the latest run.
- P99 and max spikes still exist and should be investigated separately.

## Remaining Work

High-value next targets if optimization restarts from a clean branch:

- Re-apply startup coordinate index and initial-load event suppression first; this is the largest deterministic win.
- Re-apply gear stable-skip/topology cache next; stable networks should not DFS every tick.
- Remove runtime profiler marker creation from per-component runtime loops.
- Re-apply chest slot index only where inventory size justifies it.
- Re-apply connector target caches for belt/chest output.
- Re-apply FluidPipe transfer cache if fluid pipe remains visible in amplified component measurements.
- Investigate state event batching, especially fluid pipe updates.
- Investigate remaining P99/max spikes separately from average tick improvements.

Do not use only one full-tick run as proof for small changes. Use repeated current-save runs plus amplified component measurements.
