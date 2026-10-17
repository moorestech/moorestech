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
        public int DefaultWindow { get; }
        public int Tick { get; private set; }

        public BeltThroughputSession(IReadOnlyList<Vector3Int> sources, IReadOnlyList<Vector3Int> sinks, IReadOnlyList<Vector3Int> blockedSinks, int defaultWindow)
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
        }

        public void TickMany(int count)
        {
            for (var i = 0; i < count; i++) TickOnce();
        }

        // 全受けチェストが今より2個以上増えるまで進める。受けが無い配置は即座に戻る
        // Advance until every sink has received at least two more items than now; returns at once for layouts without sinks
        public void TickUntilEverySinkReceived()
        {
            var floor = SinkCounts();
            var deadline = Tick + StartDeadlineTicks;
            while (!EverySinkReceived())
            {
                TickOnce();
                if (deadline < Tick) Assert.Fail($"no measurement start within {StartDeadlineTicks} ticks: sinks={string.Join(",", SinkCounts())}");
            }
            return;

            bool EverySinkReceived()
            {
                var counts = SinkCounts();
                for (var i = 0; i < counts.Count; i++)
                {
                    if (counts[i] - floor[i] < MinimumArrivalsBeforeStart) return false;
                }
                return true;
            }
        }

        // 窓の間の各受けの増分と各供給の減分を数える
        // Count each sink's increase and each source's decrease over the window
        public BeltThroughputRun Measure(int window)
        {
            var startTick = Tick;
            var sinkBaseline = SinkCounts();
            var sourceBaseline = SourceCounts();
            TickMany(window);
            var sinkCounts = SinkCounts();
            var sourceCounts = SourceCounts();
            for (var i = 0; i < sinkCounts.Count; i++) sinkCounts[i] -= sinkBaseline[i];
            for (var i = 0; i < sourceCounts.Count; i++) sourceCounts[i] = sourceBaseline[i] - sourceCounts[i];
            return new BeltThroughputRun(window, startTick, sinkCounts, sourceCounts);
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
