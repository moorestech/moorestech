using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Core.Master;
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
            // 写した行為を実行の記録で検証し、番地・個数・クリック枠の取り違えを赤にする
            // Verify converted actions through the execution record so swapped slots, counts or clicked slot turn red
            // 集めは grab が空のときだけクリック枠へ集めるので、分割で grab が埋まる前に置く
            // Collect targets the clicked slot only while grab is empty, so it runs before the split fills grab
            var operations = new RecordingPlayerInventoryOperations(9, 2);
            var wood = new ItemId(1);
            operations.SetEquipment(0, new SlotItem(wood, 5));
            operations.SetMain(1, new SlotItem(wood, 9));
            var actions = new[]
            {
                Convert(PlannedItemAction.Collect(ItemMoveSlotRef.Main(0))),
                Convert(PlannedItemAction.MoveItem(ItemMoveSlotRef.Equipment(0), ItemMoveSlotRef.Main(3), 2)),
                Convert(PlannedItemAction.Split(ItemMoveSlotRef.Main(1))),
            };
            Assert.IsInstanceOf<CollectAction>(actions[0]);
            Assert.IsInstanceOf<MoveBetweenSlotsAction>(actions[1]);
            Assert.IsInstanceOf<SplitToGrabAction>(actions[2]);

            var result = new PlayerItemActionExecutor(operations).Execute(actions);

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            Assert.AreEqual(new[] { $"{PlayerSlotRef.Equipment(0)}->{PlayerSlotRef.Main(3)} x2", $"{PlayerSlotRef.Main(1)}->Grab x4" }, operations.Moves.ToArray());
            Assert.AreEqual(new[] { PlayerSlotRef.Main(0).ToString() }, operations.Collects.ToArray());
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
