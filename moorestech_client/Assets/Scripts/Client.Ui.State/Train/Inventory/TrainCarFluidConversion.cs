using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Common;
using Game.Block.Interface.State;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 貨車の液体を描画値へ写す。S4-machine のタンクの写しを入力側だけで使い、写しを 2 か所に持たない
    /// Maps car fluids to draw values by reusing the S4-machine tank conversion with inputs only, so the mapping lives in one place
    /// </summary>
    public static class TrainCarFluidConversion
    {
        public static IReadOnlyList<BlockFluidSlotState> ToFluids(IReadOnlyList<FluidMessagePack> fluids)
        {
            return BlockFluidSlotConversion.FromTanks(fluids, new List<FluidMessagePack>());
        }
    }
}
