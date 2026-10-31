using Client.WebUiHost.Game.Topics.BlockDetail.NetworkInfo;
using Game.Gear.Common;
using NUnit.Framework;
using Server.Protocol.PacketResponse;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    ///     網に属さない応答（Info=null）を消失でなく取得済みの値として返すことを固定する（通信失敗の null とは別扱い）
    ///     Pins that a not-a-member response (Info == null) is a fetched value rather than a vanish, apart from the transport-failure null
    /// </summary>
    public class NetworkInfoPollSourceTest
    {
        [Test]
        public void ElectricNonMemberIsFetchedValueTest()
        {
            var source = new ElectricNetworkInfoPollSource();

            // 網に属さないは取得済みの null、属していればその集約値
            // Not a member is a fetched null; a member yields its aggregate
            Assert.AreEqual(BlockInfoReadResult.Success, source.TryRead(new GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack(null), out var none));
            Assert.IsNull(none);
            var info = new GetElectricNetworkInfoProtocol.ElectricNetworkInfoSnapshot(10f, 5f, 1f, 2);
            Assert.AreEqual(BlockInfoReadResult.Success, source.TryRead(new GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack(info), out var member));
            Assert.AreSame(info, member);
        }

        [Test]
        public void GearNonMemberIsFetchedValueTest()
        {
            var source = new GearNetworkInfoPollSource();

            Assert.AreEqual(BlockInfoReadResult.Success, source.TryRead(new GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack(null), out var none));
            Assert.IsNull(none);
            var info = new GetGearNetworkInfoProtocol.GearNetworkInfoSnapshot(4f, 8f, GearNetworkStopReason.None);
            Assert.AreEqual(BlockInfoReadResult.Success, source.TryRead(new GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack(info), out var member));
            Assert.AreSame(info, member);
        }
    }
}
