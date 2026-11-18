using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockInventory.Logic.Common;
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の列と進捗を読む入口
    /// Entry reading the fluid list and progress from the tracked block's details and pipe replica
    /// </summary>
    public static class BlockFluidSlotReading
    {
        public static BlockFluidSlotsViewModel Read(BlockGameObject block, IFluidReplicaReader replica)
        {
            var slots = new List<BlockFluidSlotState>();
            // 入力→出力の順に並べる
            // Inputs then outputs
            var fluid = block.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey);
            if (fluid != null) slots.AddRange(BlockFluidSlotConversion.FromTanks(fluid.InputTanks, fluid.OutputTanks));
            // 複製に節がある座標だけパイプ
            // Only a position with a replica node is a pipe
            if (replica.TryReadNode(block.BlockPosInfo.OriginalPos, out var fluidId, out var amount, out var capacity)) slots.Add(BlockFluidSlotConversion.FromPipeNode(fluidId, amount, capacity));

            // 進捗を立てるのは機械と採掘機だけ
            // Only machines and miners set progress, as Web's dto.Progress
            var machine = block.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
            if (machine != null) return new BlockFluidSlotsViewModel(slots, true, machine.ProcessingRate);
            var miner = block.GetStateDetail<CommonMinerBlockStateDetail>(CommonMinerBlockStateDetail.BlockStateDetailKey);
            var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            if (miner != null && common != null) return new BlockFluidSlotsViewModel(slots, true, common.ProcessingRate);
            return new BlockFluidSlotsViewModel(slots, false, 0);
        }
    }
}
