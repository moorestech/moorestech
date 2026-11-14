using Game.Block.Interface.State;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;
using Game.Fluid;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    internal static class FluidSlotDtoBuilder
    {
        public static void Append(List<BlockFluidSlotDto> slots, IEnumerable<FluidMessagePack> tanks)
        {
            foreach (var tank in tanks)
            {
                // 空流体はGuid空文字（uGUI MachineBlockInventoryView の EmptyFluidId 分岐と同じ扱い）
                // Empty fluids get an empty GUID (same handling as the EmptyFluidId branch in uGUI MachineBlockInventoryView)
                var isEmpty = new FluidId(tank.FluidId) == FluidMaster.EmptyFluidId;
                var fluidGuid = isEmpty ? "" : MasterHolder.FluidMaster.GetFluidMaster(new FluidId(tank.FluidId)).FluidGuid.ToString("D");
                slots.Add(new BlockFluidSlotDto { FluidId = tank.FluidId, Amount = tank.Amount, Capacity = tank.MaxCapacity, FluidGuid = fluidGuid });
            }
        }

        // State が空/中身ありを決めた液体の列を写す（空は空液体の ID・量 0・Guid 空文字）
        // Map a fluid list whose empty/filled split State already decided (empty: the empty fluid id, amount 0, empty GUID)
        public static void AppendStates(List<BlockFluidSlotDto> slots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            foreach (var fluid in fluids)
            {
                slots.Add(fluid is FilledBlockFluidSlotState filled
                    ? new BlockFluidSlotDto { FluidId = filled.FluidId.AsPrimitive(), Amount = filled.Amount, Capacity = filled.Capacity, FluidGuid = MasterHolder.FluidMaster.GetFluidMaster(filled.FluidId).FluidGuid.ToString("D") }
                    : new BlockFluidSlotDto { FluidId = FluidMaster.EmptyFluidId.AsPrimitive(), Amount = 0, Capacity = fluid.Capacity, FluidGuid = "" });
            }
        }
    }
}
