# Belt Segment benchmark

This .NET 8 console benchmark compiles the same `Game.BeltSegment` source files used by Unity. It requires no external packages.

```sh
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 1000 64 2000 200 serial
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 1000 64 2000 200 parallel
```

The five required arguments are segment count, capacity, measured ticks, warmup ticks, and `serial`, `parallel`, or `replay-packing`. The following description covers the two Core modes. Segment count, capacity, and measured ticks must be positive; warmup may be zero. Capacity cannot exceed the Core limit. Each normal segment runs at speed 32, starts with one item every four cells, and has its own one-item sink. Successful outputs are returned to the same segment at the next tick boundary, preserving their exit distance. Warmup and measurement use separate, identically configured scenarios.

The JSON reports elapsed time and allocations for **Tick plus reinsertion** (`measurementScope: "tick-and-reinsertion"`). Setup, warmup, and final item validation are outside the measured interval. The process exits with a nonzero code if item count or GUID conservation fails. These numbers describe the isolated Core loop; they do not measure difference notifications, GPU work, or game integration.

## Replay and GPU upload preparation

```sh
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 129 64 10000 1000 replay-packing
```

This mode records a fixed workload, then measures the production CPU `BeltReplaySimulation` and the actual `GpuBeltTickUpload` ABI packing separately. Setup, recording, warmup and final parity checks are outside the measured intervals. JSON includes initial/final item counts, input/output event counts, CPU time and allocated bytes for each stage, and `gpuUploadBytes` (event count × ABI stride). It exits nonzero if replay parity fails.

GPU upload bytes are not MessagePack wire bytes. The same `BeltRecordedWorkload` source is compiled by this benchmark and Unity's focused production-serializer measurement:

```sh
uloop run-tests --project-path moorestech_client --test-mode EditMode --filter-type class --filter-value BeltRecordedWireMeasurementTest
```

The Unity fixture uses 129 segments × 64 capacity, 10,000 ticks and 1,000 warmup iterations. It serializes the initial `BeltWorldSnapshotMessagePack` and every `BeltWorldFrameMessagePack`, including generation/tick/sequence/prior-state hash, then decodes and replays the complete stream to verify event counts and parity. Snapshot and accumulated frame bytes are reported separately. This is the shared Core workload with null item Position metadata; transport envelopes are excluded. The earlier one-segment serializer check remains a separate microbenchmark. Neither mode measures GPU execution. See [gameplay validation](../../docs/belt-segment-gameplay-validation.md) for exact results, allocation-counter limits and raw artifacts.

## Normal connections

`BeltSimulation` extracts Normal-to-Normal connections, including self-connections, when it is constructed. Rebuild the simulation after wiring changes and supply every updated segment exactly once. Each Normal segment must have at most one input source.

At the start of each tick, before phase 0, the simulation records the actual entrance space for every Normal-to-Normal edge. Phase 4 stages successful transfers against that fixed space and advances each Normal queue once. Incoming items are committed after all Normal updates finish, so they cannot advance again during their arrival tick. Space created by movement or output in this tick becomes available to these transfers in the following tick. Full cycles stop, including full self-connections (D12).

The original phases 0–4 remain: freeze speeds, collect buffers, reserve merge inputs, transfer buffers, and advance Normal segments. Buffer-to-Normal arrivals occur in phase 3 and still advance in phase 4; Normal-to-Normal arrivals occur after phase 4. Normal output to merges and external machines keeps its existing phase-4 path.

Deterministic cycle cuts and their save/restore mapping belong to World-to-segment construction. This Core accepts completed self-connections; it does not construct World cycles. The benchmark above uses external sinks and therefore measures the existing output path, not Normal-to-Normal transfer performance.

## Current client replay scope — 2026-09-24

The `replay-packing` mode above retains the historical `ApplyTick` path and same-tick GUID recycling workload; its allocation results do not cover the current `TryApplyTick` validation entry. Run `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'BeltRecordedWireMeasurementTest' --unsaved-changes fail --timeout-seconds 1200` for the fresh-ingress production check. It preserves topology, event timing/counts and replay parity while assigning a new deterministic ingress GUID, as the server does. The measured interval includes prior-state hashing, live/same-frame GUID checks and CPU replay; construction, workload/oracle setup, DTO validation/deserialization and GPU execution are excluded. Two uncontrolled Editor repetitions were 4100.7293/1837.1325 ms for 10,000 ticks; no cross-runtime speedup ratio is implied. The Unity allocation probe is unavailable, so no zero-allocation claim applies to this path. Wire byte figures remain the historical MessagePack DTO scope, excluding transport envelopes. See the dated [gameplay validation update](../../docs/belt-segment-gameplay-validation.md#final-review-update--2026-09-24) for the exact test sequence, normalization observations and unresolved C03/D01 plus two Refix Warnings.
