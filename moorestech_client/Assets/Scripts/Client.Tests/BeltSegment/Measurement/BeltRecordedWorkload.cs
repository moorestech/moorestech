using System;
using System.Collections.Generic;
using System.Linq;
using Game.BeltSegment;

namespace Client.Tests.BeltSegment.Measurement
{
    // Unity通信量と.NET replay/packingで同一の記録済みworkloadを共有する。
    // Share one recorded workload between Unity wire sizing and .NET replay/packing.
    internal sealed class BeltRecordedWorkload
    {
        internal readonly BeltReplaySnapshot Initial;
        internal readonly BeltReplayTick[] Frames;
        internal readonly long InputEvents, OutputEvents;
        internal readonly uint FinalHash;
        internal BeltRecordedWorkload(int segmentCount, int capacity, int ticks, bool freshIngressIdentity)
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
                    new BeltItem { Guid=new Guid(i * capacity + n + 1, 0, 0, new byte[8]), ItemId=n%2+1, AcceptedInput=BeltDirection.Back },n*1024)).ToArray();
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
                    if (freshIngressIdentity) item.Guid = new Guid(segmentCount * capacity + tick * segmentCount + i + 1, 0, 0, new byte[8]);
                    if(!graph.TryInsert(i,16,item)) throw new InvalidOperationException("Source reinsertion failed");
                    received.Add(new BeltReplayInsertion(i,16,item));ports[i].Pending=null;
                }
                frames[tick]=new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(),Array.Empty<int>(),sent.ToArray(),received.ToArray());
                inputEvents+=received.Count;outputEvents+=sent.Count;
            }
            Initial = initial; Frames = frames; InputEvents = inputEvents; OutputEvents = outputEvents;
            FinalHash = graph.ComputeStateHash();
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
}
