using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// machineSlotGhosts.test.ts の共通データ（入力 3・出力 3・モジュール 1 の 7 スロット機械）と検査の補助
    /// Shared data of machineSlotGhosts.test.ts (a 7-slot machine: 3 inputs, 3 outputs, 1 module) and assertion helpers
    /// </summary>
    internal static class MachineSlotGhostFixtures
    {
        public static readonly FluidId InputFluid = new FluidId(1);

        // input(0,1) へ素材 2 件、output(3) へ生産物 1 件、tank(0) へ入力液体 1 件
        // Two inputs on slots 0 and 1, one output on slot 3, one input fluid on tank 0
        public static MachineBindingDetail Machine()
        {
            return WithTanks(new[] { new MachineTankBinding(0, InputFluid, 10) });
        }

        public static MachineBindingDetail WithTanks(MachineTankBinding[] tanks)
        {
            var bindings = new[]
            {
                Binding(0, 1, 2, new[] { 1, 11, 12 }),
                Binding(1, 5, 1, new[] { 5 }),
                Binding(3, 2, 1, new[] { 2 }),
            };
            return new MachineBindingDetail(new MachineSlotLayout(3, 3, 1), bindings, tanks);
        }

        public static MachineBindingDetail Unbound()
        {
            return new MachineBindingDetail(new MachineSlotLayout(3, 3, 1), new MachineItemSlotBinding[0], new MachineTankBinding[0]);
        }

        public static BlockItemSlotState[] EmptyItemSlots()
        {
            var slots = new BlockItemSlotState[7];
            for (var i = 0; i < slots.Length; i++) slots[i] = new BlockItemSlotState(new ItemId(0), 0);
            return slots;
        }

        public static BlockFluidSlotState[] EmptyFluidSlots(int count)
        {
            var slots = new BlockFluidSlotState[count];
            for (var i = 0; i < count; i++) slots[i] = BlockFluidSlotState.Empty(100);
            return slots;
        }

        public static ItemId[] Ids(int[] ids)
        {
            var result = new ItemId[ids.Length];
            for (var i = 0; i < ids.Length; i++) result[i] = new ItemId(ids[i]);
            return result;
        }

        public static void AssertGhostSlot(BoundItemSlot actual, int index, int itemId, int count, int[] accepted)
        {
            Assert.AreEqual(index, actual.Index);
            Assert.IsInstanceOf<GhostedItemSlot>(actual, $"slot {index} must have a ghost");
            var ghost = ((GhostedItemSlot)actual).Ghost;
            Assert.AreEqual(new ItemId(itemId), ghost.ItemId);
            Assert.AreEqual(count, ghost.Count);
            CollectionAssert.AreEqual(Ids(accepted), ghost.AcceptedItemIds);
        }

        public static void AssertGhostlessSlot(BoundItemSlot actual, int index)
        {
            Assert.AreEqual(index, actual.Index);
            Assert.IsInstanceOf<PlainItemSlot>(actual, $"slot {index} must have no ghost");
        }

        private static MachineItemSlotBinding Binding(int slot, int itemId, int count, int[] accepted)
        {
            return new MachineItemSlotBinding(slot, new ItemId(itemId), count, Ids(accepted));
        }
    }
}
