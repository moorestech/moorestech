using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;
using Game.Block.Interface.State;
using Game.Fluid;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の列を組む。タンクは入力→出力の順、パイプは流体複製の節 1 つ（Web ホスト FluidSlotDtoBuilder の写し）
    /// Builds the fluid list: tanks input-then-output, a pipe as one replica node (port of the Web host's FluidSlotDtoBuilder)
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

        // 空の液体は Empty（uGUI MachineBlockInventoryView の EmptyFluidId 分岐と同じ）
        // The empty fluid becomes Empty (same as uGUI MachineBlockInventoryView's EmptyFluidId branch)
        private static BlockFluidSlotState ToSlot(int fluidId, double amount, double capacity)
        {
            var id = new FluidId(fluidId);
            return id == FluidMaster.EmptyFluidId ? BlockFluidSlotState.Empty(capacity) : BlockFluidSlotState.Filled(id, amount, capacity);
        }
    }
}
