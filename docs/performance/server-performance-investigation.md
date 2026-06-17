# Server Performance Investigation

## 2026-06-17 Startup Load

Target save:

- `C:\Users\5080\AppData\Roaming\.moorestech\Saves\save_1.json`
- Size: `2,710,922` bytes
- World blocks: `6,302`
- Mod directory: `C:\Users\5080\Documents\GitHub\moorestech_master\server_v8\`

Measured command:

```powershell
uloop run-tests --project-path ./moorestech_server --filter-type exact --filter-value "Tests.Investigation.StartupPerformanceInvestigationTest.ProfileCurrentSaveStartupLoad"
```

Before optimization:

- `LoadOrInitialize`: about `100.7s`
- Dominant cost: `WorldBlockDatastore.TryAddBlock`
- Root cause: every block placement event scanned every coordinate-specific subscriber, then checked `BlockPositionInfo.IsContainPos`.
- Scale observed during startup: thousands of block placements multiplied by tens of thousands of `BlockConnectorComponent` subscriptions.

Applied optimization:

- Added `BlockCoordinateIndex` to map every occupied coordinate to a `BlockInstanceId`.
- Replaced overlap and `GetBlock(pos)` scans with coordinate-index lookups.
- Replaced coordinate-specific block place/remove subscriptions with dictionary dispatch by occupied coordinate.
- Preserved global `OnBlockPlaceEvent`/`OnBlockRemoveEvent` for internal systems.
- Marked load-time placement events with `BlockPlaceProperties.IsInitialLoad`.
- Suppressed initial-load block placement broadcasts in `PlaceBlockEventPacket`.

After optimization:

- `LoadOrInitialize`: `1.396s`
- `BlockFactoryLoad`: `771.882ms`
- `TryAddBlock`: `318.645ms`
- `PostBlockLoad`: `30.107ms`
- Unity Console Error: `0`

Verification:

```powershell
uloop compile --project-path ./moorestech_server --wait-for-domain-reload true
uloop run-tests --project-path ./moorestech_server --filter-type regex --filter-value "(MultiSizeBlockTest|WorldInstallationDatastoreTest|BlockPlaceEventPacketTest|GetWorldDataProtocolTest|PlaceHotBarBlockProtocolTest|BlockRemoverTest|ConnectElectricSegmentTest|DisconnectElectricSegmentTest|GearChainPoleSaveLoadTest|FluidMachineSaveLoadTest|MachineSaveLoadTest)"
uloop get-logs --project-path ./moorestech_server --log-type Error --max-count 100
```

Results:

- Compile: success, `ErrorCount=0`
- Regression tests: `24/24` passed
- Console errors: `0`

Conclusion:

- Startup optimization is complete for this investigation pass.
- The previous startup bottleneck is no longer block placement subscriber dispatch.
- Remaining startup cost is mainly block template restoration inside `BlockFactoryLoad`.

## 2026-06-17 Game Update Baseline

Purpose:

- Do not optimize game update yet.
- Establish a repeatable baseline for the current normal save.
- Identify which parts of `GameUpdater.Update()` are actually heavy.
- Treat microsecond-level measurements as noisy unless they are aggregated or amplified.

Target save and world:

- `C:\Users\5080\AppData\Roaming\.moorestech\Saves\save_1.json`
- Size: `2,710,922` bytes
- Blocks: `6,302`
- Updatable block components: `14,571`
- Energy segments: `1`
- Gear networks: `6`
- Trains: `1`

Measurement entry point:

```powershell
uloop run-tests --project-path ./moorestech_server --filter-type regex --filter-value "Tests\.Investigation\.(GameUpdatePerformanceInvestigationTest|GameUpdateDispatchOverheadInvestigationTest|GameUpdateSubscriberBreakdownInvestigationTest)\..*"
```

Method:

- Load the current save through `MoorestechServerDIContainerGenerator` and `IWorldSaveDataLoader.LoadOrInitialize()`.
- Run `100` warmup ticks before each measurement.
- Measure full tick distribution for `300` ticks.
- Measure `Update` and `LateUpdate` phases separately for `200` ticks.
- Measure top-level subscribers separately for `200` samples.
- Measure block components both in actual tick order and with type-grouped `100x` amplification.

Baseline acceptance rule for later optimization:

- Primary metric is `FullTick` after `100` warmup ticks, using `avg`, `p50`, `p95`, `p99`, and `50` tick windows.
- A change is not convincing if only `max` improves or if only one run improves by less than the observed window noise.
- In this run, `50` tick window averages ranged from `56.586ms` to `66.904ms`, so small changes need repeated runs.
- Microsecond-level component numbers are only trusted when they are consistent between actual-order and amplified measurements.
- Sub-microsecond components measured one call at a time are considered measurement-overhead dominated.

Full tick distribution:

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

50 tick windows:

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

Top-level findings:

| Target | Average | P50 | P95 | P99 | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| `DirectBlockSystemTick` | `37.989ms` | `33.107ms` | `66.639ms` | `102.354ms` | Directly updates all `6,302` block systems. |
| `BlockSystemWrapperOnlyTick` | `14.441ms` | `12.820ms` | `20.322ms` | `62.036ms` | Excludes `component.Update()` body; mostly per-block/per-component wrapper and profiler-marker path. |
| `GearNetworkDatastore` | `15.510ms` | `14.160ms` | `20.685ms` | `42.642ms` | Updates all gear networks. |
| `MachineOutputInventories` | `0.851ms` | `0.806ms` | `1.462ms` | `1.935ms` | Small compared with block and gear network paths. |
| `EnergySegments` | `0.029ms` | `0.024ms` | `0.041ms` | `0.075ms` | Negligible at this scale. |
| `TrainUpdateService` | `0.011ms` | `0.009ms` | `0.013ms` | `0.030ms` | Negligible at this scale. |
| `ChallengeDatastore` | `0.005ms` | `0.003ms` | `0.006ms` | `0.017ms` | Negligible at this scale. |
| `EmptySubscriberFullTick` | `0.081ms` | `0.077ms` | `0.091ms` | `0.180ms` | `6,302` empty subscribers; UniRx dispatch itself is not the bottleneck. |

Gear network detail:

| Network | Transformers | Generators | Average | P50 | P95 | P99 | Max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `2` | `5,255` | `45` | `19.016ms` | `15.165ms` | `39.024ms` | `53.004ms` | `76.399ms` |
| `3` | `198` | `2` | `0.864ms` | `0.531ms` | `1.187ms` | `1.904ms` | `47.737ms` |
| `5` | `133` | `0` | `0.029ms` | `0.025ms` | `0.038ms` | `0.047ms` | `0.081ms` |

Code reason:

- `GearNetworkDatastore.Update()` calls `ManualUpdate()` for every network each tick.
- `GearNetwork.ManualUpdate()` picks the fastest generator, recursively walks connected gears, computes energy balance, then distributes gear power.
- The large network with `5,255` transformers and `45` generators dominates gear update cost.
- The detail run and top-level run differ in absolute average because they are independent noisy runs, but both point to the same large network.

Block component detail:

| Component | Count | Actual-order ms/tick | 100x amplified ms/tick | Confidence |
| --- | ---: | ---: | ---: | --- |
| `VanillaBeltConveyorComponent` | `4,307` | `7.836ms` | `6.321ms` | High; heavy in both methods. |
| `VanillaChestComponent` | `41` | `5.623ms` | `5.267ms` | High; few instances but high per-call cost. |
| `FluidPipeComponent` | `143` | `3.026ms` | `2.448ms` | High; dictionary/list work and fluid distribution are visible. |
| `VanillaMachineProcessorComponent` | `200` | `1.794ms` | `2.386ms` | High enough; state changes and event emission make it noisy. |
| `FuelGearGeneratorComponent` | `47` | `0.622ms` | `0.345ms` | Medium; visible but much smaller. |
| `VanillaMinerProcessorComponent` | `26` | `0.301ms` | `0.188ms` | Medium; visible but much smaller. |
| `GearBeltConveyorComponent` | `4,307` | `0.838ms` | `0.030ms` | Low; actual-order one-call timing is dominated by measurement overhead. |
| `GearOverloadBreakageComponent` | `5,423` | `0.708ms` | `0.048ms` | Low; actual-order one-call timing is dominated by measurement overhead. |

Measurement caveat:

- `ComponentActualOrder` wraps each individual `component.Update()` call with `Stopwatch`.
- That keeps real tick order, but it overstates very cheap components because the measurement cost is paid thousands of times.
- `ComponentAmplified` wraps a whole type group and repeats it `100` times, which reduces timer overhead.
- The amplified path changes cache locality and mutates component state in a grouped order, so it is for attribution, not an exact total.

Current conclusion:

- The current normal save is above the `50ms` budget for `20 TPS`: average `59.463ms`, P95 `114.282ms`.
- `LateUpdate` is effectively irrelevant; almost all cost is in `Update`.
- `GameUpdater` and UniRx dispatch are not the heavy part.
- The dominant areas are `BlockSystem` and `GearNetworkDatastore`.
- Inside `BlockSystem`, there is both a large wrapper/profiler-marker cost and real component body cost.
- The highest-confidence component hotspots are belt conveyor, chest, fluid pipe, and machine processor.
- No game-update optimization has been applied in this pass; only investigation probes and repeatable benchmarks were added.

Deep dive:

- `docs/performance/game-update-deep-dive-2026-06-17.md`
