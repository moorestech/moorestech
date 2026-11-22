using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Tests.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Client.Ui.State.BlockInventory.Special.Source;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using ServerFilterSplitterMode = Game.Block.Blocks.FilterSplitter.FilterSplitterMode;

namespace Client.Tests.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 開いたフィルター分岐器のマスタから出口の側を決め、届いた応答の方向へ並びのまま載せる（L0）
    /// Derives exit sides from the open filter splitter's master and attaches them to the received directions in order (L0)
    /// </summary>
    public class SpecialBlockDetailReaderFilterSplitterTest
    {
        private TrackedBlockHarness _harness;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _harness = new TrackedBlockHarness();
        }

        [TearDown]
        public void TearDown()
        {
            _harness.Dispose();
        }

        [Test]
        public void 開いた分岐器のマスタの側が方向の並びのまま詳細に載る()
        {
            var block = _harness.CreateBlock(Vector3Int.zero, MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FilterSplitter));
            var cache = new BlockNetworkInfoCache();
            Assert.IsFalse(Read(block, cache).TryGetFilterSplitter(out _), "応答が未着なら出さない");

            // テストマスタの出口は +X・-X・+Z の順で入口は背面。入口から見て右・左・正面
            // The test master lists exits +X, -X, +Z with the entry at the back: right, left, front as seen from the entry
            cache.ApplyFilterSplitterSnapshot(Response(3));
            Assert.IsTrue(Read(block, cache).TryGetFilterSplitter(out var splitter));
            CollectionAssert.AreEqual(new[] { FilterSplitterOutputSide.Right, FilterSplitterOutputSide.Left, FilterSplitterOutputSide.Front }, splitter.Directions.Select(direction => direction.Side).ToArray());
            Assert.AreEqual(FilterSplitterMode.Whitelist, splitter.Directions[0].Mode);
            Assert.AreEqual(FilterSplitterMode.Default, splitter.Directions[2].Mode);
        }

        [Test]
        public void 分岐器でないブロックは分岐器の詳細を出さない()
        {
            var block = _harness.CreateBlock(Vector3Int.zero, MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.RocketLaunchPadId));
            var cache = new BlockNetworkInfoCache();
            cache.ApplyFilterSplitterSnapshot(Response(3));

            Assert.IsFalse(Read(block, cache).TryGetFilterSplitter(out _));
        }

        private static FilterSplitterStateProtocol.FilterSplitterStateResponse Response(int directionCount)
        {
            var directions = Enumerable.Range(0, directionCount).Select(index => new FilterSplitterStateProtocol.DirectionStatePack
            {
                Mode = index == 0 ? ServerFilterSplitterMode.Whitelist : ServerFilterSplitterMode.Default,
                FilterItemIds = new List<ItemId> { ItemMaster.EmptyItemId },
            }).ToList();
            return new FilterSplitterStateProtocol.FilterSplitterStateResponse(true, FilterSplitterStateProtocol.FilterSplitterStateFailureReason.None, directionCount, 1, directions);
        }

        private static SpecialBlockDetailViewModel Read(Client.Game.InGame.Block.BlockGameObject block, BlockNetworkInfoCache cache)
        {
            return SpecialBlockDetailReader.Read(block, cache, new BeltTransportClientCache(new BeltSegmentExitStatisticsTable()), Array.Empty<ItemId>(), out _);
        }
    }
}
