using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core.Transport;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.Util.Throughput
{
    // スループット計測の配置と手順（ADR 0084）。ブロックの世界だけを通し、毎tick複製のハッシュ一致も要求する
    // A throughput measurement layout and procedure (ADR 0084): goes only through the block world and demands the replica hash to match every tick
    // 計測開始は全受けチェストが2個以上受けた tick、そこから Window tick の増分を数える
    // Measurement starts on the tick every sink has received at least two items, then counts the increments over Window ticks
    public sealed class BeltThroughputScenario
    {
        private const int SourceStackCount = 1_000_000;
        private const int MinimumArrivalsBeforeStart = 2;
        private const int StartDeadlineTicks = 20_000;

        private readonly List<(BlockId blockId, Vector3Int position, BlockDirection direction)> _placements = new();
        private readonly List<Vector3Int> _sources = new();
        private readonly List<Vector3Int> _sinks = new();
        private int _window = BeltThroughputWindow.Plain;

        public BeltThroughputScenario Block(BlockId blockId, Vector3Int position, BlockDirection direction)
        {
            _placements.Add((blockId, position, direction));
            return this;
        }

        // 供給チェスト。設置後に1スロットへ大スタックを入れ、毎tick空きがあれば1個押し込む
        // A source chest; after placement one slot gets a huge stack, and it pushes one item per tick whenever the belt has room
        public BeltThroughputScenario Source(Vector3Int position)
        {
            _sources.Add(position);
            return Block(ForUnitTestModBlockId.ChestId, position, BlockDirection.North);
        }

        public BeltThroughputScenario Sink(Vector3Int position)
        {
            _sinks.Add(position);
            return Block(ForUnitTestModBlockId.ChestId, position, BlockDirection.North);
        }

        public BeltThroughputScenario Window(int ticks)
        {
            _window = ticks;
            return this;
        }

        public BeltThroughputRun Run(BeltPlacementOrder order)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            PlaceAll();
            FillSources();
            var sinks = SinkInventories();

            // 最初のtickで組が作られる。複製はその組の全量から起こし、以後は差分だけで追従させる
            // The first tick builds the assembly; the replica starts from that full state and follows diffs only
            var tick = 0;
            BeltDiffReplayTestUtil.TickServer();
            tick++;
            var replica = BeltDiffReplayTestUtil.StartReplica();

            var startTick = TickUntilEverySinkReceived();
            var baseline = Counts();
            for (var i = 0; i < _window; i++) TickOnce();
            var counts = Counts();
            for (var i = 0; i < counts.Count; i++) counts[i] -= baseline[i];
            return new BeltThroughputRun(_window, startTick, counts);

            #region Internal

            void PlaceAll()
            {
                foreach (var (blockId, position, direction) in order.Apply(_placements))
                    Assert.IsNotNull(Place(blockId, position, direction), $"placed {MasterHolder.BlockMaster.GetBlockMaster(blockId).Name} at {position}");
            }

            void FillSources()
            {
                foreach (var position in _sources)
                {
                    var inventory = Inventory(ServerContext.WorldBlockDatastore.GetBlock(position));
                    inventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ThroughputTestItem, SourceStackCount));
                }
            }

            List<IOpenableBlockInventoryComponent> SinkInventories()
            {
                var result = new List<IOpenableBlockInventoryComponent>();
                foreach (var position in _sinks) result.Add(Inventory(ServerContext.WorldBlockDatastore.GetBlock(position)));
                return result;
            }

            void TickOnce()
            {
                var diff = BeltDiffReplayTestUtil.TickServer();
                tick++;
                BeltDiffReplayTestUtil.ReplayAndAssert(replica, diff, tick);
            }

            // 受けチェストが1つも無い配置は不変条件だけを見る配置なので、即座に計測へ入る
            // A layout without sinks only checks invariants, so measurement starts at once
            int TickUntilEverySinkReceived()
            {
                while (!EverySinkReceived())
                {
                    TickOnce();
                    if (StartDeadlineTicks < tick) Assert.Fail($"no measurement start within {StartDeadlineTicks} ticks: sinks={string.Join(",", Counts())}");
                }
                return tick;
            }

            bool EverySinkReceived()
            {
                foreach (var count in Counts())
                {
                    if (count < MinimumArrivalsBeforeStart) return false;
                }
                return true;
            }

            List<int> Counts()
            {
                var result = new List<int>(sinks.Count);
                foreach (var sink in sinks) result.Add(CountOf(sink, ForUnitTestItemId.ThroughputTestItem));
                return result;
            }

            #endregion
        }
    }
}
