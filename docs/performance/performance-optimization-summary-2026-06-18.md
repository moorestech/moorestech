# Performance Optimization Summary 2026-06-18

## Scope

対象は現在の通常セーブデータです。

- Save: `C:\Users\5080\AppData\Roaming\.moorestech\Saves\save_1.json`
- Save size: `2,710,922` bytes
- Blocks: `6,302`
- Updatable block components: `14,712`
- Gear networks: `6`
- Trains: `1`

この文書は、ここまでに実際へ入れた高速化だけをまとめます。調査用テスト追加やグラフ作成だけの変更は、性能改善としては数えません。

## Summary

| Area | Before | After | Improvement | Notes |
| --- | ---: | ---: | ---: | --- |
| Startup `LoadOrInitialize` | `100.7s` | `1.396s` | about `72x` faster | Current save load path. |
| Gear update, initial baseline | `15.510ms/tick` | `0.043-0.045ms/tick` | about `345-360x` faster | Stable network case. |
| Largest gear network | `6.068ms/tick` | `0.005-0.006ms/tick` | about `1000x` faster | Stable network index `2`. |
| BlockSystem direct update | `37.989ms/tick` | `31.750ms/tick` | `6.239ms` faster, about `16%` | Runtime profiler marker removed. |
| Full `GameUpdater.Update` baseline | `59.463ms/tick` | `35.098ms/tick` | `24.365ms` faster, about `41%` | Cross-run current-save comparison. |

Important caveat:

- These are current-save investigation runs, not a formal benchmark suite with isolated machine load.
- The full tick still has large spikes. After the current changes, average is under the `50ms` budget for `20 TPS`, but P95 is still above budget.

## 1. Startup Load

### Bottleneck

Startup was dominated by block restoration:

- Every block placement event scanned many coordinate-specific subscribers.
- Coordinate lookups and overlap checks used broad scans.
- Initial block load emitted placement events that were not useful for the first client state sync.

### Changes

- Added coordinate-indexed block lookup through `BlockCoordinateIndex`.
- Replaced overlap and `GetBlock(pos)` scans with coordinate-index lookups.
- Replaced coordinate-specific block place/remove subscriber scans with dictionary dispatch.
- Added `BlockPlaceProperties.IsInitialLoad`.
- Suppressed initial-load block placement broadcasts in `PlaceBlockEventPacket`.

### Result

| Metric | Before | After |
| --- | ---: | ---: |
| `LoadOrInitialize` | about `100.7s` | `1.396s` |
| `BlockFactoryLoad` | not isolated in initial run | `771.882ms` |
| `TryAddBlock` | dominant startup cost | `318.645ms` |
| `PostBlockLoad` | not dominant | `30.107ms` |

Conclusion:

- The startup bottleneck from block placement subscriber dispatch is gone.
- Remaining startup cost is mainly block template restoration.

## 2. Gear Network

### Bottleneck

The heavy gear network was structurally stable but recomputed every tick:

- Large network index `2`: `5,255` transformers and `45` generators.
- Each tick traversed the connected component.
- It repeatedly called `GetGearConnects()`, summed required/generated power, and called `SupplyPower()` across the network.

Initial baseline:

| Metric | Value |
| --- | ---: |
| `GearNetworkDatastore` average | `15.510ms/tick` |
| Large network direct average, earlier run | `6.068ms/tick` |

### Changes

- Cached gear network topology.
- Reused network topology when the network did not change.
- Added a stable-skip path so unchanged gear networks avoid the full per-tick traversal.
- Kept network-change work allowed to be heavier; the game can pay rebuild cost when topology changes.

Relevant commits:

- `24b1bd06c Cache gear network topology`
- `7b28abba1 Skip stable gear network ticks`

### Result

Stable network case:

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

Residual cost:

- Machine/miner wrapper update remains around `1.33ms/tick` in the measured state.
- This is outside the gear network stable-skip path.

Condition:

- These numbers are for the network-unchanged case.
- If topology changes, the network is rebuilt and the update can be heavier for that tick.

## 3. Runtime Profiler Marker Removal

### Bottleneck

`BlockSystem.Update()` created profiler markers for every updatable component every tick:

