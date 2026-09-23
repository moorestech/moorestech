# Belt Segment benchmark

This .NET 8 console benchmark compiles the same `Game.BeltSegment` source files used by Unity. It requires no external packages.

```sh
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 1000 64 2000 200 serial
dotnet run --project tools/BeltSegment/Benchmark/BeltSegment.Benchmark.csproj -c Release -- 1000 64 2000 200 parallel
```

The five required arguments are segment count, capacity, measured ticks, warmup ticks, and `serial` or `parallel`. Segment count, capacity, and measured ticks must be positive; warmup may be zero. Capacity cannot exceed the Core limit. Each normal segment runs at speed 32, starts with one item every four cells, and has its own one-item sink. Successful outputs are returned to the same segment at the next tick boundary, preserving their exit distance. Warmup and measurement use separate, identically configured scenarios.

The JSON reports elapsed time and allocations for **Tick plus reinsertion** (`measurementScope: "tick-and-reinsertion"`). Setup, warmup, and final item validation are outside the measured interval. The process exits with a nonzero code if item count or GUID conservation fails. These numbers describe the isolated Core loop; they do not measure difference notifications, GPU work, or game integration.
