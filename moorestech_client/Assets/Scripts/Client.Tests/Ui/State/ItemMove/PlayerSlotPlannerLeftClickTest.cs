using Client.Ui.State.ItemMove;
using NUnit.Framework;
using static Client.Tests.Ui.State.ItemMove.ItemMoveFixtures;
using SlotRef = Client.Ui.State.ItemMove.ItemMoveSlotRef;

namespace Client.Tests.Ui.State.ItemMove
{
    public class PlayerSlotPlannerLeftClickTest
    {
        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">grab保持中の空スロットはスプリットドラッグ開始を返す</web-test>
        [Test]
        public void 掴み中の空スロットは分割ドラッグ開始を返す()
        {
            var plan = PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(1), Slot(0, 0), false, ClosedBlock(4));
            Assert.IsInstanceOf<BeginSplitDragPlan>(plan);
            Assert.AreEqual(SlotRef.Main(1), ((BeginSplitDragPlan)plan).Target);
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">grab保持中は grab 全量をクリックスロットへ置く</web-test>
        [Test]
        public void 掴み中は掴みの全量をクリックしたスロットへ置く()
        {
            var plan = PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 98), false, ClosedBlock(4));
            ItemMoveAssert.PlanActions(plan, PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Main(0), 4));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">grab保持中の中身ありは同ID・別IDとも全量置きへ回す（別IDはサーバーがswap）</web-test>
        [Test]
        public void 掴み中の中身ありは同じIDも別のIDも全量置きにする()
        {
            var expected = PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Main(0), 4);
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(9, 2), false, ClosedBlock(4)), expected);
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 2), false, ClosedBlock(4)), expected);
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">空手+空スロットは無操作</web-test>
        [Test]
        public void 空手で空スロットなら何もしない()
        {
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(1), Slot(0, 0), false, ClosedBlock(0)));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">空手+中身ありは全量を grab へ拾う</web-test>
        [Test]
        public void 空手で中身があれば全量を掴む()
        {
            var plan = PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 98), false, ClosedBlock(0));
            ItemMoveAssert.PlanActions(plan, PlannedItemAction.MoveItem(SlotRef.Main(0), SlotRef.Grab, 98));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">Shift+クリックはブロック開時 block へ配分する</web-test>
        [Test]
        public void ブロックが開いているときShiftはブロックへ配分する()
        {
            var context = OpenBlock(0, new[] { Slot(1, 99), Slot(0, 0) }, BlockSlotRestriction.Unrestricted);
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 5), true, context),
                PlannedItemAction.MoveItem(SlotRef.Main(0), SlotRef.Block(0), 1),
                PlannedItemAction.MoveItem(SlotRef.Main(0), SlotRef.Block(1), 4));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">束縛で受け付ける枠が無いShiftは送らず拒否を返し、空スロットは無操作のまま</web-test>
        [Test]
        public void 受け付ける枠が無いShiftは送らず断り空スロットは何もしない()
        {
            var context = OpenBlock(0, new[] { Slot(0, 0) }, BlockSlotRestriction.Bound(new int[0]));
            var refused = PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 5), true, context);
            Assert.IsInstanceOf<ShiftMoveRefusedPlan>(refused);
            Assert.AreEqual(ShiftMoveRefusalReason.NoAcceptingBlockSlot, ((ShiftMoveRefusedPlan)refused).Reason);
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(1), Slot(0, 0), true, context));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">%s は送信せず理由を返す</web-test>
        [Test]
        public void インベントリを開けなかったときは送らず理由を返す()
        {
            AssertRefused(BlockSlotRestriction.InventoryOpenFailed, ShiftMoveRefusalReason.InventoryOpenFailed);
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">%s は送信せず理由を返す</web-test>
        [Test]
        public void 詳細の取得中は送らず理由を返す()
        {
            AssertRefused(BlockSlotRestriction.DetailLoading, ShiftMoveRefusalReason.DetailLoading);
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">束縛で受け付ける枠があればそこだけへ配分する</web-test>
        [Test]
        public void 受け付ける枠があればそこだけへ配分する()
        {
            var context = OpenBlock(0, new[] { Slot(0, 0), Slot(0, 0) }, BlockSlotRestriction.Bound(new[] { 1 }));
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 5), true, context),
                PlannedItemAction.MoveItem(SlotRef.Main(0), SlotRef.Block(1), 5));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">Shift+クリックはブロック閉時、main からは配分先を持たず無操作になる（旧main⇔hotbar振り分けは廃止済み）</web-test>
        [Test]
        public void ブロックが閉じているときメインからのShiftは何もしない()
        {
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 5), true, ClosedBlock(0)));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">equipment からの Shift は持ち物本体(main)へ戻す</web-test>
        [Test]
        public void 装備からのShiftは持ち物本体へ戻す()
        {
            var plan = PlayerSlotPlanner.PlanLeftClick(SlotRef.Equipment(0), Slot(1, 1), true, ClosedBlock(0));
            ItemMoveAssert.PlanActions(plan, PlannedItemAction.MoveItem(SlotRef.Equipment(0), SlotRef.Main(0), 1));
        }

        /// <web-test file="src/shared/itemMove/playerSlotPlan.test.ts">grab 起点の Shift は配分先を持たず、掴み状態に応じて全量置きか無操作になる</web-test>
        [Test]
        public void 持ち手起点のShiftは掴み状態に応じて全量置きか何もしない()
        {
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Grab, Slot(9, 4), true, ClosedBlock(4)),
                PlannedItemAction.MoveItem(SlotRef.Grab, SlotRef.Grab, 4));
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Grab, Slot(0, 0), true, ClosedBlock(0)));
        }

        // Web は満杯を無音の無操作にするが、UITK は理由付きの断りにして通知・ログへ回す
        // The Web treats full as a silent no-op; UITK refuses with a reason so it is notified and logged
        [Test]
        public void ブロックが満杯ならShiftは送らず満杯の理由を返す()
        {
            var context = OpenBlock(0, new[] { Slot(2, 100), Slot(3, 100) }, BlockSlotRestriction.Unrestricted);
            var plan = PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 5), true, context);
            Assert.IsInstanceOf<ShiftMoveRefusedPlan>(plan);
            Assert.AreEqual(ShiftMoveRefusalReason.DestinationFull, ((ShiftMoveRefusedPlan)plan).Reason);
        }

        [Test]
        public void 未知のIDのShiftは配分する腕でだけ未知の断りを返す()
        {
            // D-65: 断るのは DirectMovePlanner を呼ぶ腕だけ。ブロック閉の main 起点は配分先が無いので何もしない
            // D-65: only arms that call DirectMovePlanner refuse; a main source with the block closed has no destination and does nothing
            var stack = new FixedMaxStackLookup(100);
            stack.SetUnknown(new Core.Master.ItemId(9));
            var open = PlayerSlotContext.WithBlockOpen(PlayerInventory(0), new[] { Slot(0, 0) }, BlockSlotRestriction.Unrestricted, stack);
            var closed = PlayerSlotContext.WithBlockClosed(PlayerInventory(0), stack);
            Assert.AreEqual(ShiftMoveRefusalReason.UnknownItem, ((ShiftMoveRefusedPlan)PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(9, 5), true, open)).Reason);
            Assert.AreEqual(ShiftMoveRefusalReason.UnknownItem, ((ShiftMoveRefusedPlan)PlayerSlotPlanner.PlanLeftClick(SlotRef.Equipment(0), Slot(9, 5), true, closed)).Reason);
            ItemMoveAssert.PlanActions(PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(9, 5), true, closed));
        }

        // 枠数が縮んだ直後の古い束縛は例外にせず、古い番地として無視する
        // A stale binding right after the slot count shrank is ignored as a stale ref instead of throwing
        [Test]
        public void 枠数を超える候補番号を持つ古い束縛のShiftは古い番地として無視する()
        {
            var context = OpenBlock(0, new[] { Slot(0, 0) }, BlockSlotRestriction.Bound(new[] { 0, 3 }));
            var plan = PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 5), true, context);
            Assert.IsInstanceOf<StaleSlotIgnoredPlan>(plan);
            Assert.AreEqual(SlotRef.Block(3), ((StaleSlotIgnoredPlan)plan).Target);
        }

        [Test]
        public void アイテム枠0のブロックへの無制約Shiftは満杯でなく受け付ける枠なしを返す()
        {
            AssertRefused(BlockSlotRestriction.Unrestricted, ShiftMoveRefusalReason.NoAcceptingBlockSlot);
        }

        private static void AssertRefused(BlockSlotRestriction restriction, ShiftMoveRefusalReason expected)
        {
            var plan = PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), Slot(1, 5), true, OpenBlock(0, new Client.Ui.State.Items.SlotItem[0], restriction));
            Assert.IsInstanceOf<ShiftMoveRefusedPlan>(plan);
            Assert.AreEqual(expected, ((ShiftMoveRefusedPlan)plan).Reason);
        }
    }
}
