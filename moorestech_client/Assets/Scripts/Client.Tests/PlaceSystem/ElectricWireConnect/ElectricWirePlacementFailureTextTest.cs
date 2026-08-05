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

        [Test]
        public void 残りの失敗理由も個別の文言に変換される()
        {
            Assert.AreEqual("電柱が足りません", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.NoPoleItem));
            Assert.AreEqual("接続できない対象です", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.InvalidTarget));
            Assert.AreEqual("設置位置が埋まっています", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.PositionOccupied));
            Assert.AreEqual("インベントリがいっぱいです", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.InventoryFull));
            Assert.AreEqual("接続されていません", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.NotConnected));
            Assert.AreEqual("未解放です", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.NotUnlocked));
            Assert.AreEqual("素材が足りません", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.InsufficientItems));
        }

        [Test]
        public void 未定義の理由は既定文言にフォールバックする()
        {
            Assert.AreEqual("設置できません", ElectricWirePlacementFailureText.ToText(ElectricWirePlacementFailureReason.InvalidMode));
        }
    }
}
