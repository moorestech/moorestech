using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
namespace BeltSegment.Benchmark;
internal static class BeltReplayBenchmark
{
    internal static int Run(int segmentCount, int capacity, int ticks, int warmup)
    {
        var states = new BeltReplaySegmentState[segmentCount];
        var inputs = new BeltReplayInput[segmentCount];
        var outputs = new BeltReplayOutput[segmentCount];
        var ports = new Port[segmentCount];
        // 記録元の準備とイベント生成は測定区間外。
        // Source setup and event recording are outside both measured intervals.
        for(int i=0;i<segmentCount;i++)
        {
            var items = Enumerable.Range(0,(capacity+3)/4).Select(n => new BeltItemState(
                new BeltItem { Guid=Guid.NewGuid(), ItemId=n%2+1, AcceptedInput=BeltDirection.Back },n*1024)).ToArray();
            states[i]=BeltReplaySegmentState.Normal(capacity,16,items);
            inputs[i]=new BeltReplayInput(i,BeltDirection.Back);
            outputs[i]=new BeltReplayOutput(i,BeltDirection.Front);
            ports[i]=new Port();
        }
        var initial=new BeltReplaySnapshot(states,Array.Empty<BeltReplayLink>(),inputs,outputs);
        var graph=new BeltSimulationGraph(initial,ports,ports);
        var frames=new BeltReplayTick[ticks];
        long inputEvents=0, outputEvents=0;
        for(int tick=0;tick<ticks;tick++)
        {
            graph.Tick(false);
            var sent=new List<int>(); var received=new List<BeltReplayInsertion>();
            for(int i=0;i<segmentCount;i++)
            {
                if(!ports[i].Pending.HasValue) continue;
                sent.Add(i);
                var item=ports[i].Pending.Value;
                if(!graph.TryInsert(i,16,item)) throw new InvalidOperationException("Source reinsertion failed");
                received.Add(new BeltReplayInsertion(i,16,item));ports[i].Pending=null;
            }
            frames[tick]=new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(),Array.Empty<int>(),sent.ToArray(),received.ToArray());
            inputEvents+=received.Count;outputEvents+=sent.Count;
        }
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
        if(graph.ComputeStateHash()!=replay.ComputeStateHash() || !initialIds.SequenceEqual(finalIds))
            throw new InvalidOperationException("Replay parity or conservation failed");
        Console.WriteLine(JsonSerializer.Serialize(new {
            mode="replay-packing",segmentCount,capacity,ticks,warmup,initialItemCount=initialIds.Length,finalItemCount=finalIds.Length,
            inputEvents,outputEvents,packedEvents,uploadStride=Marshal.SizeOf<GpuBeltEvent>(),gpuUploadBytes=packedEvents*Marshal.SizeOf<GpuBeltEvent>(),
            cpuReplayMs=replayMs,cpuReplayAllocatedBytes=replayAlloc,cpuUploadPreparationMs=packingMs,cpuUploadPreparationAllocatedBytes=packingAlloc,
            measurementScope="CPU replay and actual GPU ABI packing measured separately; setup/recording/validation excluded",
            wireBytes="Measured by Unity BeltWireRoundTripTest with the production MessagePack serializer",
            gpuExecutionTime="not measured",parity=true,framework=RuntimeInformation.FrameworkDescription
        }));
        return 0;
    }
    private sealed class Port : IBeltSource, IBeltReceiver
    {
        internal BeltItem? Pending;
        public bool TryGetOutput(BeltDirection direction)=>false;
        public void AttachInput(IBeltSource source,BeltDirection direction) { }
        public int GetOffer(BeltDirection direction)=>Pending.HasValue ? 0 : 256;
        public bool TryReceive(BeltDirection direction,int length,in BeltItem item)
        {
            if(Pending.HasValue)return false;
            Pending=item;return true;
        }
    }
}
