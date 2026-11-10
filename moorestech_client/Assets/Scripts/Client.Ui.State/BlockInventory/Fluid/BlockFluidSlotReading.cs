using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockInventory.Logic.Common;
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 追跡ブロックの詳細とパイプの複製から液体の列と進捗を読む入口（Model とテストが共有）
    /// The entry that reads the fluid list and progress from the tracked block's details and the pipe replica (shared by the model and tests)
    /// </summary>
    public static class BlockFluidSlotReading
    {
        public static BlockFluidSlotsViewModel Read(BlockGameObject block, IFluidReplicaReader replica)
        {
            var slots = new List<BlockFluidSlotState>();
            // 機械・タンク・液体プラットフォームは入力→出力の順
            // Machines, tanks and fluid platforms list inputs then outputs
            var fluid = block.GetStateDetail<FluidMachineInventoryStateDetail>(FluidMachineInventoryStateDetail.BlockStateDetailKey);
            if (fluid != null) slots.AddRange(BlockFluidSlotConversion.FromTanks(fluid.InputTanks, fluid.OutputTanks));
            // 複製に節がある座標だけパイプ
            // Only a position with a replica node is a pipe
            if (replica.TryReadNode(block.BlockPosInfo.OriginalPos, out var fluidId, out var amount, out var capacity)) slots.Add(BlockFluidSlotConversion.FromPipeNode(fluidId, amount, capacity));

            // 進捗を立てるのは機械と採掘機だけ（Web の dto.Progress と同じ）
            // Only machines and miners set progress (same as Web's dto.Progress)
            var machine = block.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
            if (machine != null) return new BlockFluidSlotsViewModel(slots, true, machine.ProcessingRate);
            var miner = block.GetStateDetail<CommonMinerBlockStateDetail>(CommonMinerBlockStateDetail.BlockStateDetailKey);
            var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
            if (miner != null && common != null) return new BlockFluidSlotsViewModel(slots, true, common.ProcessingRate);
            return new BlockFluidSlotsViewModel(slots, false, 0);
        }
    }
}
