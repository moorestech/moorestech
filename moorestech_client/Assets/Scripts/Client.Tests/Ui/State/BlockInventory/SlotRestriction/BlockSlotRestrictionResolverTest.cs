using System;
using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.Machine;
using Client.Tests.Ui.State.ItemMove;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.ItemMove;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using SlotRef = Client.Ui.State.ItemMove.ItemMoveSlotRef;

namespace Client.Tests.Ui.State.BlockInventory.SlotRestriction
{
    /// <summary>
    /// Web の blockSlotRestriction.test.ts の写し（L0）。機械の 2 件は Web に無いので新設
    /// Port of Web blockSlotRestriction.test.ts (L0); the two machine cases are new since the Web has none
    /// </summary>
    public class BlockSlotRestrictionResolverTest
    {
        private static readonly ItemId PayloadItem = new(4);
        private static readonly ItemId SatelliteItem = new(5);

        /// <web-test file="src/features/inventory/blockSlotRestriction.test.ts">積荷は受け付ける積荷枠だけへ流し、先頭の空き資材枠や出力枠を選ばない</web-test>
        [Test]
        public void 発射台の積荷は受け付ける積荷枠だけへ流す()
        {
            CollectionAssert.AreEqual(new[] { 1 }, ShiftDestinations(RocketPad(), PayloadItem, 10));
        }

        /// <web-test file="src/features/inventory/blockSlotRestriction.test.ts">どの枠も受け付けないアイテムは送らず拒否として返す</web-test>
        [Test]
        public void 発射台のどの枠も受け付けないアイテムは送らず拒否する()
        {
            AssertRefused(ShiftMoveRefusalReason.NoAcceptingBlockSlot, ShiftPlan(RocketPad(), new ItemId(99), 10));
        }

        /// <web-test file="src/features/inventory/blockSlotRestriction.test.ts">受け付け情報が未着なら無制限へ倒さず送らない</web-test>
        [Test]
        public void 発射台の受け付け情報が未着なら無制限へ倒さず送らない()
        {
            var pending = BlockSlotRestrictionInput.OpenBlock("RocketLaunchPad", 3);
            Assert.AreSame(BlockSlotRestriction.DetailLoading, BlockSlotRestrictionResolver.Resolve(pending, PayloadItem));
            AssertRefused(ShiftMoveRefusalReason.DetailLoading, ShiftPlan(pending, PayloadItem, 3));
        }

        /// <web-test file="src/features/inventory/blockSlotRestriction.test.ts">運べる衛星は入口だけへ流し、出口へは入れない</web-test>
        [Test]
        public void 宇宙エレベーターの衛星は入口だけへ流し出口へは入れない()
        {
            CollectionAssert.AreEqual(new[] { 0 }, ShiftDestinations(SpaceElevator(), SatelliteItem, 3));
            AssertRefused(ShiftMoveRefusalReason.NoAcceptingBlockSlot, ShiftPlan(SpaceElevator(), new ItemId(99), 3));
        }

        /// <web-test file="src/features/inventory/blockSlotRestriction.test.ts">受け付け情報が未着なら送らない</web-test>
        [Test]
        public void 宇宙エレベーターの受け付け情報が未着なら送らない()
        {
            var pending = BlockSlotRestrictionInput.OpenBlock("SpaceElevatorBase", 3);
            Assert.AreSame(BlockSlotRestriction.DetailLoading, BlockSlotRestrictionResolver.Resolve(pending, SatelliteItem));
            AssertRefused(ShiftMoveRefusalReason.DetailLoading, ShiftPlan(pending, SatelliteItem, 3));
        }

        /// <web-test file="src/features/inventory/blockSlotRestriction.test.ts">詳細の有無によらず失敗を返し空在庫を探索しない</web-test>
        [Test]
        public void 取得失敗した在庫は詳細の有無によらず失敗を返す()
        {
            Assert.AreSame(BlockSlotRestriction.InventoryOpenFailed, BlockSlotRestrictionResolver.Resolve(BlockSlotRestrictionInput.OpenFailedBlock, SatelliteItem));
            AssertRefused(ShiftMoveRefusalReason.InventoryOpenFailed, ShiftPlanFor(BlockSlotRestrictionInput.OpenFailedBlock, SatelliteItem, 3, 3, false));
        }