- Updatable components: `14,712`
- Old path called `CreateComponentUpdateMarker(BlockMasterElement, component)` inside the component loop.
- Under `ENABLE_PROFILER`, it allocated a marker name like `BlockName_ComponentTypeName`.
- This did not advance gameplay. It only existed for Unity Profiler labeling.

Earlier decomposition showed the wrapper/profiler path alone was large:

| Path | Average |
| --- | ---: |
| `BlockSystemFullWithProfiler` | `44.183ms` |
| `BlockSystemNoProfilerComponentBody` | `29.749ms` |
| `BlockSystemWrapperProfilerNoBody` | `16.674ms` |
| `BlockSystemComponentIterationOnly` | `0.928ms` |

### Changes

- Removed per-component profiler marker creation and `Begin`/`End` calls from `BlockSystem.Update()`.
- Removed `ProfilerMarkerCreator`.
- Removed small runtime profiler markers from `GameUpdater.Update()` and `ServerGameUpdater.StartUpdate()`.
- Updated investigation tests so they no longer report removed profiler paths as current behavior.

Relevant commit:

- `256e20d8a Remove runtime profiler markers`

### Result

BlockSystem after removal:

| Metric | Before | After |
| --- | ---: | ---: |
| `DirectBlockSystemTick` average | `37.989ms` | `31.750ms` |
| `BlockSystemUpdate` average | old profiler run `44.183ms` | `33.287ms` |
| `BlockSystemIterationOnlyTick` | about `0.928ms` | `0.926ms` |

Interpretation:

- The pure iteration floor did not change, as expected.
- The removed cost was the profiler marker/wrapper path, not game logic.
- Exact delta varies between runs because component bodies mutate state and produce spikes, but the profiler marker path itself is no longer in runtime code.

## 4. Full Game Update After Current Optimizations

Measured after gear stable-skip and profiler marker removal:

```powershell
uloop run-tests --project-path ./moorestech_client --filter-type exact --filter-value "Tests.Investigation.GameUpdatePerformanceInvestigationTest.ProfileCurrentSaveSteadyStateTickDistribution"
```

Latest result:

| Metric | Value |
| --- | ---: |
| Count | `300` |
| Average | `35.098ms` |
| Min | `24.794ms` |
| P50 | `31.745ms` |
| P90 | `40.023ms` |
| P95 | `63.442ms` |
| P99 | `113.026ms` |
| Max | `116.788ms` |
| StdDev | `13.478ms` |

50 tick windows:

| Ticks | Average | P95 | Max |
| --- | ---: | ---: | ---: |
| `0-49` | `33.415ms` | `37.268ms` | `113.013ms` |
| `50-99` | `35.608ms` | `73.788ms` | `116.788ms` |
| `100-149` | `35.170ms` | `73.433ms` | `96.293ms` |
| `150-199` | `36.640ms` | `82.594ms` | `113.949ms` |
| `200-249` | `31.888ms` | `36.154ms` | `37.394ms` |
| `250-299` | `37.866ms` | `71.413ms` | `113.026ms` |

Comparison with the original game-update baseline:

| Metric | Original baseline | Current after optimizations |
| --- | ---: | ---: |
| Average | `59.463ms` | `35.098ms` |
| P50 | `51.722ms` | `31.745ms` |
| P95 | `114.282ms` | `63.442ms` |
| P99 | `138.591ms` | `113.026ms` |
| Max | `157.895ms` | `116.788ms` |

Conclusion:

- Average tick is now below `50ms`.
- P95 and P99 still show spikes, so the game update is not finished.
- The next useful target is still inside `BlockSystem` component body work and state event emission.

## 5. Remaining Suspects

These are not yet optimized in this pass:

- `VanillaBeltConveyorComponent`: high total cost due to `4,307` belts and `3,516` occupied belt slots.
- `VanillaChestComponent`: only `41` chests, but `750` total slots and `398` non-empty slots; each tick tries slot-wise output.
- `FluidPipeComponent`: `143` pipes, `203` source buckets, `304` connections; also emits many state events.
- State events: `FluidPipe` emitted `91,396` block state events over `200` ticks in the measured current save.

Current recommendation:

- Treat gear network as solved for stable networks.
- Do not spend more time on runtime profiler marker overhead; it has been removed.
- Continue with real `BlockSystem` component body work: chest output, belt output/inserter path, fluid pipe update, and state event batching.
