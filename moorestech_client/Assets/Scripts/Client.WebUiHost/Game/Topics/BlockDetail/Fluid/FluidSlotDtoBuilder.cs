using Game.Block.Interface.State;
using System.Collections.Generic;
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

    }
}
