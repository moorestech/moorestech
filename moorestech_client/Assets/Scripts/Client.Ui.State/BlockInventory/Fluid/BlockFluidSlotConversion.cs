using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;
using Game.Block.Interface.State;
using Game.Fluid;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の列を組む。タンクは入力→出力
    /// Builds the fluid list: tanks input-then-output, a pipe as one replica node, as the Web host
    /// </summary>
    public static class BlockFluidSlotConversion
    {
        public static IReadOnlyList<BlockFluidSlotState> FromTanks(IReadOnlyList<FluidMessagePack> inputTanks, IReadOnlyList<FluidMessagePack> outputTanks)
        {
            var slots = new List<BlockFluidSlotState>();
            foreach (var tank in inputTanks) slots.Add(ToSlot(tank.FluidId, tank.Amount, tank.MaxCapacity));
            foreach (var tank in outputTanks) slots.Add(ToSlot(tank.FluidId, tank.Amount, tank.MaxCapacity));
            return slots;
        }

        public static BlockFluidSlotState FromPipeNode(int fluidId, double amount, double capacity)
        {
            return ToSlot(fluidId, amount, capacity);
        }

        // 空の液体は Empty にする
        // The empty fluid becomes Empty, as uGUI MachineBlockInventoryView's EmptyFluidId branch
        private static BlockFluidSlotState ToSlot(int fluidId, double amount, double capacity)
        {
            var id = new FluidId(fluidId);
            return id == FluidMaster.EmptyFluidId ? BlockFluidSlotState.Empty(capacity) : BlockFluidSlotState.Filled(id, amount, capacity);
        }
    }
}
