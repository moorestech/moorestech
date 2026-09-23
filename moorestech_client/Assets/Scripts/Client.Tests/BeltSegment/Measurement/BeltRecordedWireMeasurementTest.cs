using System;
using System.Diagnostics;
using System.Linq;
using Client.Tests.BeltSegment.Network;
using Game.BeltSegment;
using MessagePack;
using NUnit.Framework;
using Server.Util.MessagePack.BeltSegment;

namespace Client.Tests.BeltSegment.Measurement
{
    public sealed class BeltRecordedWireMeasurementTest
    {
        [Test]
        public void SerializeShared129SegmentWorkloadWithProductionMessagePack()
        {
            const int segments = 129, capacity = 64, ticks = 10000, warmup = 1000;
            BeltNetworkFixture.LoadMaster();
            var workload = new BeltRecordedWorkload(segments, capacity, ticks, false);
            var routes = Enumerable.Range(0, segments).Select(segment => new BeltRoute(
                Enumerable.Range(0, capacity).Select(cell => new BeltRouteCell(new(segment * 2, cell, 0), BeltEntryDirection.FromBack, 0, 0)).ToArray(),
                Enumerable.Repeat(new BeltRouteCell(new(segment * 2, -1, 0), BeltEntryDirection.FromBack, 0, 0), 4).ToArray())).ToArray();
            var initial = new BeltWorldSnapshot(new(0, 0), 1, workload.Initial, routes);
            var snapshot = new BeltWorldSnapshotMessagePack(initial);
            var frames = new BeltWorldFrameMessagePack[ticks];
            var replay = new BeltReplaySimulation(workload.Initial);
            var previous = initial.Position;
            // 実際のstream位置と前状態hashを使い、DTO準備は測定の外に置く。
            // Use real stream positions and prior hashes; prepare DTOs outside measurement.
            for (int i = 0; i < ticks; i++)
            {
                var position = new BeltStreamPosition((ulong)i + 1, 1);
                frames[i] = new BeltWorldFrameMessagePack(new(previous, position, 1, replay.ComputeStateHash(), workload.Frames[i]));
                replay.ApplyTick(workload.Frames[i], false); previous = position;
            }
            Assert.AreEqual(workload.FinalHash, replay.ComputeStateHash());
            for (int i = 0; i < warmup; i++) MessagePackSerializer.Serialize(frames[i]);
            MessagePackSerializer.Serialize(snapshot);
            // Monoの割当counterは未実装の場合0固定なので、既知の配列割当で可用性を確かめる。
            // Some Mono allocation counters always return zero; probe a known array allocation.
            long probeBefore = GC.GetAllocatedBytesForCurrentThread();
            var allocationProbe = new byte[8192];
            GC.KeepAlive(allocationProbe);
            long allocationProbeBytes = GC.GetAllocatedBytesForCurrentThread() - probeBefore;
            bool allocationCounterAvailable = allocationProbe.Length <= allocationProbeBytes;
            var timer = new Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            timer.Start(); var snapshotBytes = MessagePackSerializer.Serialize(snapshot); timer.Stop();
            long snapshotAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            double snapshotMs = timer.Elapsed.TotalMilliseconds;
            timer.Reset(); before = GC.GetAllocatedBytesForCurrentThread();
            long frameBytes = 0;
            timer.Start();
            foreach (var frame in frames) frameBytes += MessagePackSerializer.Serialize(frame).Length;
            timer.Stop(); long frameAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
            var decoded = BeltWireCodec.Decode(MessagePackSerializer.Deserialize<BeltWorldSnapshotMessagePack>(snapshotBytes));
            var check = new BeltReplaySimulation(decoded.Simulation);
            long decodedInputs = 0, decodedOutputs = 0;
            // 全frameを本番decode経由で再生し、計測対象の個数・hashも独立確認する。
            // Replay every production-decoded frame and independently verify counts/hash.
            foreach (var frame in frames)
            {
                var value = BeltWireCodec.Decode(MessagePackSerializer.Deserialize<BeltWorldFrameMessagePack>(MessagePackSerializer.Serialize(frame)));
                BeltWireCodec.ValidateFrame(value, decoded.Simulation);
                Assert.AreEqual(check.ComputeStateHash(), value.PreviousHash);
                decodedInputs += value.Replay.Insertions.Length; decodedOutputs += value.Replay.SuccessfulOutputs.Length;
                check.ApplyTick(value.Replay, false);
            }
            Assert.AreEqual(20253, decodedInputs); Assert.AreEqual(20253, decodedOutputs);
            Assert.AreEqual(workload.InputEvents, decodedInputs); Assert.AreEqual(workload.OutputEvents, decodedOutputs);
            Assert.AreEqual(workload.FinalHash, check.ComputeStateHash());
            // 本番の外部搬入は新GUIDを割り当てる。旧baselineの同tick循環IDはそのまま保つ。
            // Production ingress allocates fresh IDs; retain the old same-tick recycling baseline above.
            var productionWorkload = new BeltRecordedWorkload(segments, capacity, ticks, true);
            var productionReplay = new BeltReplaySimulation(productionWorkload.Initial);
            var priorHashes = new uint[ticks];
            for (int i = 0; i < ticks; i++)
            {
                priorHashes[i] = productionReplay.ComputeStateHash();
                productionReplay.ApplyTick(productionWorkload.Frames[i], false);
            }
            productionReplay = new BeltReplaySimulation(productionWorkload.Initial);
            var replayTimer = new Stopwatch();
            long replayBefore = GC.GetAllocatedBytesForCurrentThread();
            replayTimer.Start();
            for (int i = 0; i < ticks; i++)
                if (!productionReplay.TryApplyTick(productionWorkload.Frames[i], priorHashes[i], false, out var reason))
                    Assert.Fail(reason);
            replayTimer.Stop();
            long replayAllocated = GC.GetAllocatedBytesForCurrentThread() - replayBefore;
            Assert.AreEqual(productionWorkload.FinalHash, productionReplay.ComputeStateHash());
            Assert.AreEqual(workload.InputEvents, productionWorkload.InputEvents);
            Assert.AreEqual(workload.OutputEvents, productionWorkload.OutputEvents);
            UnityEngine.Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(new {
                measurement = "production-TryApplyTick-fresh-ingress-workload", segments, capacity, ticks,
                elapsedMs = replayTimer.Elapsed.TotalMilliseconds,
                allocatedBytes = allocationCounterAvailable ? (long?)replayAllocated : null,
                allocationCounterAvailable, allocationProbeBytes, parity = true,
                scope = "Same 129x64 event timing/counts with fresh ingress GUIDs as production; CPU replay with previousHash/live GUID checks; DTO decode/GPU excluded; old recycling-ID packing baseline above is unchanged"
            }));
            int initialItems = workload.Initial.Segments.Sum(s => s.Items.Length);
            int finalItems = check.CaptureSnapshot().Segments.Sum(s => s.Items.Length);
            Assert.AreEqual(2064, initialItems); Assert.AreEqual(initialItems, finalItems);
            UnityEngine.Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(new {
                measurement = "production-messagepack-shared-workload", segments, capacity, ticks, warmup,
                initialItems, finalItems, inputEvents = decodedInputs, outputEvents = decodedOutputs,
                snapshotBytes = snapshotBytes.Length, accumulatedFrameBytes = frameBytes,
                snapshotSerializationMs = snapshotMs, snapshotAllocatedBytes = allocationCounterAvailable ? (long?)snapshotAllocated : null,
                frameSerializationMs = timer.Elapsed.TotalMilliseconds, frameAllocatedBytes = allocationCounterAvailable ? (long?)frameAllocated : null,
                allocationCounterAvailable, allocationProbeBytes,
                gpuUploadBytes = (decodedInputs + decodedOutputs) * 16, parity = true,
                scope = "BeltWorld snapshot/frame DTO MessagePack payloads; transport envelope excluded; item Position null as in shared Core workload; generation1 tick0..10000 sequence1"
            }));
        }
    }
}
