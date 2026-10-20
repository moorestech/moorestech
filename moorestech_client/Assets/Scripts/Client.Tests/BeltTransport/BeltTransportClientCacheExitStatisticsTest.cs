using System;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BeltTransport
{
    // 複製の搬出が統計へ届き、マス座標から統計対象のsegmentが引け、合流と内部segmentは対象外になるか
    // Whether replica outputs reach the statistics, a cell resolves to its statistics-bearing segment, and merge and internal segments stay out
    public class BeltTransportClientCacheExitStatisticsTest
    {
        private const int Cell = BeltConstants.ItemWidth;
        private const int Speed = 18;
        private static readonly ItemId ItemA = new(1);

        [Test]
        public void 機械へ渡した搬出が窓に数えられる()
        {
            var cache = new BeltTransportClientCache(new BeltSegmentExitStatisticsTable());
            cache.ApplyFullState(new BeltTransportFullState(new[] { LineToMachine(Running(1, 0)) }));

            // 差分の搬出予告で複製が機械へ渡し、その後は空のまま90tickまで進める
            // The diff's extract announcement makes the replica hand the item to the machine; then it idles to tick 90
            var extract = new BeltTickDiff(Array.Empty<BeltSegmentSpeedRecord>(), Array.Empty<BeltMachineInsertRecord>(), new[] { new BeltMachineExtractRecord(0, BeltDirection.Front) });
            Assert.IsTrue(cache.ApplyTickDiff(extract));
            for (var tick = 2; tick <= 90; tick++) cache.ApplyTickDiff(BeltTickDiff.Empty);

            Assert.IsTrue(cache.ExitStatistics.TryGetExitCount(0, 90, out var count));
            Assert.AreEqual(1, count);
        }

        [Test]
        public void マス座標は通常と分岐のsegmentだけを引き合流と内部は引けない()
        {
            var cache = new BeltTransportClientCache(new BeltSegmentExitStatisticsTable());
            cache.ApplyFullState(new BeltTransportFullState(new[] { LineToMachine(), Branch(), Merge(), Internal() }));

            Assert.IsTrue(cache.TryGetObservedSegmentIndex(new Vector3Int(0, 0, 1), out var line));
            Assert.AreEqual(0, line);
            Assert.IsTrue(cache.TryGetObservedSegmentIndex(new Vector3Int(5, 0, 0), out var branch));
            Assert.AreEqual(1, branch);
            Assert.IsFalse(cache.TryGetObservedSegmentIndex(new Vector3Int(9, 0, 0), out _), "merge cells are not counted");
            Assert.IsFalse(cache.TryGetObservedSegmentIndex(new Vector3Int(1, 1, 1), out _), "not a belt cell");
        }

        [Test]
        public void 組み直しで統計は全リセットされる()
        {
            var cache = new BeltTransportClientCache(new BeltSegmentExitStatisticsTable());
            cache.ApplyFullState(new BeltTransportFullState(new[] { LineToMachine() }));
            for (var tick = 1; tick <= 90; tick++) cache.ApplyTickDiff(BeltTickDiff.Empty);
            Assert.IsTrue(cache.ExitStatistics.TryGetExitCount(0, 90, out _));

            cache.ApplyFullState(new BeltTransportFullState(new[] { LineToMachine() }));
            Assert.AreEqual(0, cache.ExitStatistics.CompletedTicks);
            Assert.IsFalse(cache.ExitStatistics.TryGetExitCount(0, 90, out _));
        }

        // 原点から+Zへ2マス、末尾は機械へ出す
        // Two cells from the origin along +Z, the tail outputs into a machine
        private static BeltSegmentState LineToMachine(params BeltItemSnapshot[] items)
        {
            var cells = new[] { FrontCell(0, 0, 0), FrontCell(0, 0, 1) };
            var outputs = new[] { new BeltLinkShape(BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back), BeltLinkShape.Machine) };
            var shape = new BeltSegmentShape(BeltSegmentKind.Normal, false, cells, cells.Length, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), outputs);
            return new BeltSegmentState(shape, 0, Speed, items, false, default);
        }

        private static BeltSegmentState Branch()
        {
            var shape = new BeltSegmentShape(BeltSegmentKind.Branch, false, new[] { FrontCell(5, 0, 0) }, 1, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Speed, Array.Empty<BeltItemSnapshot>(), false, default);
        }

        private static BeltSegmentState Merge()
        {
            var shape = new BeltSegmentShape(BeltSegmentKind.Merge, false, new[] { FrontCell(9, 0, 0) }, 1, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Speed, Array.Empty<BeltItemSnapshot>(), false, default);
        }

        private static BeltSegmentState Internal()
        {
            var shape = new BeltSegmentShape(BeltSegmentKind.Normal, true, Array.Empty<BeltCellShape>(), 1, Speed, BeltDirection.Front, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
            return new BeltSegmentState(shape, 0, Speed, Array.Empty<BeltItemSnapshot>(), false, default);
        }

        private static BeltItemSnapshot Running(long instanceId, int distanceToExit)
        {
            return new BeltItemSnapshot(new BeltItem(ItemA, new ItemInstanceId(instanceId), BeltEntryDirection.FromBack), distanceToExit);
        }

        private static BeltCellShape FrontCell(int x, int y, int z)
        {
            return new BeltCellShape(new Vector3Int(x, y, z), BeltDirection.Front);
        }
    }
}
