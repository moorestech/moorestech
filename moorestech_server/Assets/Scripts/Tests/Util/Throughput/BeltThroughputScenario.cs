using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.Util.Throughput
{
    // スループット計測の配置（ADR 0084）。Run は「設置→計測開始→窓」を一息に行い、途中で手を入れる配置は Start でセッションを取る
    // A throughput measurement layout (ADR 0084). Run does place -> start -> window in one go; layouts that intervene midway take a session via Start
    public sealed class BeltThroughputScenario
    {
        private readonly List<(BlockId blockId, Vector3Int position, BlockDirection direction)> _placements = new();
        private readonly List<Vector3Int> _sources = new();
        private readonly List<Vector3Int> _sinks = new();
        private readonly List<Vector3Int> _blockedSinks = new();
        private int _window = BeltThroughputWindow.Plain;

        // ブロックIDの解決(ForUnitTestModBlockId)がマスタ読込済みを前提にするので、配置を組む前にサーバーを起こす
        // Block id lookups (ForUnitTestModBlockId) need the master loaded, so the server is booted before the layout is built
        public BeltThroughputScenario()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        public BeltThroughputScenario Block(BlockId blockId, Vector3Int position, BlockDirection direction)
        {
            _placements.Add((blockId, position, direction));
            return this;
        }

        // 供給チェスト(出力口だけ)。設置後に1スロットへ大スタックを入れ、毎tick空きがあれば1個押し込む
        // A source chest (outputs only); after placement one slot gets a huge stack, and it pushes one item per tick whenever the belt has room
        // 通常のチェストは全面に出力口があり、隣のチェストやベルトの横面へ勝手に押し込むので使わない
        // Ordinary chests emit on every face and would push into neighbouring chests or belt sides, so they are not used
        public BeltThroughputScenario Source(Vector3Int position)
        {
            _sources.Add(position);
            return Block(ForUnitTestModBlockId.ThroughputSourceChest, position, BlockDirection.North);
        }

        // 受けチェスト(入力口だけ)
        // A sink chest (inputs only)
        public BeltThroughputScenario Sink(Vector3Int position)
        {
            _sinks.Add(position);
            return Block(ForUnitTestModBlockId.ThroughputSinkChest, position, BlockDirection.North);
        }

        // 1スロットの受け。計測中に BlockSink/UnblockSink で詰まらせたり戻したりする配置が使う
        // A one-slot sink, for layouts that block and unblock it mid-measurement via BlockSink/UnblockSink
        public BeltThroughputScenario OneSlotSink(Vector3Int position)
        {
            _sinks.Add(position);
            return Block(ForUnitTestModBlockId.ThroughputOneSlotSinkChest, position, BlockDirection.North);
        }

        // 最初から詰まっている受け。1スロットのチェストを別アイテムで埋め、計測開始の条件には数えない
        // A sink that is blocked from the start: a one-slot chest filled with another item, not counted for the measurement start
        public BeltThroughputScenario BlockedSink(Vector3Int position)
        {
            _blockedSinks.Add(position);
            return Block(ForUnitTestModBlockId.ThroughputOneSlotSinkChest, position, BlockDirection.North);
        }

        public BeltThroughputScenario Window(int ticks)
        {
            _window = ticks;
            return this;
        }

        public BeltThroughputRun Run(BeltPlacementOrder order)
        {
            var session = Start(order);
            session.TickUntilEverySinkReceived();
            return session.Measure(_window);
        }

        // 設置し、供給を満たし、最初のtickで組を作って複製を起こした状態で返す
        // Places everything, fills the sources, builds the assembly on the first tick and starts the replica
        public BeltThroughputSession Start(BeltPlacementOrder order)
        {
            foreach (var (blockId, position, direction) in order.Apply(_placements))
                Assert.IsNotNull(Place(blockId, position, direction), $"placed {MasterHolder.BlockMaster.GetBlockMaster(blockId).Name} at {position}");
            return new BeltThroughputSession(_sources, _sinks, _blockedSinks, _window);
        }
    }
}
