using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Inventory.Drag
{
    /// <summary>
    /// L0-s4a の計画をプレイヤー側の行為へ写し、ブロック側は写さない（S4-alpha のブロック実行器へ回す）
    /// Converts L0-s4a plans into player-side actions and leaves block-side ones unconverted (for S4-alpha's block executor)
    /// </summary>
    public class PlayerItemActionConversionTest
    {
        [Test]
        public void grabとの移動は掴むと置くに写す()
        {
            Assert.IsTrue(PlayerItemActionConversion.TryConvert(PlannedItemAction.MoveItem(ItemMoveSlotRef.Main(2), ItemMoveSlotRef.Grab, 5), out var pickUp));
            Assert.AreEqual(PlayerSlotRef.Main(2), ((PickUpToGrabAction)pickUp).From);
            Assert.IsTrue(PlayerItemActionConversion.TryConvert(PlannedItemAction.MoveItem(ItemMoveSlotRef.Grab, ItemMoveSlotRef.Equipment(1), 1), out var place));
            Assert.AreEqual(PlayerSlotRef.Equipment(1), ((PlaceFromGrabAction)place).To);
        }

        [Test]
        public void 枠どうしの移動と分割と集めを写す()
        {
            Assert.IsInstanceOf<MoveBetweenSlotsAction>(Convert(PlannedItemAction.MoveItem(ItemMoveSlotRef.Equipment(0), ItemMoveSlotRef.Main(3), 2)));
            Assert.IsInstanceOf<SplitToGrabAction>(Convert(PlannedItemAction.Split(ItemMoveSlotRef.Main(0))));
            Assert.IsInstanceOf<CollectAction>(Convert(PlannedItemAction.Collect(ItemMoveSlotRef.Main(0))));
        }

        [Test]
        public void ブロック側の計画は写さない()
        {
            Assert.IsFalse(PlayerItemActionConversion.TryConvert(PlannedItemAction.MoveItem(ItemMoveSlotRef.Block(0), ItemMoveSlotRef.Grab, 1), out _));
            Assert.IsFalse(PlayerItemActionConversion.TryConvert(PlannedItemAction.Split(ItemMoveSlotRef.Block(0)), out _));
            Assert.IsFalse(PlayerItemActionConversion.TryConvert(PlannedItemAction.Collect(ItemMoveSlotRef.Block(0)), out _));
        }

        private static PlayerItemAction Convert(PlannedItemAction action)
        {
            Assert.IsTrue(PlayerItemActionConversion.TryConvert(action, out var converted), action.ToString());
            return converted;
        }
    }
}
