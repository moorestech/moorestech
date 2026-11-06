using Client.Tests.Ui.State.ItemMove;
using Client.Ui.State.Inventory.Logic;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using NUnit.Framework;
using SlotRef = Client.Ui.State.ItemMove.ItemMoveSlotRef;

namespace Client.Tests.Ui.State.Inventory
{
    public class PlayerSlotActionPlannerTest
    {
        private static readonly SlotItem EmptySlot = ItemMoveFixtures.Slot(0, 0);
        private static readonly SlotItem[] NoEquipment = new SlotItem[0];

        private static PlayerInventorySnapshot Snapshot(SlotItem[] mainSlots, SlotItem grab, SlotItem[] equipment) => new(mainSlots, grab, equipment);
        private static PlayerInventorySnapshot Default() => Snapshot(new[] { ItemMoveFixtures.Slot(1, 5) }, EmptySlot, NoEquipment);
        private static PlayerSlotContext Closed(PlayerInventorySnapshot inventory) => PlayerSlotContext.WithBlockClosed(inventory, new FixedMaxStackLookup(100));
        private static PlayerInventorySnapshot Grabbing(SlotItem target) => Snapshot(new[] { target }, ItemMoveFixtures.Slot(9, 4), NoEquipment);

        /// <web-test file="src/features/inventory/slotActions.test.ts">クリック時の inventory・block slots・maxStack で移動を計画する</web-test>
        [Test]
        public void 操作の瞬間の在庫とブロックスロットとスタック上限で移動を計画する()
        {
            var blockSlots = new[] { ItemMoveFixtures.Slot(1, 8), EmptySlot };
            var context = PlayerSlotContext.WithBlockOpen(Default(), blockSlots, BlockSlotRestriction.Unrestricted, new FixedMaxStackLookup(10));
            ItemMoveAssert.PlanActions(PlayerSlotActionPlanner.PlanLeftDown(context, SlotRef.Main(0), true),
                PlannedItemAction.MoveItem(SlotRef.Main(0), SlotRef.Block(0), 2),
                PlannedItemAction.MoveItem(SlotRef.Main(0), SlotRef.Block(1), 3));
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">右クリック時の最新 grab 数で分割操作を選ぶ</web-test>
        [Test]
        public void 右クリックは操作の瞬間の持ち手で置くか分割かを選ぶ()
        {
            var inventory = Snapshot(new[] { ItemMoveFixtures.Slot(1, 5) }, ItemMoveFixtures.Slot(9, 3), NoEquipment);
            ItemMoveAssert.PlanActions(PlayerSlotActionPlanner.PlanRightDown(inventory, SlotRef.Main(0)), PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Main(0), 1));
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">右ドラッグ進入は grab がある時だけ1個配置する</web-test>
        [Test]
        public void 右ドラッグ進入は持ち手があるときだけ1個置く()
        {
            ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanRightEnter(Default(), SlotRef.Main(0)));
            var holding = Snapshot(new[] { EmptySlot }, ItemMoveFixtures.Slot(9, 3), NoEquipment);
            ItemMoveAssert.PlanActions(PlayerSlotActionPlanner.PlanRightEnter(holding, SlotRef.Main(0)), PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Main(0), 1));
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">装備枠の中身ありスロットへも全量moveを送る</web-test>
        [Test]
        public void 掴み中の左クリックは装備枠の中身ありへも全量を送る()
        {
            var inventory = Snapshot(new[] { EmptySlot }, ItemMoveFixtures.Slot(9, 4), new[] { ItemMoveFixtures.Slot(1, 5) });
            ItemMoveAssert.PlanActions(PlayerSlotActionPlanner.PlanLeftDown(Closed(inventory), SlotRef.Equipment(0), false), PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Equipment(0), 4));
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">別IDの中身ありスロットへは全量moveを送る（サーバーが入れ替える）</web-test>
        [Test]
        public void 掴み中の左クリックは別IDの中身ありへ全量を送る()
        {
            var plan = PlayerSlotActionPlanner.PlanLeftDown(Closed(Grabbing(ItemMoveFixtures.Slot(1, 5))), SlotRef.Main(0), false);
            ItemMoveAssert.PlanActions(plan, PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Main(0), 4));
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">同IDの中身ありスロットへも全量moveを送る</web-test>
        [Test]
        public void 掴み中の左クリックは同じIDの中身ありへも全量を送る()
        {
            var plan = PlayerSlotActionPlanner.PlanLeftDown(Closed(Grabbing(ItemMoveFixtures.Slot(9, 5))), SlotRef.Main(0), false);
            ItemMoveAssert.PlanActions(plan, PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Main(0), 4));
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">空スロットは従来どおりスプリットドラッグを開始し何も送らない</web-test>
        [Test]
        public void 掴み中の左クリックは空スロットで分割ドラッグを始め何も送らない()
        {
            var plan = PlayerSlotActionPlanner.PlanLeftDown(Closed(Grabbing(EmptySlot)), SlotRef.Main(0), false);
            Assert.IsInstanceOf<BeginSplitDragPlan>(plan);
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">equipment の範囲外 ref は例外を投げず何も送らない</web-test>
        [Test]
        public void 範囲外の装備番地は例外を投げず何も送らない()
        {
            AssertEveryGestureSendsNothing(Default(), SlotRef.Equipment(0));
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">main の範囲外 ref は例外を投げず何も送らない</web-test>
        [Test]
        public void 範囲外のメイン番地は例外を投げず何も送らない()
        {
            AssertEveryGestureSendsNothing(Default(), SlotRef.Main(9));
        }

        /// <web-test file="src/features/inventory/slotActions.test.ts">grab 保持中でも範囲外スロットへは何も送らない</web-test>
        [Test]
        public void 掴み中でも範囲外の番地へは何も送らない()
        {
            var inventory = Snapshot(new[] { EmptySlot }, ItemMoveFixtures.Slot(9, 4), NoEquipment);
            var target = SlotRef.Equipment(0);
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanLeftDown(Closed(inventory), target, false)));
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanRightDown(inventory, target)));
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanRightEnter(inventory, target)));
        }

        private static void AssertEveryGestureSendsNothing(PlayerInventorySnapshot inventory, ItemMoveSlotRef target)
        {
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanLeftDown(Closed(inventory), target, false)));
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanLeftDown(Closed(inventory), target, true)));
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanRightDown(inventory, target)));
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanDoubleClick(inventory, target)));
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanLeftEnter(inventory, target)));
            Assert.DoesNotThrow(() => ItemMoveAssert.SendsNothing(PlayerSlotActionPlanner.PlanRightEnter(inventory, target)));
        }
    }
}
