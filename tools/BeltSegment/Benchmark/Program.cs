using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BeltSegment.Benchmark;

internal static class Program
{
    private const string Usage = "Usage: <segmentCount> <capacity> <ticks> <warmup> <serial|parallel|replay-packing>";
    private static int Main(string[] args)
    {
        if (args.Length != 5 ||
            !TryPositive(args[0], out var segmentCount) ||
            !TryPositive(args[1], out var capacity) || Game.BeltSegment.BeltConstants.MaximumCapacity < capacity ||
            !TryPositive(args[2], out var ticks) ||
            !int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var warmup) || warmup < 0 ||
            (args[4] != "serial" && args[4] != "parallel" && args[4] != "replay-packing"))
        {
            Console.Error.WriteLine(Usage);
            Console.Error.WriteLine($"capacity must be at most {Game.BeltSegment.BeltConstants.MaximumCapacity}; warmup must be nonnegative.");
            return 2;
        }

        if (args[4] == "replay-packing") return BeltReplayBenchmark.Run(segmentCount, capacity, ticks, warmup);
        var parallel = args[4] == "parallel";

        // 同一構成の準備用と計測用を別生成。
        // Create matching warmup and measurement scenarios separately.
        var warmupScenario = new BeltBenchmarkScenario(segmentCount, capacity);
        for (var tick = 0; tick < warmup; tick++)
        {
            warmupScenario.ReinsertOutputs();
            warmupScenario.Simulation.Tick(parallel);
        }

        var scenario = new BeltBenchmarkScenario(segmentCount, capacity);
        var initialItemCount = scenario.InitialItemCount;
        // 計測器自体の割当を測定範囲から除く。
        // Exclude the timer object's allocation from the measured scope.
        var stopwatch = new Stopwatch();
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        stopwatch.Start();
        for (var tick = 0; tick < ticks; tick++)
        {
            // 搬出したアイテムを次のtick境界で同じsegmentへ戻す。
            // Return each output to its segment at the next tick boundary.
            scenario.ReinsertOutputs();
            scenario.Simulation.Tick(parallel);
        }
        stopwatch.Stop();
        var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocatedBefore;

        // 測定外で保留中のアイテムも含めてGUIDと個数を検証する。
        // Validate GUIDs and count after measurement, including pending outputs.
        if (!scenario.ValidateItems(out var finalItemCount, out var validationError))
        {
            Console.Error.WriteLine(validationError);
            return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            segmentCount,
            capacity,
            ticks,
            warmup,
            mode = args[4],
            framework = RuntimeInformation.FrameworkDescription,
            processorCount = Environment.ProcessorCount,
            measurementScope = "tick-and-reinsertion",
            elapsedMs = stopwatch.Elapsed.TotalMilliseconds,
            msPerTick = stopwatch.Elapsed.TotalMilliseconds / ticks,
            allocatedBytes,
            allocatedBytesPerTick = (double)allocatedBytes / ticks,
            initialItemCount,
            finalItemCount,
            outputCount = scenario.OutputCount,
            reinsertionCount = scenario.ReinsertionCount
        }));
        return 0;
    }

    private static bool TryPositive(string value, out int parsed)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && 0 < parsed;
    }
}
