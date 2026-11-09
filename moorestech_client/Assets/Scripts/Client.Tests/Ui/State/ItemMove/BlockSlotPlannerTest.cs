using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using NUnit.Framework;
using static Client.Tests.Ui.State.ItemMove.ItemMoveFixtures;
using SlotRef = Client.Ui.State.ItemMove.ItemMoveSlotRef;

namespace Client.Tests.Ui.State.ItemMove
{
    public class BlockSlotPlannerTest
    {
        private static readonly FixedMaxStackLookup Stack100 = new(100);
        private static readonly SlotItem[] EmptyMain = { Slot(0, 0) };

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">grab保持中は grab 全量を block スロットへ置く（空スロットでも置く）</web-test>
        [Test]
        public void 左_掴み中は掴みの全量をブロックスロットへ置く()
        {
            var actions = BlockSlotPlanner.PlanLeftClick(1, Slot(0, 0), false, 4, EmptyMain, Stack100).Actions;
            ItemMoveAssert.Actions(actions, PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Block(1), 4));
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">空手+中身ありは全量を grab へ拾う</web-test>
        [Test]
        public void 左_空手で中身があれば全量を掴む()
        {
            var actions = BlockSlotPlanner.PlanLeftClick(2, Slot(10, 6), false, 0, EmptyMain, Stack100).Actions;
            ItemMoveAssert.Actions(actions, PlannedItemAction.MoveItem(SlotRef.Block(2), SlotRef.Grab, 6));
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">空手+空スロットは無操作</web-test>
        [Test]
        public void 左_空手で空スロットなら何もしない()
        {
            ItemMoveAssert.Actions(BlockSlotPlanner.PlanLeftClick(3, Slot(0, 0), false, 0, EmptyMain, Stack100).Actions);
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">Shift+クリックは main の同種スタック→空きの順に配分する</web-test>
        [Test]
        public void 左_Shiftはメインへ同種スタックから空きの順に配分する()
        {
            var actions = BlockSlotPlanner.PlanLeftClick(4, Slot(1, 7), true, 0, new[] { Slot(1, 98), Slot(0, 0) }, Stack100).Actions;
            ItemMoveAssert.Actions(actions,
                PlannedItemAction.MoveItem(SlotRef.Block(4), SlotRef.Main(0), 2),
                PlannedItemAction.MoveItem(SlotRef.Block(4), SlotRef.Main(1), 5));
        }

        [Test]
        public void 左_Shiftの配分先が満杯なら行為を持たず満杯の断りを返す()
        {
            // D-4: 送らない理由は計画器が返す。呼び手は条件を写さない
            // D-4: the planner returns why nothing is sent; callers never re-derive the condition
            var plan = BlockSlotPlanner.PlanLeftClick(4, Slot(1, 7), true, 0, new[] { Slot(1, 100) }, Stack100);
            Assert.AreEqual(ShiftMoveRefusalReason.DestinationFull, plan.Refusal);
            CollectionAssert.IsEmpty(plan.Actions);
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">grab保持中の Shift は通常の置きと同じ（uGUI同様 grab が優先）</web-test>
        [Test]
        public void 左_掴み中のShiftは通常の置きと同じ()
        {
            var actions = BlockSlotPlanner.PlanLeftClick(0, Slot(1, 7), true, 4, EmptyMain, Stack100).Actions;
            ItemMoveAssert.Actions(actions, PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Block(0), 4));
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">grab保持中は block スロットへ1個置く</web-test>
        [Test]
        public void 右_掴み中はブロックスロットへ1個置く()
        {
            ItemMoveAssert.Actions(BlockSlotPlanner.PlanRightClick(2, Slot(0, 0), 5), PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Block(2), 1));
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">空手+中身ありは block_inventory.split（半分掴みはホスト計算）</web-test>
        [Test]
        public void 右_空手で中身があれば分割を送る()
        {
            ItemMoveAssert.Actions(BlockSlotPlanner.PlanRightClick(0, Slot(1, 7), 0), PlannedItemAction.Split(SlotRef.Block(0)));
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">空手+1個も split を送る（半分0の no-op 判断はホスト）</web-test>
        [Test]
        public void 右_空手で1個でも分割を送る()
        {
            ItemMoveAssert.Actions(BlockSlotPlanner.PlanRightClick(0, Slot(1, 1), 0), PlannedItemAction.Split(SlotRef.Block(0)));
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">空手+空スロットは無操作</web-test>
        [Test]
        public void 右_空手で空スロットなら何もしない()
        {
            ItemMoveAssert.Actions(BlockSlotPlanner.PlanRightClick(0, Slot(0, 0), 0));
        }

        /// <web-test file="src/shared/itemMove/blockSlotPlan.test.ts">クリックスロットを送るだけ（収集先はホストが grab 状態で決める）</web-test>
        [Test]
        public void ダブル_クリックしたスロットを送るだけ()
        {
            ItemMoveAssert.Actions(BlockSlotPlanner.PlanDoubleClick(1), PlannedItemAction.Collect(SlotRef.Block(1)));
        }
    }
}
