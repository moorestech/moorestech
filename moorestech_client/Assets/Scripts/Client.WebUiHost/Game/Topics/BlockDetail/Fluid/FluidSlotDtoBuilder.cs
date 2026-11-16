using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Core.Master;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    internal static class FluidSlotDtoBuilder
    {
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
