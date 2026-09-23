# Belt Segment benchmark

This .NET 8 console benchmark compiles the same `Game.BeltSegment` source files used by Unity. It requires no external packages.

```sh
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 1000 64 2000 200 serial
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 1000 64 2000 200 parallel
```

The five required arguments are segment count, capacity, measured ticks, warmup ticks, and `serial` or `parallel`. Segment count, capacity, and measured ticks must be positive; warmup may be zero. Capacity cannot exceed the Core limit. Each normal segment runs at speed 32, starts with one item every four cells, and has its own one-item sink. Successful outputs are returned to the same segment at the next tick boundary, preserving their exit distance. Warmup and measurement use separate, identically configured scenarios.

The JSON reports elapsed time and allocations for **Tick plus reinsertion** (`measurementScope: "tick-and-reinsertion"`). Setup, warmup, and final item validation are outside the measured interval. The process exits with a nonzero code if item count or GUID conservation fails. These numbers describe the isolated Core loop; they do not measure difference notifications, GPU work, or game integration.

## Normal connections

`BeltSimulation` extracts Normal-to-Normal connections, including self-connections, when it is constructed. Rebuild the simulation after wiring changes and supply every updated segment exactly once. Each Normal segment must have at most one input source.

At the start of each tick, before phase 0, the simulation records the actual entrance space for every Normal-to-Normal edge. Phase 4 stages successful transfers against that fixed space and advances each Normal queue once. Incoming items are committed after all Normal updates finish, so they cannot advance again during their arrival tick. Space created by movement or output in this tick becomes available to these transfers in the following tick. Full cycles stop, including full self-connections (D12).

The original phases 0–4 remain: freeze speeds, collect buffers, reserve merge inputs, transfer buffers, and advance Normal segments. Buffer-to-Normal arrivals occur in phase 3 and still advance in phase 4; Normal-to-Normal arrivals occur after phase 4. Normal output to merges and external machines keeps its existing phase-4 path.

Deterministic cycle cuts and their save/restore mapping belong to World-to-segment construction. This Core accepts completed self-connections; it does not construct World cycles. The benchmark above uses external sinks and therefore measures the existing output path, not Normal-to-Normal transfer performance.
