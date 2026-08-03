using Client.Game.InGame.UI.Inventory.Main;
using NUnit.Framework;
using Server.Util.MessagePack;

namespace Client.Tests.Inventory
{
    /// <summary>
    ///     ローカル座標→サーバー識別子/スロットの変換を固定する。装備が grab やメインへ化けると移動先が丸ごと変わる
    ///     Pins the local-to-server coordinate conversion; equipment degrading into grab or main would redirect the whole move
    /// </summary>
    public class InventoryMoveServerCoordinateTest
    {
        private const int MainSlotCount = 54;
        private const int PlayerId = 3;

        // 装備はスロット恒等で装備識別子へ写る（結合スロットではないのでオフセットを引かない）
        // Equipment maps onto the equipment identifier with an identity slot (no offset, since it is not a combined slot)
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EquipmentMapsToEquipmentIdentifierWithIdentitySlot(int localSlot)
        {
            var (identifier, serverSlot) = InventoryMoveServerDispatcher.ToServerCoordinate(null, MainSlotCount, PlayerId, LocalMoveInventoryType.Equipment, localSlot);

            Assert.AreEqual(InventoryType.Equipment, identifier.InventoryType);
            Assert.AreEqual(PlayerId, identifier.PlayerId);
            Assert.AreEqual(localSlot, serverSlot);
        }

        // grab はスロットを持たないため常に0へ潰れる
        // grab has no slot, so it always collapses to 0
        [Test]
        public void GrabMapsToGrabIdentifierWithSlotZero()
        {
            var (identifier, serverSlot) = InventoryMoveServerDispatcher.ToServerCoordinate(null, MainSlotCount, PlayerId, LocalMoveInventoryType.Grab, 5);

            Assert.AreEqual(InventoryType.Grab, identifier.InventoryType);
            Assert.AreEqual(PlayerId, identifier.PlayerId);
            Assert.AreEqual(0, serverSlot);
        }

        // 結合スロットのメイン範囲はスロット恒等でメイン識別子へ写る
        // The main range of the combined slot maps onto the main identifier with an identity slot
        [Test]
        public void MainRangeOfCombinedSlotMapsToMainIdentifier()
        {
            var (identifier, serverSlot) = InventoryMoveServerDispatcher.ToServerCoordinate(null, MainSlotCount, PlayerId, LocalMoveInventoryType.MainOrSub, MainSlotCount - 1);

            Assert.AreEqual(InventoryType.Main, identifier.InventoryType);
            Assert.AreEqual(PlayerId, identifier.PlayerId);
            Assert.AreEqual(MainSlotCount - 1, serverSlot);
        }
    }
}
