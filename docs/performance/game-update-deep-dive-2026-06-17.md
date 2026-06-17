# Game Update Deep Dive 2026-06-17

Scope:

- Current save: `C:\Users\5080\AppData\Roaming\.moorestech\Saves\save_1.json`
- Focus: `BlockSystem` average `37.989ms` and `GearNetworkDatastore` average `15.510ms`
- Goal: investigation only; no game-update optimization applied

Commands:

```powershell
uloop run-tests --project-path ./moorestech_server --filter-type regex --filter-value "Tests\.Investigation\.BlockSystemDeepDiveInvestigationTest\..*"
uloop run-tests --project-path ./moorestech_server --filter-type regex --filter-value "Tests\.Investigation\.GearNetworkDeepDiveInvestigationTest\..*"
```

## BlockSystem

Measured all `6,302` block systems after `100` warmup ticks.

| Path | Average | P50 | P95 | P99 | Interpretation |
| --- | ---: | ---: | ---: | ---: | --- |
| `BlockSystemFullWithProfiler` | `39.236ms` | `34.789ms` | `65.893ms` | `113.896ms` | Current direct BlockSystem path. |
| `BlockSystemNoProfilerComponentBody` | `26.812ms` | `23.837ms` | `39.775ms` | `77.235ms` | Component bodies without per-component profiler marker. |
| `BlockSystemWrapperProfilerNoBody` | `13.874ms` | `12.898ms` | `17.873ms` | `57.671ms` | Profiler/wrapper path without `component.Update()`. |
| `BlockSystemBlockProfilerOnly` | `0.388ms` | `0.340ms` | `0.523ms` | `0.916ms` | Per-block outer markers only. |
| `BlockSystemComponentIterationOnly` | `0.740ms` | `0.631ms` | `1.015ms` | `1.322ms` | Component list iteration only. |

Conclusion:

- About `26.8ms` is real component body work.
- About `12.7ms` is per-component profiler marker/wrapper cost: `13.874 - 0.388 - 0.740`.
- Plain UniRx dispatch was already measured at about `0.081ms`, so it is not the cause.
- `ProfilerMarkerCreator.CreateComponentUpdateMarker()` creates a marker with string interpolation under `ENABLE_PROFILER`, which matches the measured wrapper cost.

Code evidence:

- `BlockSystem.Update()` creates a component marker for every updatable component every tick.
- `ProfilerMarkerCreator.CreateComponentUpdateMarker()` returns `new ProfilerMarker($"{blockMasterElement.Name}_{component.GetType().Name}")` under `ENABLE_PROFILER`.
- Current save has `14,571` updatable block components, so this path is paid `14,571` times per tick.

Block type amplified ranking:

| Block type | Blocks | Components | Full ms/tick | No-profiler ms/tick | Wrapper ms/tick | Notes |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `GearBeltConveyor` | `4,307` | `12,921` | `23.637` | `9.239` | `10.565` | Dominates wrapper cost and a large part of component body cost. |
| `Chest` | `41` | `41` | `6.068` | `6.795` | `0.040` | Mutation-sensitive run; use with component breakdown, not as exact total. |
| `FluidPipe` | `143` | `143` | `6.241` | `2.524` | `0.139` | Body cost is real; full path is noisy in amplified order. |
| `GearMachine` | `115` | `115` | `0.924` | `0.732` | `0.115` | Smaller but visible. |
| `ElectricMachine` | `85` | `88` | `0.546` | `0.467` | `0.057` | Smaller but visible. |
| `FuelGearGenerator` | `47` | `94` | `0.493` | `0.371` | `0.094` | Smaller but visible. |

Caveat:

- Block type amplified measurement mutates block state repeatedly and changes cache/order behavior.
- Use it for ranking and cross-checking, not as an exact decomposition of the full tick.
- The reliable conclusion is the aggregate split: component bodies around `24.5ms`, marker/wrapper around `14.4ms`.

## GearNetworkDatastore

The heavy network is index `2`.

The direct value for network `2` moved between about `15ms` and `20ms` across runs, so use repeated direct measurements for pass/fail comparisons. The replica probe is for phase attribution.

| Network | Transformers | Generators | Direct avg | Direct P50 | Direct P95 | Direct P99 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `2` | `5,255` | `45` | `19.934ms` | `19.082ms` | `23.818ms` | `39.581ms` |
| `3` | `198` | `2` | `0.439ms` | `0.413ms` | `0.591ms` | `0.747ms` |
| `5` | `133` | `0` | `0.026ms` | `0.025ms` | `0.029ms` | `0.058ms` |

Replica phase validation for network `2`:

| Phase | Average | P50 | P95 | P99 | Meaning |
| --- | ---: | ---: | ---: | ---: | --- |
| `ProbeTotal` | `14.683ms` | `13.515ms` | `20.803ms` | `34.282ms` | Lower than direct in this run; useful for phase ratio, not absolute truth. |
| `PropagateRotation` | `8.659ms` | `7.779ms` | `11.741ms` | `22.522ms` | DFS from fastest generator, including `GetGearConnects()` and rotation validation. |
| `DistributeGearPower` | `5.295ms` | `5.094ms` | `7.154ms` | `8.479ms` | Re-sums power and calls `SupplyPower()` for all gears. |
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

Operation micro-costs for network `2`:

| Operation | Average | P50 | P95 | P99 | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| `GetConnectsOnly` | `3.837ms` | `3.079ms` | `5.543ms` | `16.232ms` | High variance; allocates/returns connection lists for `5,300` gears. |
| `SupplyPowerOnly` | `2.179ms` | `2.050ms` | `2.823ms` | `3.469ms` | Calls `SupplyPower()` for `5,300` gears. |
| `RequiredTorqueOnly` | `0.469ms` | `0.437ms` | `0.650ms` | `0.822ms` | Not the main bottleneck. |

Code evidence:

- `GearNetwork.ManualUpdate()` recursively calls `GetGearConnects()` during propagation.
- `GearEnergyTransformer.GetGearConnects()` creates a new `List<GearConnect>` every call.
- `SimpleGearService.SupplyPower()` always invokes `_onGearUpdate.OnNext(GearUpdateType.SupplyPower)`, even when RPM/torque/direction are unchanged.
- `DistributeGearPower()` repeats total required/generated power summation after `CalculateEnergyBalance()` already did a similar pass.

Conclusion:

- The GearNetwork cost is not generator selection or required torque calculation.
- The main cost is the huge per-tick traversal of a `5,300` gear connected component.
- The strongest suspects are repeated `GetGearConnects()` list allocation/enumeration and per-gear `SupplyPower()` event emission.
- The large network is structurally stable, so any future optimization should compare direct `GearNetworkDirect index=2` and full `GameUpdate` metrics, not only phase-probe totals.
