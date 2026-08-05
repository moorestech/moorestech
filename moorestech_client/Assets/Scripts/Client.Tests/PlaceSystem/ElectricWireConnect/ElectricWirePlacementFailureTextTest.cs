using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using NUnit.Framework;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    public class ElectricWirePlacementFailureTextTest
    {
        [Test]
        public void 主要な失敗理由が個別の文言に変換される()
        {
            Assert.AreEqual("接続範囲外です", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.OutOfRange));
            Assert.AreEqual("電線が足りません", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.NoWireItem));
            Assert.AreEqual("接続上限です", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.ConnectionLimit));
            Assert.AreEqual("接続済みです", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.AlreadyConnected));
            Assert.AreEqual(string.Empty, ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.None));
        }
    }
}
