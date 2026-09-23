using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Client.Game.InGame.BeltSegment.Gpu;
using Client.Tests.BeltSegment.Measurement;
using Game.BeltSegment;
namespace BeltSegment.Benchmark;
internal static class BeltReplayBenchmark
{
    internal static int Run(int segmentCount, int capacity, int ticks, int warmup)
    {
        var workload = new BeltRecordedWorkload(segmentCount, capacity, ticks, false);
        var initial = workload.Initial; var frames = workload.Frames;
        long inputEvents = workload.InputEvents, outputEvents = workload.OutputEvents;
        var warm=new BeltReplaySimulation(initial);
        var upload=new GpuBeltTickUpload(segmentCount,segmentCount,segmentCount);
        for(int n=0;n<warmup;n++)
        {
            if(n%ticks==0) warm=new BeltReplaySimulation(initial);
            warm.ApplyTick(frames[n%ticks],false);upload.Prepare(frames[n%ticks]);
        }
        var replay=new BeltReplaySimulation(initial);
        var clock=new Stopwatch(); long before=GC.GetTotalAllocatedBytes(true);
        clock.Start();foreach(var frame in frames) replay.ApplyTick(frame,false);clock.Stop();
        double replayMs=clock.Elapsed.TotalMilliseconds;long replayAlloc=GC.GetTotalAllocatedBytes(true)-before;
        clock.Reset();before=GC.GetTotalAllocatedBytes(true);long packedEvents=0;
        clock.Start();foreach(var frame in frames) packedEvents+=upload.Prepare(frame);clock.Stop();
        double packingMs=clock.Elapsed.TotalMilliseconds;long packingAlloc=GC.GetTotalAllocatedBytes(true)-before;
        // 実Coreとreplayを照合し、GUID集合と個数も別に照合。
        // Compare actual Core/replay state hashes and independently check identity conservation.
        var final=replay.CaptureSnapshot();
        var initialIds=initial.Segments.SelectMany(s=>s.Items).Select(i=>i.Item.Guid).OrderBy(i=>i).ToArray();
        var finalIds=final.Segments.SelectMany(s=>s.Items).Select(i=>i.Item.Guid).OrderBy(i=>i).ToArray();
        if(workload.FinalHash!=replay.ComputeStateHash() || !initialIds.SequenceEqual(finalIds))
            throw new InvalidOperationException("Replay parity or conservation failed");
        Console.WriteLine(JsonSerializer.Serialize(new {
            mode="replay-packing",segmentCount,capacity,ticks,warmup,initialItemCount=initialIds.Length,finalItemCount=finalIds.Length,
            inputEvents,outputEvents,packedEvents,uploadStride=Marshal.SizeOf<GpuBeltEvent>(),gpuUploadBytes=packedEvents*Marshal.SizeOf<GpuBeltEvent>(),
            cpuReplayMs=replayMs,cpuReplayAllocatedBytes=replayAlloc,cpuUploadPreparationMs=packingMs,cpuUploadPreparationAllocatedBytes=packingAlloc,
            measurementScope="CPU replay and actual GPU ABI packing measured separately; setup/recording/validation excluded",
            wireMeasurement="Unity BeltRecordedWireMeasurementTest serializes this shared workload",
            gpuExecutionTime="not measured",parity=true,framework=RuntimeInformation.FrameworkDescription
        }));
        return 0;
    }
}