        // 種別と食い違う詳細は組み立てた時点で例外になる
        // A detail that disagrees with the type throws at build time
        [Test]
        public void 種別と食い違う詳細は組み立てた時点で例外になる()
        {
            var inlet = new[] { new InletSlotBinding(0, new[] { SatelliteItem }) };
            Assert.Throws<ArgumentException>(() => BlockSlotRestrictionInput.OpenBlock("Machine", 7).WithInletBindings(inlet));
            Assert.Throws<ArgumentException>(() => BlockSlotRestrictionInput.OpenBlock("RocketLaunchPad", 3).WithMachine(MachineSlotGhostFixtures.BoundMachine()));
        }

        /// <web-test file="src/features/inventory/blockSlotRestriction.test.ts">チェストは無制限のまま</web-test>
        [Test]
        public void チェストは無制限のまま()
        {
            Assert.AreSame(BlockSlotRestriction.Unrestricted, BlockSlotRestrictionResolver.Resolve(BlockSlotRestrictionInput.OpenBlock("Chest", 3), PayloadItem));
        }

        [Test]
        public void 機械は入力帯だけ束縛で絞り出力帯とモジュール帯は候補に残す()
        {
            var machine = BlockSlotRestrictionInput.OpenBlock("Machine", 7).WithMachine(MachineSlotGhostFixtures.BoundMachine());
            var restriction = BlockSlotRestrictionResolver.Resolve(machine, new ItemId(5));
            CollectionAssert.AreEqual(new[] { 1, 3, 4, 5, 6 }, ((BoundBlockSlots)restriction).CandidateIndices);
        }

        [Test]
        public void 束縛の無い機械は無制限()
        {
            var unbound = BlockSlotRestrictionInput.OpenBlock("Machine", 7).WithMachine(MachineSlotGhostFixtures.Unbound());
            Assert.AreSame(BlockSlotRestriction.Unrestricted, BlockSlotRestrictionResolver.Resolve(unbound, new ItemId(5)));
        }

        private static OpenBlockSlotInput RocketPad()
        {
            return BlockSlotRestrictionInput.OpenBlock("RocketLaunchPad", 3).WithInletBindings(new[]
            {
                new InletSlotBinding(0, new[] { new ItemId(3) }),
                new InletSlotBinding(1, new[] { PayloadItem }),
            });
        }

        private static OpenBlockSlotInput SpaceElevator()
        {
            return BlockSlotRestrictionInput.OpenBlock("SpaceElevatorBase", 3).WithInletBindings(new[] { new InletSlotBinding(0, new[] { SatelliteItem }) });
        }

        // 持ち物の枠 0 の全量を Shift で配る計画（発射台の枠 0 は資材 20 個入り、他は空）
        // The plan for Shift-allocating main slot 0 in full (launch pad slot 0 holds 20 materials, others empty)
        private static PlayerSlotActionPlan ShiftPlan(OpenBlockSlotInput block, ItemId itemId, int count)
        {
            return ShiftPlanFor(block, itemId, count, block.ItemSlotCount, block.BlockType == "RocketLaunchPad");
        }

        private static PlayerSlotActionPlan ShiftPlanFor(BlockSlotRestrictionInput block, ItemId itemId, int count, int itemSlotCount, bool rocketPad)
        {
            var held = new SlotItem(itemId, count);
            var inventory = new PlayerInventorySnapshot(new[] { held }, new SlotItem(new ItemId(0), 0), new SlotItem[0]);
            var blockSlots = new List<SlotItem>();
            for (var i = 0; i < itemSlotCount; i++) blockSlots.Add(i == 0 && rocketPad ? new SlotItem(new ItemId(3), 20) : new SlotItem(new ItemId(0), 0));
            var context = PlayerSlotContext.WithBlockOpen(inventory, blockSlots, BlockSlotRestrictionResolver.Resolve(block, itemId), new FixedMaxStackLookup(1000));
            return PlayerSlotPlanner.PlanLeftClick(SlotRef.Main(0), held, true, context);
        }

        private static List<int> ShiftDestinations(OpenBlockSlotInput block, ItemId itemId, int count)
        {
            var plan = ShiftPlan(block, itemId, count);
            Assert.IsInstanceOf<ItemActionsPlan>(plan, "shift click must plan actions");
            var destinations = new List<int>();
            foreach (var action in ((ItemActionsPlan)plan).Actions)
            {
                Assert.AreEqual(PlannedItemActionKind.BlockInventoryMoveItem, action.Kind);
                destinations.Add(action.To.Slot);
            }
            return destinations;
        }

        private static void AssertRefused(ShiftMoveRefusalReason expected, PlayerSlotActionPlan plan)
        {
            Assert.IsInstanceOf<ShiftMoveRefusedPlan>(plan);
            Assert.AreEqual(expected, ((ShiftMoveRefusedPlan)plan).Reason);
        }
    }
}
