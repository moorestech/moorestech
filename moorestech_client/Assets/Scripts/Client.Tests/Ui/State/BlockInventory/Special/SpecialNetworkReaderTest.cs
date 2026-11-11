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
            Assert.IsNull(SpecialNetworkReader.FilterSplitter(null));
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
            var detail = SpecialNetworkReader.FilterSplitter(response);
            Assert.AreEqual(2, detail.Directions.Count);
            Assert.AreEqual(FilterSplitterMode.Whitelist, detail.Directions[0].Mode);
            CollectionAssert.AreEqual(new[] { new ItemId(4), ItemMaster.EmptyItemId }, detail.Directions[0].FilterItemIds);
            Assert.AreEqual(FilterSplitterMode.Blacklist, detail.Directions[1].Mode);
        }
    }
}
