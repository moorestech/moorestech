using Client.Ui.State.ItemMove;
using NUnit.Framework;
using static Client.Tests.Ui.State.ItemMove.ItemMoveFixtures;
using SlotRef = Client.Ui.State.ItemMove.ItemMoveSlotRef;

namespace Client.Tests.Ui.State.ItemMove
{
    public class PlayerSlotPlannerRightAndDoubleClickTest
    {
        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">grab保持中はクリックスロットへ1個置く</web-test>
        [Test]
        public void 右_掴み中はクリックしたスロットへ1個置く()
        {
            ItemMoveAssert.Actions(PlayerSlotPlanner.PlanRightClick(SlotRef.Main(1), Slot(0, 0), 4), PlannedItemAction.InventoryMoveItem(SlotRef.Grab, SlotRef.Main(1), 1));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">空手+中身ありは inventory.split（半分掴みはホスト計算）</web-test>
        [Test]
        public void 右_空手で中身があれば分割を送る()
        {
            ItemMoveAssert.Actions(PlayerSlotPlanner.PlanRightClick(SlotRef.Main(0), Slot(1, 7), 0), PlannedItemAction.InventorySplit(SlotRef.Main(0)));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">空手+空スロットは無操作</web-test>
        [Test]
        public void 右_空手で空スロットなら何もしない()
        {
            ItemMoveAssert.Actions(PlayerSlotPlanner.PlanRightClick(SlotRef.Main(1), Slot(0, 0), 0));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">クリックスロットを送るだけ（収集先はホストが grab 状態で決める）</web-test>
        [Test]
        public void ダブル_クリックしたスロットを送るだけ()
        {
            ItemMoveAssert.Actions(PlayerSlotPlanner.PlanDoubleClick(SlotRef.Main(2)), PlannedItemAction.InventoryCollect(SlotRef.Main(2)));
        }
    }
}
