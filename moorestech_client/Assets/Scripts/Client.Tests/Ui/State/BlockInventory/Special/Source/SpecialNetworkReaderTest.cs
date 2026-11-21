using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Source;
using Core.Master;
using Game.Gear.Common;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using ServerFilterSplitterMode = Game.Block.Blocks.FilterSplitter.FilterSplitterMode;
using ServerFilterSplitterOutputSide = Game.Block.Interface.Component.FilterSplitterOutputSide;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// 集約キャッシュの値を網と分配器の詳細へ写す（未取得は不在）
    /// Maps aggregate cache values to network and splitter details; unfetched values stay absent
    /// </summary>
    public class SpecialNetworkReaderTest
    {
        [Test]
        public void 未取得の集約は不在のまま()
        {
            Assert.IsNull(SpecialNetworkReader.Electric(null));
            Assert.IsNull(SpecialNetworkReader.Gear(null));
            Assert.IsNull(SpecialNetworkReader.FilterSplitter(null, new[] { ServerFilterSplitterOutputSide.Left }));
        }

        [Test]
        public void 電力網と歯車網の集約を写す()
        {
            var electric = SpecialNetworkReader.Electric(new GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot(240f, 180f, 1f, 3));
            Assert.AreEqual(240d, electric.TotalGeneratePower);
            Assert.AreEqual(180d, electric.TotalRequiredPower);
            Assert.AreEqual(3, electric.ConsumerCount);
            Assert.AreEqual(1d, electric.PowerRate);

            var gear = SpecialNetworkReader.Gear(new GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot(30f, 20f, GearNetworkStopReason.OverRequirePower));
            Assert.AreEqual(20d, gear.TotalGenerateGearPower);
            Assert.AreEqual(30d, gear.TotalRequiredGearPower);
            Assert.AreEqual(GearNetworkStopReasonKind.OverRequirePower, gear.StopReason);
        }

        [Test]
        public void 供給率はWebと同じ値で丸まる()
        {
            var electric = SpecialNetworkReader.Electric(new GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot(167f, 200f, 0.835f, 1));
            Assert.AreEqual(0.835d, electric.PowerRate);
        }

        [Test]
        public void 停止理由はサーバーの全値を写す()
        {
            foreach (GearNetworkStopReason reason in Enum.GetValues(typeof(GearNetworkStopReason)))
            {
                Assert.AreEqual(reason.ToString(), SpecialNetworkReader.ToStopReasonKind(reason).ToString());
            }
        }

        [Test]
        public void 分配器のモードを両向きに写し方向とフィルタ枠を並びのまま写す()
        {
            foreach (ServerFilterSplitterMode mode in Enum.GetValues(typeof(ServerFilterSplitterMode)))
            {
                Assert.AreEqual(mode, SpecialNetworkReader.ToServerMode(SpecialNetworkReader.ToClientMode(mode)));
            }

            var response = new FilterSplitterStateProtocol.FilterSplitterStateResponse(true, FilterSplitterStateProtocol.FilterSplitterStateFailureReason.None, 2, 2, new List<FilterSplitterStateProtocol.DirectionStatePack>
            {
                new() { Mode = ServerFilterSplitterMode.Whitelist, FilterItemIds = new List<ItemId> { new(4), ItemMaster.EmptyItemId } },
                new() { Mode = ServerFilterSplitterMode.Blacklist, FilterItemIds = new List<ItemId> { new(7), new(8) } },
            });
            var detail = SpecialNetworkReader.FilterSplitter(response, new[] { ServerFilterSplitterOutputSide.Right, ServerFilterSplitterOutputSide.Front });
            Assert.AreEqual(2, detail.Directions.Count);
            Assert.AreEqual(FilterSplitterMode.Whitelist, detail.Directions[0].Mode);
            CollectionAssert.AreEqual(new[] { new ItemId(4), ItemMaster.EmptyItemId }, detail.Directions[0].FilterItemIds);
            Assert.AreEqual(FilterSplitterMode.Blacklist, detail.Directions[1].Mode);
            Assert.AreEqual(FilterSplitterOutputSide.Right, detail.Directions[0].Side);
            Assert.AreEqual(FilterSplitterOutputSide.Front, detail.Directions[1].Side);
        }

        [Test]
        public void 応答の方向数とマスタの出口の数が違えば止める()
        {
            // 数の食い違いはクライアントとサーバーのマスタが違うときだけ起きる。見出しが嘘になるので黙って詰めない
            // A count mismatch only happens when client and server masters differ; the headings would lie, so do not pad silently
            var response = new FilterSplitterStateProtocol.FilterSplitterStateResponse(true, FilterSplitterStateProtocol.FilterSplitterStateFailureReason.None, 1, 2, new List<FilterSplitterStateProtocol.DirectionStatePack>
            {
                new() { Mode = ServerFilterSplitterMode.Default, FilterItemIds = new List<ItemId> { ItemMaster.EmptyItemId, ItemMaster.EmptyItemId } },
            });

            Assert.Throws<InvalidOperationException>(() => SpecialNetworkReader.FilterSplitter(response, new[] { ServerFilterSplitterOutputSide.Left, ServerFilterSplitterOutputSide.Front }));
        }
    }
}
