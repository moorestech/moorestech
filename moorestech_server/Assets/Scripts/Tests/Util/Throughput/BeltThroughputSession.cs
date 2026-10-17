using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Tests.CombinedTest.Core.Transport;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.Util.Throughput
{
    // 設置済みワールドでの計測手順。毎tick複製へ差分を再生し、サーバーとハッシュが一致することを要求する（関所2）
    // The measurement procedure on a placed world; every tick replays the diff on the replica and demands its hash to match the server (gate 2)
    public sealed class BeltThroughputSession
    {
        private const int SourceStackCount = 1_000_000;
        private const int MinimumArrivalsBeforeStart = 2;
        private const int StartDeadlineTicks = 20_000;
        private static readonly ItemId BlockingItem = new(1);

        private readonly List<IOpenableBlockInventoryComponent> _sources = new();
        private readonly List<IOpenableBlockInventoryComponent> _sinks = new();
        private readonly BeltTransportReplica _replica;
        private readonly BeltSegmentExitCounter _exitCounter;
        public int DefaultWindow { get; }
        public int Tick { get; private set; }

        public BeltThroughputSession(IReadOnlyList<Vector3Int> sources, IReadOnlyList<Vector3Int> sinks, IReadOnlyList<Vector3Int> blockedSinks, IReadOnlyList<Vector3Int> watchedCells, int defaultWindow)
        {
            DefaultWindow = defaultWindow;
            foreach (var position in sources)
            {
                var inventory = InventoryAt(position);
                inventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ThroughputTestItem, SourceStackCount));
                _sources.Add(inventory);
            }
            foreach (var position in sinks) _sinks.Add(InventoryAt(position));
            foreach (var position in blockedSinks) BlockSink(position);

            // 最初のtickで組が作られる。複製はその組の全量から起こし、以後は差分だけで追従させる
            // The first tick builds the assembly; the replica starts from that full state and follows diffs only
            BeltDiffReplayTestUtil.TickServer();
            Tick++;
            _replica = BeltDiffReplayTestUtil.StartReplica();
            _exitCounter = new BeltSegmentExitCounter(watchedCells);
        }

        public static IOpenableBlockInventoryComponent InventoryAt(Vector3Int position)
        {
            return Inventory(ServerContext.WorldBlockDatastore.GetBlock(position));
        }

        public void TickOnce()
        {
            var diff = BeltDiffReplayTestUtil.TickServer();
            Tick++;
            BeltDiffReplayTestUtil.ReplayAndAssert(_replica, diff, Tick);
            _exitCounter.Observe();
        }

        public void TickMany(int count)
        {
            for (var i = 0; i < count; i++) TickOnce();
        }

        // 定常状態まで進める: 全受けチェストが今より2個以上増え、監視 segment がそれぞれ2個以上搬出するまで。何tick目かの厳密性は求めない(ユーザー裁定 2026-10-18)
        // Advance to the steady state: every sink has received two more items and every watched segment has emitted two; the exact tick is not demanded (user ruling 2026-10-18)
        public void TickUntilSteady()
        {
            var sinkFloor = SinkCounts();
            var exitFloor = _exitCounter.Exits();
            var deadline = Tick + StartDeadlineTicks;
            while (!Reached(SinkCounts(), sinkFloor) || !Reached(_exitCounter.Exits(), exitFloor))
            {
                TickOnce();
                if (deadline < Tick) Assert.Fail($"no steady state within {StartDeadlineTicks} ticks: sinks={string.Join(",", SinkCounts())} watchedExits={string.Join(",", _exitCounter.Exits())}");
            }
            return;

            bool Reached(List<int> now, List<int> floor)
            {
                for (var i = 0; i < now.Count; i++)
                {
                    if (now[i] - floor[i] < MinimumArrivalsBeforeStart) return false;
                }
                return true;
            }
        }

        // 窓の開始を次の供給の tick に揃えてから、窓の間の各供給の減分・各受けの増分・ベルト上の増減を数える
        // Align the window start to the next source emission, then count each source's decrease, each sink's increase and the change on the belts
        // 供給は周期 P ごとに1個出るので、供給の tick から数えれば窓の供給数は floor(W/P) でちょうど決まる。受けの合計は経路や buffer の位相で ±1 揺れるので保存則で見る
        // A source emits once per period P, so counting from an emission tick gives exactly floor(W/P) emissions; sink totals wobble by ±1 with route and buffer phase, so they are checked by conservation
        public BeltThroughputRun Measure(int window)
        {
            TickUntilSourceEmission();
            var startTick = Tick;
            var sinkBaseline = SinkCounts();
            var sourceBaseline = SourceCounts();
            var onBeltsBaseline = ItemsOnBelts();
            var exitBaseline = _exitCounter.Exits();
            TickMany(window);
            var sinkCounts = SinkCounts();
            var sourceCounts = SourceCounts();
            for (var i = 0; i < sinkCounts.Count; i++) sinkCounts[i] -= sinkBaseline[i];
            for (var i = 0; i < sourceCounts.Count; i++) sourceCounts[i] = sourceBaseline[i] - sourceCounts[i];
            var watchedExits = _exitCounter.Exits();
            for (var i = 0; i < watchedExits.Count; i++) watchedExits[i] -= exitBaseline[i];
            return new BeltThroughputRun(window, startTick, sinkCounts, sourceCounts, ItemsOnBelts() - onBeltsBaseline, watchedExits);
        }

        // どれかの供給から1個出た tick まで進める。供給が無い配置は即座に戻る
        // Advance to the tick on which any source emitted one item; returns at once for layouts without sources
        private void TickUntilSourceEmission()
        {
            if (_sources.Count == 0) return;
            var before = SourceCounts();
            var deadline = Tick + StartDeadlineTicks;
            while (true)
            {
                TickOnce();
                var now = SourceCounts();
                for (var i = 0; i < now.Count; i++)
                {
                    if (now[i] < before[i]) return;
                }
                if (deadline < Tick) Assert.Fail($"no source emission within {StartDeadlineTicks} ticks");
            }
        }

        // 1スロットのチェストを別アイテムで埋めて受け付けなくする／空にして受け付けに戻す
        // Fill a one-slot chest with another item so it refuses, or empty it so it accepts again
        public void BlockSink(Vector3Int position)
        {
            var inventory = InventoryAt(position);
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(BlockingItem, ItemStackLevelDataStore.Instance.GetMaxStack(BlockingItem)));
        }

        public void UnblockSink(Vector3Int position)
        {
            InventoryAt(position).SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
        }

        // ベルト上(buffer含む)にあるアイテム数。個数保存の確認に使う
        // Items on the belts, buffers included; used to check conservation
        public int ItemsOnBelts()
        {
            var total = 0;
            foreach (var segment in Assembly().Segments)
            {
                total += segment.CaptureItems().Length;
                if (segment is BeltBufferedSegment buffered && buffered.Buffer.HasItem) total++;
            }
            return total;
        }

        public List<int> WatchedExits()
        {
            return _exitCounter.Exits();
        }

        public List<int> SinkCounts()
        {
            var result = new List<int>(_sinks.Count);
            foreach (var sink in _sinks) result.Add(CountOf(sink, ForUnitTestItemId.ThroughputTestItem));
            return result;
        }

        public List<int> SourceCounts()
        {
            var result = new List<int>(_sources.Count);
            foreach (var source in _sources) result.Add(CountOf(source, ForUnitTestItemId.ThroughputTestItem));
            return result;
        }
    }
}
